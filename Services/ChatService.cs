using SmartSupport.Api.Interfaces;

namespace SmartSupport.Api.Services
{
    public class ChatService : IChatService
    {
        private readonly ISearchService _searchService;
        private readonly IOllamaChatService _ollamaChatService;

        public ChatService(ISearchService searchService, IOllamaChatService ollamaChatService)
        {
            _searchService = searchService;
            _ollamaChatService = ollamaChatService;
        }

        public async IAsyncEnumerable<string> AskQuestionAsync(string query)
        {
            // 1. חיפוש ב-Qdrant
            var searchResults = await _searchService.SearchRelevantContentAsync(query);

            // 2. בניית הקונטקסט
            var contextText = string.Join("\n", searchResults.Select(r => r.Content));
            var prompt = $"Use this context: {contextText}\n\nQuestion: {query}\n\nAnswer:";

            // 3. קריאה למתודת ה-STREAM ב-Ollama
            // שים לב: כאן חייבת להיות מתודה שמחזירה IAsyncEnumerable
            await foreach (var chunk in _ollamaChatService.GenerateResponseStreamAsync(prompt))
            {
                yield return chunk;
            }
        }
    }
}
