namespace SmartSupport.Api.Interfaces
{
    public interface IIngestionService
    {
        /// <summary>Splits, embeds and stores a document for the given organization.</summary>
        Task ProcessDocumentAsync(string fileName, string rawText, Guid organizationId);
    }
}
