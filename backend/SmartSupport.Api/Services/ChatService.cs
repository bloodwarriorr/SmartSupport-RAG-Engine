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
            
            var searchResults = await _searchService.SearchRelevantContentAsync(query);

            
            var contextText = string.Join("\n", searchResults.Select(r => r.Content));
            var prompt = $"Use this context: {contextText}\n\nQuestion: {query}\n\nAnswer:";

            
            await foreach (var chunk in _ollamaChatService.GenerateResponseStreamAsync(prompt))
            {
                yield return chunk;
            }
        }
    }
}
