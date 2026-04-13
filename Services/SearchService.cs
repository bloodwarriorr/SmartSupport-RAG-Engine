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
        // 1. הפיכת השאלה של המשתמש לוקטור
        var queryEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(new List<string> { query });
        var queryVector = queryEmbeddings[0].ToArray();

        // 2. חיפוש ב-Qdrant
        var searchResults = await _qdrantClient.SearchAsync(
            collectionName: "knowledge_base",
            vector: queryVector,
            limit: (ulong)limit
        );

        // 3. עיבוד התוצאות
        return searchResults.Select(hit => new SearchResult(
            Content: hit.Payload["content"].ToString(),
            FileName: hit.Payload["file_name"].ToString(),
            Score: hit.Score // רמת הביטחון של ה-AI בתוצאה (0 עד 1)
        )).ToList();
    }

}

public record SearchResult(string Content, string FileName, float Score);