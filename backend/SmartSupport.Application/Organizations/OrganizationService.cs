using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Common.Exceptions;
using SmartSupport.Application.Common.Interfaces;
using SmartSupport.Domain.Entities;

namespace SmartSupport.Application.Organizations;

// ---------- DTOs (JSON shapes the client relies on) ----------

public record UserDto(string? Name, string Email);
public record MyOrganizationDto(Guid Id, string Name, string Role, int DocumentCount);
public record MeDto(UserDto User, MyOrganizationDto? Org);
public record OrganizationDto(Guid Id, string Name, string Role);
public record InviteCodeDto(string Code);

public interface IOrganizationService
{
    Task<MeDto> GetMeAsync(CancellationToken cancellationToken = default);
    Task<OrganizationDto> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<OrganizationDto> JoinAsync(string code, CancellationToken cancellationToken = default);
    Task<InviteCodeDto> GetInviteCodeAsync(CancellationToken cancellationToken = default);
    Task<InviteCodeDto> RegenerateInviteCodeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberSummary>> ListMembersAsync(CancellationToken cancellationToken = default);
}

public sealed class OrganizationService : IOrganizationService
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 100;

    private readonly IOrganizationAccess _access;
    private readonly IOrganizationRepository _organizations;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationService(IOrganizationAccess access, IOrganizationRepository organizations, IDocumentRepository documents, IUnitOfWork unitOfWork)
    {
        _access = access;
        _organizations = organizations;
        _documents = documents;
        _unitOfWork = unitOfWork;
    }

    public async Task<MeDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        // Also creates the user record on first sign-in.
        var user = await _access.GetOrCreateCurrentUserAsync(cancellationToken);
        var membership = await _organizations.FindMembershipByUserIdAsync(user.Id, cancellationToken);

        MyOrganizationDto? org = null;
        if (membership != null)
        {
            var documentCount = await _documents.CountAsync(membership.OrganizationId, cancellationToken);
            org = new MyOrganizationDto(membership.OrganizationId, membership.Organization.Name, membership.Role.ToString(), documentCount);
        }

        return new MeDto(new UserDto(user.Name, user.Email), org);
    }

    public async Task<OrganizationDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length < MinNameLength || name.Length > MaxNameLength)
            throw new ValidationException($"Organization name must be {MinNameLength}-{MaxNameLength} characters.");

        var user = await _access.GetOrCreateCurrentUserAsync(cancellationToken);
        if (await _organizations.FindMembershipByUserIdAsync(user.Id, cancellationToken) != null)
            throw new ConflictException("You already belong to an organization.");

        var organization = new Organization
        {
            Name = name,
            InviteCode = await GenerateUniqueCodeAsync(cancellationToken)
        };
        _organizations.Add(organization);
        // The creator is the first admin.
        _organizations.AddMembership(new Membership { UserId = user.Id, Organization = organization, Role = OrgRole.Admin });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(organization.Id, organization.Name, OrgRole.Admin.ToString());
    }

    public async Task<OrganizationDto> JoinAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = InviteCode.Normalize(code);
        if (normalized.Length == 0)
            throw new ValidationException("Please enter an invite code.");

        var user = await _access.GetOrCreateCurrentUserAsync(cancellationToken);
        var existing = await _organizations.FindMembershipByUserIdAsync(user.Id, cancellationToken);
        if (existing != null)
        {
            // Opening the same invite link twice is fine.
            if (existing.Organization.InviteCode == normalized)
                return new OrganizationDto(existing.OrganizationId, existing.Organization.Name, existing.Role.ToString());
            throw new ConflictException($"You already belong to {existing.Organization.Name}.");
        }

        var organization = await _organizations.FindByInviteCodeAsync(normalized, cancellationToken)
            ?? throw new NotFoundException("That invite code is not valid. Ask your admin for a new one.");

        // Everyone who joins with a code is a member; only the creator starts as admin.
        _organizations.AddMembership(new Membership { UserId = user.Id, OrganizationId = organization.Id, Role = OrgRole.Member });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrganizationDto(organization.Id, organization.Name, OrgRole.Member.ToString());
    }

    public async Task<InviteCodeDto> GetInviteCodeAsync(CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireAdminAsync("Only organization admins can invite people.", cancellationToken);
        return new InviteCodeDto(InviteCode.Format(membership.Organization.InviteCode));
    }

    public async Task<InviteCodeDto> RegenerateInviteCodeAsync(CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireAdminAsync("Only organization admins can invite people.", cancellationToken);

        // The old code stops working immediately.
        membership.Organization.InviteCode = await GenerateUniqueCodeAsync(cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new InviteCodeDto(InviteCode.Format(membership.Organization.InviteCode));
    }

    public async Task<IReadOnlyList<MemberSummary>> ListMembersAsync(CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireAdminAsync("Only organization admins can see the member list.", cancellationToken);
        return await _organizations.ListMembersAsync(membership.OrganizationId, cancellationToken);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = InviteCode.Generate();
            if (!await _organizations.InviteCodeExistsAsync(code, cancellationToken))
                return code;
        }
    }
}
