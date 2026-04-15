using System.ComponentModel.DataAnnotations;

namespace SmartSupport.Api.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; } 

        [Required]
        public string GoogleId { get; set; } 

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string? Name { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        
        public List<ChatSession> ChatSessions { get; set; } = new();
    }
    public class ChatSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // קשר למשתמש
        public int UserId { get; set; } 
        public User User { get; set; }

        public string Title { get; set; } = "New Chat";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ChatMessage> Messages { get; set; } = new();
    }
    public class ChatMessage
    {
        public int Id { get; set; } 

        
        public Guid ChatSessionId { get; set; }

        public string UserEmail { get; set; }
        public string Role { get; set; }

        
        public string Content { get; set; }

        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
