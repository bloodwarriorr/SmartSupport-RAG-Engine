using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartSupport.Api.Contracts;
using SmartSupport.Api.Extensions;
using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Organizations;

namespace SmartSupport.Api.Controllers;

/// <summary>The caller's organization: create, join with an invite code, and (admins) manage invites.</summary>
[Authorize]
[ApiController]
[Route("api/orgs")]
public class OrgsController : ControllerBase
{
    private readonly IOrganizationService _organizations;

    public OrgsController(IOrganizationService organizations)
    {
        _organizations = organizations;
    }

    /// <summary>Who am I and which organization am I in? Called right after sign-in.</summary>
    [HttpGet("me")]
    public Task<MeDto> Me(CancellationToken cancellationToken) =>
        _organizations.GetMeAsync(cancellationToken);

    /// <summary>Creates an organization; the caller becomes its admin.</summary>
    [HttpPost]
    public Task<OrganizationDto> Create(CreateOrganizationRequest request, CancellationToken cancellationToken) =>
        _organizations.CreateAsync(request.Name, cancellationToken);

    /// <summary>Joins an organization with its invite code, as a member.</summary>
    [HttpPost("join")]
    [EnableRateLimiting(ApiServiceExtensions.JoinPolicy)]
    public Task<OrganizationDto> Join(JoinOrganizationRequest request, CancellationToken cancellationToken) =>
        _organizations.JoinAsync(request.Code, cancellationToken);

    /// <summary>Admin only: the current invite code.</summary>
    [HttpGet("invite")]
    public Task<InviteCodeDto> GetInvite(CancellationToken cancellationToken) =>
        _organizations.GetInviteCodeAsync(cancellationToken);

    /// <summary>Admin only: replaces the invite code. The old code stops working.</summary>
    [HttpPost("invite/regenerate")]
    public Task<InviteCodeDto> RegenerateInvite(CancellationToken cancellationToken) =>
        _organizations.RegenerateInviteCodeAsync(cancellationToken);

    /// <summary>Admin only: who is in the organization.</summary>
    [HttpGet("members")]
    public Task<IReadOnlyList<MemberSummary>> Members(CancellationToken cancellationToken) =>
        _organizations.ListMembersAsync(cancellationToken);
}
