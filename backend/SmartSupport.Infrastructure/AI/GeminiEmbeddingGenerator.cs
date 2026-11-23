using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartSupport.Application.Abstractions.AI;

namespace SmartSupport.Infrastructure.AI;

/// <summary>Embeddings from Google's Gemini API (free tier available). Uses a typed HttpClient from IHttpClientFactory.</summary>
internal sealed class GeminiEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiEmbeddingGenerator> _logger;

    public GeminiEmbeddingGenerator(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiEmbeddingGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public int Dimensions => _options.Dimensions;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new List<float[]>(texts.Count);

        foreach (var batch in texts.Chunk(_options.MaxBatchSize))
        {
            var request = new
            {
                requests = batch.Select(text => new
                {
                    model = $"models/{_options.Model}",
                    content = new { parts = new[] { new { text } } },
                    outputDimensionality = _options.Dimensions
                })
            };

            using var response = await _httpClient.PostAsJsonAsync($"models/{_options.Model}:batchEmbedContents", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Log the provider's details, but never put them in the exception message users might see.
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini embedding request failed: {Status} {Body}", (int)response.StatusCode, body);
                throw new HttpRequestException($"Embedding service returned {(int)response.StatusCode}.", null, response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<BatchEmbedResponse>(cancellationToken: cancellationToken);
            if (result?.Embeddings == null || result.Embeddings.Count != batch.Length)
                throw new InvalidOperationException("Embedding service returned an unexpected response.");

            // Gemini only normalizes full-size (3072) vectors, so normalize reduced ones ourselves.
            results.AddRange(result.Embeddings.Select(e => Normalize(e.Values)));
        }

        return results;
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return norm == 0 ? vector : vector.Select(v => v / norm).ToArray();
    }

    private sealed class BatchEmbedResponse
    {
        [JsonPropertyName("embeddings")] public List<EmbeddingValues>? Embeddings { get; set; }
    }

    private sealed class EmbeddingValues
    {
        [JsonPropertyName("values")] public float[] Values { get; set; } = Array.Empty<float>();
    }
}
