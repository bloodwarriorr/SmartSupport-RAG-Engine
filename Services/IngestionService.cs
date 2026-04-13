using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SmartSupport.Api.Data;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Models;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace SmartSupport.Api.Services;

public class IngestionService : IIngestionService
{
    private readonly AppDbContext _dbContext;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly QdrantClient _qdrantClient;
    public IngestionService(AppDbContext dbContext, ITextEmbeddingGenerationService embeddingService, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        var host = configuration["Qdrant:Host"] ?? "127.0.0.1";
        var port = int.Parse(configuration["Qdrant:Port"] ?? "6334");

        _qdrantClient = new QdrantClient(host, port);
    }

    [Experimental("SKEXP0050")]
    public async Task ProcessDocumentAsync(string fileName, string rawText)
    {
        try
        {
            // וודא שהאוסף קיים לפני שמתחילים
            await EnsureCollectionExistsAsync();

            var document = new KnowledgeDocument
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                CreatedAt = DateTime.UtcNow
            };

#pragma warning disable SKEXP0050
            var lines = TextChunker.SplitPlainTextLines(rawText, 500);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 1000);
#pragma warning restore SKEXP0050

            foreach (var paragraph in paragraphs)
            {
                // 1. יצירת Embedding (דורש קרדיט ב-OpenAI)
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(new List<string> { paragraph });
                var vector = embeddings[0].ToArray();

                var chunkId = Guid.NewGuid();

                // 2. שמירה ב-Qdrant
                await _qdrantClient.UpsertAsync("knowledge_base", new[]
                {
                    new PointStruct
                    {
                        Id = chunkId,
                        Vectors = vector,
                        Payload =
                        {
                            ["file_name"] = fileName,
                            ["content"] = paragraph
                        }
                    }
                });

                // 3. שמירה ב-SQL
                document.Chunks.Add(new DocumentChunk
                {
                    Id = chunkId,
                    TextContent = paragraph,
                    VectorId = chunkId.ToString()
                });
            }

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error during ingestion: {ex.Message}", ex);
        }
    }
    private async Task EnsureCollectionExistsAsync()
    {
        var response = await _qdrantClient.ListCollectionsAsync();
        if (!response.Contains("knowledge_base"))
        {
            await _qdrantClient.CreateCollectionAsync("knowledge_base",
                new VectorParams
                {
                    Size = 768, 
                    Distance = Distance.Cosine
                });
        }
    }
}