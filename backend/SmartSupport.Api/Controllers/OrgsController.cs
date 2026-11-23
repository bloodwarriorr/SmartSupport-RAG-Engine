using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartSupport.Api.Data;
using SmartSupport.Api.Models;
using SmartSupport.Api.Services;

namespace SmartSupport.Api.Controllers;

public record CreateOrgRequest(string Name);
public record JoinOrgRequest(string Code);

[Authorize]
[ApiController]
[Route("api/orgs")]
public class OrgsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OrgAccessService _orgAccess;

    public OrgsController(AppDbContext db, OrgAccessService orgAccess)
    {
        _db = db;
        _orgAccess = orgAccess;
    }

    /// <summary>
    /// Who am I and which organization am I in? Called right after sign-in.
    /// Also creates the user record on first sign-in.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _orgAccess.GetOrCreateUserAsync(User);
        var membership = await _orgAccess.GetMembershipAsync(User);

        return Ok(new
        {
            user = new { user.Name, user.Email },
            org = membership == null ? null : new
            {
                membership.Organization.Id,
                membership.Organization.Name,
                Role = membership.Role.ToString(),
                DocumentCount = await _db.Documents.CountAsync(d => d.OrganizationId == membership.OrganizationId)
            }
        });
    }

    /// <summary>Creates an organization. The creator becomes its admin.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrgRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 100)
            return BadRequest(new { message = "Organization name must be 2-100 characters." });

        var user = await _orgAccess.GetOrCreateUserAsync(User);
        if (await _db.Memberships.AnyAsync(m => m.UserId == user.Id))
            return Conflict(new { message = "You already belong to an organization." });

        var org = new Organization
        {
            Name = name,
            InviteCode = await _orgAccess.GenerateUniqueInviteCodeAsync()
        };
        _db.Organizations.Add(org);
        _db.Memberships.Add(new Membership { UserId = user.Id, Organization = org, Role = OrgRole.Admin });
        await _db.SaveChangesAsync();

        return Ok(new { org.Id, org.Name, Role = OrgRole.Admin.ToString() });
    }

    /// <summary>Joins an organization with its invite code. Joiners become members.</summary>
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinOrgRequest request)
    {
        var code = OrgAccessService.NormalizeInviteCode(request.Code ?? string.Empty);
        if (code.Length == 0)
            return BadRequest(new { message = "Please enter an invite code." });

        var user = await _orgAccess.GetOrCreateUserAsync(User);
        var existing = await _db.Memberships.Include(m => m.Organization).FirstOrDefaultAsync(m => m.UserId == user.Id);
        if (existing != null)
        {
            // Opening the same invite link twice is fine.
            if (existing.Organization.InviteCode == code)
                return Ok(new { existing.Organization.Id, existing.Organization.Name, Role = existing.Role.ToString() });
            return Conflict(new { message = $"You already belong to {existing.Organization.Name}." });
        }

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.InviteCode == code);
        if (org == null)
            return NotFound(new { message = "That invite code is not valid. Ask your admin for a new one." });

        _db.Memberships.Add(new Membership { UserId = user.Id, OrganizationId = org.Id, Role = OrgRole.Member });
        await _db.SaveChangesAsync();

        return Ok(new { org.Id, org.Name, Role = OrgRole.Member.ToString() });
    }

    /// <summary>Admin only: the current invite code.</summary>
    [HttpGet("invite")]
    public async Task<IActionResult> GetInvite()
    {
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null || membership.Role != OrgRole.Admin) return Forbid();

        return Ok(new { code = OrgAccessService.FormatInviteCode(membership.Organization.InviteCode) });
    }

    /// <summary>Admin only: replaces the invite code. The old code stops working.</summary>
    [HttpPost("invite/regenerate")]
    public async Task<IActionResult> RegenerateInvite()
    {
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null || membership.Role != OrgRole.Admin) return Forbid();

        membership.Organization.InviteCode = await _orgAccess.GenerateUniqueInviteCodeAsync();
        await _db.SaveChangesAsync();

        return Ok(new { code = OrgAccessService.FormatInviteCode(membership.Organization.InviteCode) });
    }

    /// <summary>Admin only: who is in the organization.</summary>
    [HttpGet("members")]
    public async Task<IActionResult> Members()
    {
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null || membership.Role != OrgRole.Admin) return Forbid();

        var members = await _db.Memberships
            .Where(m => m.OrganizationId == membership.OrganizationId)
            .OrderByDescending(m => m.Role == OrgRole.Admin).ThenBy(m => m.JoinedAt)
            .Select(m => new { m.User.Name, m.User.Email, Role = m.Role.ToString(), m.JoinedAt })
            .ToListAsync();

        return Ok(members);
    }
}
