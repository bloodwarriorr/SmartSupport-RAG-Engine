using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SmartSupport.Api.Interfaces;

namespace SmartSupport.Api.Services;

public class SearchService : ISearchService
{
    private readonly QdrantClient _qdrantClient;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IOllamaChatService _ollamaChatService;

    public SearchService(ITextEmbeddingGenerationService embeddingService, IOllamaChatService ollamaChatService)
    {
        _embeddingService = embeddingService;
        _qdrantClient = new QdrantClient("127.0.0.1", 6334);
        _ollamaChatService = ollamaChatService;
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
            Content: hit.Payload.ContainsKey("content") ? hit.Payload["content"].ToString() : "No Content",
            FileName: hit.Payload.ContainsKey("file_name") ? hit.Payload["file_name"].ToString() : "Unknown",
            Score: hit.Score // Assuming the score is a float, you may need to convert it if it's not already in the correct format
        )).ToList();
    }
   

}

public record SearchResult(string Content, string FileName, float Score);