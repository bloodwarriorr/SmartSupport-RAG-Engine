namespace SmartSupport.Api.Interfaces
{
    public interface IIngestionService
    {
        Task ProcessDocumentAsync(string fileName, string rawText);
    }
}
