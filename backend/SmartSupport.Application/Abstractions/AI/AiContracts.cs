namespace SmartSupport.Application.Abstractions.AI;

// Contracts for the external AI and document services. Implemented in SmartSupport.Infrastructure
// (Gemini, Groq, Qdrant, PdfPig), so the business logic doesn't depend on any vendor.

/// <summary>Turns text into vectors for similarity search.</summary>
public interface IEmbeddingGenerator
{
    /// <summary>Length of every vector this generator returns.</summary>
    int Dimensions { get; }

    /// <summary>One vector per input text, in the same order.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}

public enum PromptRole { System, User, Assistant }

public record PromptMessage(PromptRole Role, string Content);

/// <summary>The large language model that writes answers.</summary>
public interface IChatModel
{
    /// <summary>Streams the reply token by token.</summary>
    IAsyncEnumerable<string> StreamAsync(IReadOnlyList<PromptMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>Returns the whole reply at once.</summary>
    Task<string> CompleteAsync(IReadOnlyList<PromptMessage> messages, CancellationToken cancellationToken = default);
}

public record VectorRecord(Guid Id, float[] Vector, Guid OrganizationId, Guid DocumentId, string FileName, string Content);

public record VectorSearchHit(string Content, string FileName, float Score);

/// <summary>Stores document-chunk vectors. Every search is limited to one organization.</summary>
public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(float[] vector, Guid organizationId, int limit, CancellationToken cancellationToken = default);
    Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>Reads the plain text out of an uploaded file.</summary>
public interface IDocumentTextExtractor
{
    /// <param name="extension">Lower-case extension including the dot, e.g. ".pdf".</param>
    Task<string> ExtractTextAsync(Stream content, string extension, CancellationToken cancellationToken = default);
}

/// <summary>Splits long text into sections small enough to embed and to quote as context.</summary>
public interface ITextChunker
{
    IReadOnlyList<string> Split(string text);
}
