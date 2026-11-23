using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Common.Exceptions;
using SmartSupport.Application.Common.Interfaces;
using SmartSupport.Domain.Entities;

namespace SmartSupport.Application.Organizations;

/// <summary>
/// Answers "who is calling, and what may they do?" for every use case, so no service
/// can forget an access check.
/// </summary>
public interface IOrganizationAccess
{
    /// <summary>The caller's user record, created on first sign-in (and kept in sync with their Google name).</summary>
    Task<User> GetOrCreateCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>The caller's membership, or null if they haven't joined an organization.</summary>
    Task<Membership?> FindCurrentMembershipAsync(CancellationToken cancellationToken = default);

    /// <summary>The caller's membership; throws 403 if they have no organization.</summary>
    Task<Membership> RequireMembershipAsync(CancellationToken cancellationToken = default);

    /// <summary>The caller's membership; throws 403 unless they are an admin.</summary>
    Task<Membership> RequireAdminAsync(string? reason = null, CancellationToken cancellationToken = default);
}

public sealed class OrganizationAccess : IOrganizationAccess
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationAccess(ICurrentUser currentUser, IUserRepository users, IOrganizationRepository organizations, IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _organizations = organizations;
        _unitOfWork = unitOfWork;
    }

    public async Task<User> GetOrCreateCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var googleId = RequireGoogleId();
        var user = await _users.FindByGoogleIdAsync(googleId, cancellationToken);

        if (user == null)
        {
            user = new User
            {
                GoogleId = googleId,
                Email = _currentUser.Email ?? string.Empty,
                Name = _currentUser.Name
            };
            _users.Add(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (_currentUser.Name != null && user.Name != _currentUser.Name)
        {
            user.Name = _currentUser.Name;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public Task<Membership?> FindCurrentMembershipAsync(CancellationToken cancellationToken = default) =>
        _organizations.FindMembershipByGoogleIdAsync(RequireGoogleId(), cancellationToken);

    public async Task<Membership> RequireMembershipAsync(CancellationToken cancellationToken = default) =>
        await FindCurrentMembershipAsync(cancellationToken)
        ?? throw new ForbiddenException("Join or create an organization first.");

    public async Task<Membership> RequireAdminAsync(string? reason = null, CancellationToken cancellationToken = default)
    {
        var membership = await RequireMembershipAsync(cancellationToken);
        if (!membership.IsAdmin)
            throw new ForbiddenException(reason ?? "Only organization admins can do this.");
        return membership;
    }

    private string RequireGoogleId() =>
        _currentUser.GoogleId ?? throw new UnauthenticatedException();
}
