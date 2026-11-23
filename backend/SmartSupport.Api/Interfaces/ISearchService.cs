using SmartSupport.Api.Services;

namespace SmartSupport.Api.Interfaces
{
    public interface ISearchService
    {
        /// <summary>Finds the chunks most similar to the query, searching only the given organization's documents.</summary>
        Task<List<SearchResult>> SearchRelevantContentAsync(string query, Guid organizationId, int limit = 3);
    }
}
