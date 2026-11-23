namespace SmartSupport.Api.Interfaces
{
    public interface IChatService
    {
        /// <summary>Answers a question using only the given organization's documents.</summary>
        IAsyncEnumerable<string> AskQuestionAsync(string query, Guid organizationId);
    }
}
