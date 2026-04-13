using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SmartSupport.Api.Interfaces;

namespace SmartSupport.Api.Services;

public class SearchService : ISearchService
{
    private readonly QdrantClient _qdrantClient;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public SearchService(ITextEmbeddingGenerationService embeddingService)
    {
        _embeddingService = embeddingService;
        _qdrantClient = new QdrantClient("localhost", 6334);
    }

    public async Task<List<SearchResult>> SearchRelevantContentAsync(string query, int limit = 3)
    {
        // Turn the query into an embedding vector
        var queryEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(new List<string> { query });
        var queryVector = queryEmbeddings[0].ToArray();

        // Search the Qdrant collection for similar vectors
        var searchResults = await _qdrantClient.SearchAsync(
            collectionName: "knowledge_base",
            vector: queryVector,
            limit: (ulong)limit
        );

        // Map the search results to our SearchResult model
        return searchResults.Select(hit => new SearchResult(
            Content: hit.Payload["content"].ToString(),
            FileName: hit.Payload["file_name"].ToString(),
            Score: hit.Score // Assuming the score is a float, you may need to convert it if it's not already in the correct format
        )).ToList();
    }

}

public record SearchResult(string Content, string FileName, float Score);