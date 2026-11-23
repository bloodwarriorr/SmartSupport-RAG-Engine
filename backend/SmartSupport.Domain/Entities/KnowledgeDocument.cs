namespace SmartSupport.Domain.Entities;

/// <summary>An uploaded file in an organization's knowledge base.</summary>
public class KnowledgeDocument
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>File extension without the dot, e.g. "pdf".</summary>
    public string ContentType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Null only for documents uploaded before organizations existed.
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public List<DocumentChunk> Chunks { get; set; } = new();
}
