namespace SmartSupport.Domain.Entities;

/// <summary>One section of a document. Its embedding lives in the vector store under <see cref="VectorId"/>.</summary>
public class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid KnowledgeDocumentId { get; set; }
    public KnowledgeDocument? KnowledgeDocument { get; set; }

    public string TextContent { get; set; } = string.Empty;
    public string VectorId { get; set; } = string.Empty;
}
