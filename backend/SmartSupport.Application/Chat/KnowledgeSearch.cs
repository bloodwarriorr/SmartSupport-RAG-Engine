using SmartSupport.Application.Abstractions.AI;

namespace SmartSupport.Application.Chat;

public record SearchResult(string Content, string FileName, float Score);

/// <summary>Finds the document passages most similar to a question, within one organization.</summary>
public interface IKnowledgeSearch
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, Guid organizationId, int limit, CancellationToken cancellationToken = default);
}

public sealed class KnowledgeSearch : IKnowledgeSearch
{
    private readonly IEmbeddingGenerator _embeddings;
    private readonly IVectorStore _vectorStore;

    public KnowledgeSearch(IEmbeddingGenerator embeddings, IVectorStore vectorStore)
    {
        _embeddings = embeddings;
        _vectorStore = vectorStore;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, Guid organizationId, int limit, CancellationToken cancellationToken = default)
    {
        var vectors = await _embeddings.EmbedAsync(new[] { query }, cancellationToken);
        var hits = await _vectorStore.SearchAsync(vectors[0], organizationId, limit, cancellationToken);
        return hits.Select(h => new SearchResult(h.Content, h.FileName, h.Score)).ToList();
    }
}
