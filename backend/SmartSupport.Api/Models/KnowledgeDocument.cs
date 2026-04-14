namespace SmartSupport.Api.Models
{
    public class KnowledgeDocument
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<DocumentChunk> Chunks { get; set; } = new();
    }
}
