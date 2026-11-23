namespace SmartSupport.Api.Interfaces
{
    /// <summary>One earlier message in the conversation ("user" or "assistant").</summary>
    public record ChatTurn(string Role, string Content);

    /// <summary>A document an answer was based on, with the passage that matched.</summary>
    public record SourceRef(string FileName, string Snippet, float Score);

    /// <summary>The documents used for an answer, plus the answer itself as a token stream.</summary>
    public record ChatAnswer(IReadOnlyList<SourceRef> Sources, IAsyncEnumerable<string> Tokens);

    public interface IChatService
    {
        /// <summary>
        /// Answers a question using only the given organization's documents.
        /// <paramref name="history"/> holds the earlier messages of the same chat, oldest first, so follow-up questions work.
        /// </summary>
        Task<ChatAnswer> AskQuestionAsync(string query, Guid organizationId, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken = default);
    }
}
