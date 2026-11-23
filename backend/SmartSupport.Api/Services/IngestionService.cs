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
    public IngestionService(AppDbContext dbContext, ITextEmbeddingGenerationService embeddingService, QdrantClient qdrantClient)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantClient = qdrantClient;
    }

    [Experimental("SKEXP0050")]
    public async Task ProcessDocumentAsync(string fileName, string rawText, Guid organizationId)
    {
        try
        {
            await QdrantCollections.EnsureKnowledgeBaseAsync(_qdrantClient);

            var document = new KnowledgeDocument
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                ContentType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow
            };

#pragma warning disable SKEXP0050
            var lines = TextChunker.SplitPlainTextLines(rawText, 500);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 1000);
#pragma warning restore SKEXP0050

            if (paragraphs.Count == 0)
                throw new InvalidOperationException("The document has no readable text.");

            // Embed all chunks in batched calls instead of one request per chunk.
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(paragraphs);

            var points = new List<PointStruct>(paragraphs.Count);
            for (var i = 0; i < paragraphs.Count; i++)
            {
                var chunkId = Guid.NewGuid();
                points.Add(new PointStruct
                {
                    Id = chunkId,
                    Vectors = embeddings[i].ToArray(),
                    Payload =
                    {
                        [QdrantCollections.OrgIdField] = organizationId.ToString(),
                        ["document_id"] = document.Id.ToString(),
                        ["file_name"] = fileName,
                        ["content"] = paragraphs[i]
                    }
                });

                document.Chunks.Add(new DocumentChunk
                {
                    Id = chunkId,
                    TextContent = paragraphs[i],
                    VectorId = chunkId.ToString()
                });
            }

            await _qdrantClient.UpsertAsync(QdrantCollections.KnowledgeBase, points);

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error during ingestion: {ex.Message}", ex);
        }
    }
}