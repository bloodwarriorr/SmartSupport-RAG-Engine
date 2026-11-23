namespace SmartSupport.Api.Models
{
    public class Organization
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;

        /// <summary>Reusable join code, stored without the dash (e.g. "K7P2XQ9M"). Admins can regenerate it.</summary>
        public string InviteCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Membership> Memberships { get; set; } = new();
    }

    public enum OrgRole
    {
        Member = 0,
        Admin = 1
    }

    /// <summary>Links a user to an organization with a role. For now a user can belong to one organization only.</summary>
    public class Membership
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;

        public OrgRole Role { get; set; } = OrgRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
