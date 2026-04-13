using Microsoft.AspNetCore.Mvc;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Services;
using System.Diagnostics.CodeAnalysis; 

namespace SmartSupport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]

[Experimental("SKEXP0050")]
public class IngestionController : ControllerBase
{
    private readonly IIngestionService _ingestionService;

    public IngestionController(IIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [HttpPost("upload-text")]
    public async Task<IActionResult> UploadText([FromBody] TextUploadRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Invalid request: Text content is missing.");
        }

        try
        {
            await _ingestionService.ProcessDocumentAsync(request.FileName ?? "unknown.txt", request.Text);

            return Ok(new
            {
                status = "Success",
                message = "Document indexed successfully."
            });
        }
        catch (Exception ex)
        {
            
            if (ex.Message.Contains("429") || ex.Message.Contains("quota"))
            {
                return StatusCode(429, new
                {
                    error = "OpenAI Quota Exceeded",
                    details = "Please check your OpenAI billing balance."
                });
            }

            
            return StatusCode(500, new
            {
                error = "Processing Error",
                details = "General Error"
            });
        }
    }
}

public record TextUploadRequest(string FileName, string Text);