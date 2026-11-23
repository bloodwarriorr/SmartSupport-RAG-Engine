using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SmartSupport.Api.Services;

/// <summary>
/// Generates embeddings with Google's Gemini embedding API (free tier available).
/// </summary>
public class GeminiEmbeddingService : ITextEmbeddingGenerationService
{
    public const int Dimensions = 768;
    private const int MaxBatchSize = 100; // Gemini batch limit per request

    private readonly HttpClient _httpClient;
    private readonly string _modelId;

    public GeminiEmbeddingService(string modelId, string apiKey)
    {
        _modelId = modelId;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/")
        };
        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ReadOnlyMemory<float>>(data.Count);

        foreach (var batch in data.Chunk(MaxBatchSize))
        {
            var requestBody = new
            {
                requests = batch.Select(text => new
                {
                    model = $"models/{_modelId}",
                    content = new { parts = new[] { new { text } } },
                    outputDimensionality = Dimensions
                })
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"models/{_modelId}:batchEmbedContents", requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Gemini Error: {response.StatusCode}. Details: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<BatchEmbedResponse>(cancellationToken: cancellationToken);
            if (result?.Embeddings == null)
                throw new Exception("Gemini Error: empty embedding response.");

            // Gemini only normalizes full-size (3072) vectors, so normalize reduced ones ourselves.
            results.AddRange(result.Embeddings.Select(e => new ReadOnlyMemory<float>(Normalize(e.Values))));
        }

        return results;
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm == 0) return vector;
        return vector.Select(v => v / norm).ToArray();
    }

    private sealed class BatchEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<EmbeddingValues>? Embeddings { get; set; }
    }

    private sealed class EmbeddingValues
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = Array.Empty<float>();
    }
}
