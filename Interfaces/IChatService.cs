namespace SmartSupport.Api.Interfaces
{
    public interface IChatService
    {
        IAsyncEnumerable<string> AskQuestionAsync(string query);
    }
}
