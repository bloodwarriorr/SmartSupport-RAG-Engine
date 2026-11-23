namespace SmartSupport.Domain.Entities;

/// <summary>One conversation in the sidebar. Belongs to a single user.</summary>
public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Null only for chats created before organizations existed.
    public Guid? OrganizationId { get; set; }

    public string Title { get; set; } = "New Chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
}
