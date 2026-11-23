namespace SmartSupport.Domain.Entities;

/// <summary>A team that shares a knowledge base. Members join with the invite code.</summary>
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Reusable join code, stored without the dash (e.g. "K7P2XQ9M"). Admins can regenerate it.</summary>
    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Membership> Memberships { get; set; } = new();
}
