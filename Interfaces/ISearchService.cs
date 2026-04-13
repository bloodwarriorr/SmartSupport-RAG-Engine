

using SmartSupport.Api.Services;

namespace SmartSupport.Api.Interfaces
{
    public interface ISearchService
    {
        Task<List<SearchResult>> SearchRelevantContentAsync(string query, int limit = 3);
    }
}
