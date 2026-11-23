using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SmartSupport.Api.Interfaces;
using static Qdrant.Client.Grpc.Conditions;

namespace SmartSupport.Api.Services;

public class SearchService : ISearchService
{
    private readonly QdrantClient _qdrantClient;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public SearchService(ITextEmbeddingGenerationService embeddingService, QdrantClient qdrantClient)
    {
        _embeddingService = embeddingService;
        _qdrantClient = qdrantClient;
    }

    public async Task<List<SearchResult>> SearchRelevantContentAsync(string query, Guid organizationId, int limit = 3)
    {
        await QdrantCollections.EnsureKnowledgeBaseAsync(_qdrantClient);

        // Turn the query into an embedding vector
        var queryEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(new List<string> { query });
        var queryVector = queryEmbeddings[0].ToArray();

        // Search only this organization's documents
        var searchResults = await _qdrantClient.SearchAsync(
            collectionName: QdrantCollections.KnowledgeBase,
            vector: queryVector,
            filter: MatchKeyword(QdrantCollections.OrgIdField, organizationId.ToString()),
            limit: (ulong)limit
        );

        return searchResults.Select(hit => new SearchResult(
            Content: hit.Payload.TryGetValue("content", out var content) ? content.StringValue : "No Content",
            FileName: hit.Payload.TryGetValue("file_name", out var fileName) ? fileName.StringValue : "Unknown",
            Score: hit.Score
        )).ToList();
    }
}

public record SearchResult(string Content, string FileName, float Score);
