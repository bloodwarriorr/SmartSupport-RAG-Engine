namespace SmartSupport.Api.Services
{
    using SmartSupport.Api.Interfaces;
    using System.Net.Http.Json;
    using System.Text.Json;

    public class OllamaChatService : IOllamaChatService
    {
        private readonly HttpClient _httpClient;
        private const string ModelName = "llama3";

        public OllamaChatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:11434");
        }

        public async IAsyncEnumerable<string> GenerateResponseStreamAsync(string prompt)
        {
            var requestBody = new
            {
                model = ModelName,
                prompt = prompt,
                stream = true // קריטי עבור הזרמה!
            };

            // משתמשים ב-PostAsync כדי לקבל גישה ל-Stream של ה-Response
            var response = await _httpClient.PostAsJsonAsync("/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var chunk = JsonSerializer.Deserialize<OllamaStreamResponse>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (chunk?.Response != null)
                {
                    yield return chunk.Response;
                }

                if (chunk?.Done == true) break;
            }
        }
        public async Task<string> GenerateResponseAsync(string prompt)
        {
            var requestBody = new
            {
                model = ModelName,
                prompt = prompt,
                stream = false // כאן אנחנו מחכים לכל התשובה
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Response ?? string.Empty;
        }
        public record OllamaStreamResponse(string Response, bool Done);
        public record OllamaResponse(string Response);
    }
}
