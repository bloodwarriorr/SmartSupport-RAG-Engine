namespace SmartSupport.Domain.Entities;

/// <summary>A question ("user") or an answer ("assistant") inside a chat.</summary>
public class ChatMessage
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public int Id { get; set; }
    public Guid ChatSessionId { get; set; }

    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = UserRole;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Assistant messages only: JSON list of the documents the answer was based on.</summary>
    public string? Sources { get; set; }

    /// <summary>Assistant messages only: 1 (helpful), -1 (not helpful) or null (not rated).</summary>
    public int? Feedback { get; set; }
}
