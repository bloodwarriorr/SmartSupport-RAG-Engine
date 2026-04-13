using Microsoft.AspNetCore.Mvc;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Services;

namespace SmartSupport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly IChatService _chatService;

    public AIController(ISearchService searchService, IChatService chatService)
    {
        _searchService = searchService;
        _chatService = chatService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var results = await _searchService.SearchRelevantContentAsync(query);
        return Ok(results);
    }
    [HttpGet("ask-stream")]
    public IAsyncEnumerable<string> AskStream([FromQuery] string query)
    {
        
        return _chatService.AskQuestionAsync(query);


    }
}