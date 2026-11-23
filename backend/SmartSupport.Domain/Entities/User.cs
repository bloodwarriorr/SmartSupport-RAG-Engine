namespace SmartSupport.Domain.Entities;

/// <summary>A person who signed in with Google.</summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Google's stable account id (the token's "sub" claim).</summary>
    public string GoogleId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ChatSession> ChatSessions { get; set; } = new();
}
