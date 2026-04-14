namespace SmartSupport.Api.Interfaces
{
    public interface IOllamaChatService
    {
        
        Task<string> GenerateResponseAsync(string prompt);

        
        IAsyncEnumerable<string> GenerateResponseStreamAsync(string prompt);
    }
}
