using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartSupport.Api.Interfaces;

namespace SmartSupport.Api.Services
{
    public class ChatService : IChatService
    {
        private const int SearchLimit = 4;

        // Sources: only chunks that are clearly relevant, i.e. close to the best match.
        private const float MinSourceScore = 0.5f;
        private const float MaxGapFromBest = 0.08f;
        private const int SnippetLength = 280;

        private const string SystemPrompt = """
            You are SmartSupport, a support assistant for an organization.
            Answer ONLY using the information in the provided context, which comes from the organization's documents.
            If the context does not contain the answer, say that you couldn't find it in the documents. Do not guess or use outside knowledge.
            Use the earlier conversation to understand follow-up questions.
            Always reply in the language of the user's LATEST question, even if the context or earlier replies are in another language.
            Be clear and concise.
            """;

        private readonly ISearchService _searchService;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(ISearchService searchService, IChatCompletionService chatCompletionService, ILogger<ChatService> logger)
        {
            _searchService = searchService;
            _chatCompletionService = chatCompletionService;
            _logger = logger;
        }

        public async Task<ChatAnswer> AskQuestionAsync(string query, Guid organizationId, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken = default)
        {
            // A follow-up like "and what about Europe?" finds nothing on its own, so search with a standalone version.
            var searchQuery = history.Count > 0
                ? await RewriteAsStandaloneQuestionAsync(query, history, cancellationToken)
                : query;

            var results = await _searchService.SearchRelevantContentAsync(searchQuery, organizationId, SearchLimit);

            var contextText = results.Count == 0
                ? "(no relevant documents found)"
                : string.Join("\n\n", results.Select(r => $"[From: {r.FileName}]\n{r.Content}"));

            var chatHistory = new ChatHistory(SystemPrompt);
            foreach (var turn in history)
            {
                if (turn.Role == "user") chatHistory.AddUserMessage(turn.Content);
                else chatHistory.AddAssistantMessage(turn.Content);
            }
            // The language rule sits right next to the question: models follow the nearest instruction,
            // and otherwise drift toward the language of the documents or of earlier (possibly wrong) replies.
            chatHistory.AddUserMessage(
                $"Context:\n{contextText}\n\n" +
                $"Question: {query}\n\n" +
                "Reply in the same language as the question above (not the language of the context or of earlier replies).");

            return new ChatAnswer(PickSources(results), StreamAsync(chatHistory, cancellationToken));
        }

        private async IAsyncEnumerable<string> StreamAsync(ChatHistory chatHistory, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var chunk in _chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken))
            {
                if (chunk.Content != null)
                    yield return chunk.Content;
            }
        }

        /// <summary>Turns a follow-up into a question that makes sense on its own, using the conversation so far.</summary>
        private async Task<string> RewriteAsStandaloneQuestionAsync(string query, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken)
        {
            try
            {
                var transcript = new StringBuilder();
                foreach (var turn in history.TakeLast(6))
                    transcript.AppendLine($"{(turn.Role == "user" ? "User" : "Assistant")}: {Truncate(turn.Content, 600)}");

                var prompt = new ChatHistory("""
                    Rewrite the user's latest message as a standalone search question that can be understood without the conversation.
                    Keep the user's language. If it is already standalone, return it unchanged.
                    Reply with the question only, no explanation.
                    """);
                prompt.AddUserMessage($"Conversation:\n{transcript}\nLatest message: {query}");

                var result = await _chatCompletionService.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
                var rewritten = result.Content?.Trim().Trim('"');
                return string.IsNullOrWhiteSpace(rewritten) || rewritten.Length > 500 ? query : rewritten;
            }
            catch (Exception ex)
            {
                // Not worth failing the answer over; fall back to the original question.
                _logger.LogWarning(ex, "Could not rewrite follow-up question; using it as-is.");
                return query;
            }
        }

        /// <summary>One entry per document, keeping only results close to the best match.</summary>
        private static IReadOnlyList<SourceRef> PickSources(List<SearchResult> results)
        {
            if (results.Count == 0) return Array.Empty<SourceRef>();

            var best = results.Max(r => r.Score);
            if (best < MinSourceScore) return Array.Empty<SourceRef>();

            return results
                .Where(r => r.Score >= best - MaxGapFromBest)
                .GroupBy(r => r.FileName)
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .OrderByDescending(r => r.Score)
                .Select(r => new SourceRef(r.FileName, Truncate(CollapseWhitespace(r.Content), SnippetLength), r.Score))
                .ToList();
        }

        private static string CollapseWhitespace(string text) =>
            System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }
}
