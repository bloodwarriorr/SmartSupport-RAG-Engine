using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Net.Http.Headers;
using System.Net.Http.Json;

public class HuggingFaceEmbeddingService : ITextEmbeddingGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;

    public HuggingFaceEmbeddingService(string modelId, string apiKey)
    {
        _modelId = modelId;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DotNetBackend");
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        
        var url = $"https://router.huggingface.co/hf-inference/models/{_modelId}/pipeline/feature-extraction";

        
        var requestBody = new
        {
            inputs = data,
            options = new { wait_for_model = true }
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"HuggingFace Error: {response.StatusCode}. Details: {error}");
        }

       
        var result = await response.Content.ReadFromJsonAsync<float[][]>();

        if (result == null) return new List<ReadOnlyMemory<float>>();

        
        return result.Select(v => new ReadOnlyMemory<float>(v)).ToList();
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();
}