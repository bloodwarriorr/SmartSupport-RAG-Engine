namespace SmartSupport.Domain.Entities;

/// <summary>Links a user to an organization with a role. For now a user can belong to one organization only.</summary>
public class Membership
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public OrgRole Role { get; set; } = OrgRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public bool IsAdmin => Role == OrgRole.Admin;
}
