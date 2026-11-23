using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SmartSupport.Application.Abstractions.AI;
using static Qdrant.Client.Grpc.Conditions;

namespace SmartSupport.Infrastructure.VectorStore;

/// <summary>
/// Document-chunk vectors in Qdrant. Each point carries its organization id, and every search
/// filters on it, so one organization can never retrieve another's content.
/// Registered as a singleton (QdrantClient is thread-safe and holds the gRPC channel).
/// </summary>
internal sealed class QdrantVectorStore : IVectorStore
{
    private const string OrgIdField = "org_id";
    private const string DocumentIdField = "document_id";
    private const string FileNameField = "file_name";
    private const string ContentField = "content";

    private readonly QdrantClient _client;
    private readonly string _collection;
    private readonly ulong _dimensions;
    private readonly SemaphoreSlim _setupLock = new(1, 1);
    private volatile bool _ready;

    // The vector size comes from the embedding settings, so the collection always matches the embedder.
    public QdrantVectorStore(QdrantClient client, IOptions<QdrantOptions> qdrantOptions, IOptions<GeminiOptions> embeddingOptions)
    {
        _client = client;
        _collection = qdrantOptions.Value.CollectionName;
        _dimensions = (ulong)embeddingOptions.Value.Dimensions;
    }

    public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;
        await EnsureCollectionAsync(cancellationToken);

        var points = records.Select(r => new PointStruct
        {
            Id = r.Id,
            Vectors = r.Vector,
            Payload =
            {
                [OrgIdField] = r.OrganizationId.ToString(),
                [DocumentIdField] = r.DocumentId.ToString(),
                [FileNameField] = r.FileName,
                [ContentField] = r.Content
            }
        }).ToList();

        await _client.UpsertAsync(_collection, points, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(float[] vector, Guid organizationId, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var hits = await _client.SearchAsync(
            _collection,
            vector,
            filter: MatchKeyword(OrgIdField, organizationId.ToString()),
            limit: (ulong)limit,
            cancellationToken: cancellationToken);

        return hits.Select(h => new VectorSearchHit(
            h.Payload.TryGetValue(ContentField, out var content) ? content.StringValue : string.Empty,
            h.Payload.TryGetValue(FileNameField, out var fileName) ? fileName.StringValue : "Unknown",
            h.Score)).ToList();
    }

    public async Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return;
        await EnsureCollectionAsync(cancellationToken);
        await _client.DeleteAsync(_collection, ids.ToList(), cancellationToken: cancellationToken);
    }

    /// <summary>Creates the collection and the org_id index if needed. Runs once per app start.</summary>
    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_ready) return;

        await _setupLock.WaitAsync(cancellationToken);
        try
        {
            if (_ready) return;

            if (!await _client.CollectionExistsAsync(_collection, cancellationToken))
            {
                await _client.CreateCollectionAsync(_collection,
                    new VectorParams { Size = _dimensions, Distance = Distance.Cosine },
                    cancellationToken: cancellationToken);
            }

            // Needed to filter by organization efficiently (and required by Qdrant Cloud's strict mode).
            // Creating an index that already exists is a no-op.
            await _client.CreatePayloadIndexAsync(_collection, OrgIdField, PayloadSchemaType.Keyword, cancellationToken: cancellationToken);

            _ready = true;
        }
        finally
        {
            _setupLock.Release();
        }
    }
}
