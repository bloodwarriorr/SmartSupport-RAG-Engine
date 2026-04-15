using Microsoft.SemanticKernel.ChatCompletion;
using SmartSupport.Api.Interfaces;

namespace SmartSupport.Api.Services
{
    public class ChatService : IChatService
    {
        private readonly ISearchService _searchService;
        private readonly IChatCompletionService _chatCompletionService;
        public ChatService(ISearchService searchService, IChatCompletionService chatCompletionService)
        {
            _searchService = searchService;
            _chatCompletionService = chatCompletionService;
        }

        public async IAsyncEnumerable<string> AskQuestionAsync(string query)
        {
            
            var searchResults = await _searchService.SearchRelevantContentAsync(query);
            var contextText = string.Join("\n", searchResults.Select(r => r.Content));

            var chatHistory = new ChatHistory("You are a helpful assistant. Use the provided context to answer questions.");

            chatHistory.AddUserMessage($"Context:\n{contextText}\n\nQuestion: {query}");

            var prompt = $"Use this context: {contextText}\n\nQuestion: {query}\n\nAnswer:";

            var results = _chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory);

            await foreach (var chunk in results)
            {
                if (chunk.Content != null)
                {
                    yield return chunk.Content;
                }
            }
        }
    }
}
