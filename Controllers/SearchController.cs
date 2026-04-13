using Microsoft.AspNetCore.Mvc;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Services;

namespace SmartSupport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> AskQuestion([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query cannot be empty.");

        try
        {
            var results = await _searchService.SearchRelevantContentAsync(query);
            return Ok(results);
        }
        catch (Exception)
        {
            return StatusCode(500, $"Search failed.");
        }
    }
}