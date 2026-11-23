using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace SmartSupport.Api.Services;

public static class QdrantCollections
{
    // Gemini vectors (768 dims). The old "knowledge_base" collection holds 384-dim HuggingFace vectors.
    public const string KnowledgeBase = "knowledge_base_gemini";

    /// <summary>Payload field holding the owning organization's id. Every search filters on it.</summary>
    public const string OrgIdField = "org_id";

    private static volatile bool _ensured;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Creates the collection and the org_id payload index if they don't exist yet.
    /// Runs once per app start; later calls return immediately.
    /// </summary>
    public static async Task EnsureKnowledgeBaseAsync(QdrantClient client)
    {
        if (_ensured) return;

        await _lock.WaitAsync();
        try
        {
            if (_ensured) return;

            if (!await client.CollectionExistsAsync(KnowledgeBase))
            {
                await client.CreateCollectionAsync(KnowledgeBase, new VectorParams
                {
                    Size = GeminiEmbeddingService.Dimensions,
                    Distance = Distance.Cosine
                });
            }

            // Needed to filter by organization efficiently (and required by Qdrant Cloud's strict mode).
            // Creating an index that already exists is a no-op.
            await client.CreatePayloadIndexAsync(KnowledgeBase, OrgIdField, PayloadSchemaType.Keyword);

            _ensured = true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
