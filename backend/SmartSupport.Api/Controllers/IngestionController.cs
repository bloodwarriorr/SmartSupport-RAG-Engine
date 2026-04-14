using Microsoft.AspNetCore.Authorization;
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

    [HttpPost("upload")] 
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        
        var allowedExtensions = new[] { ".txt", ".pdf", ".md" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest("Unsupported file type. Please upload .txt, .pdf, or .md");
        }
        string fileContent = "";
        try
        {
            switch (extension)
            {
                case ".txt":
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    {
                        fileContent = await reader.ReadToEndAsync();
                    }
                    break;

                case ".pdf":
                    using (var document = UglyToad.PdfPig.PdfDocument.Open(file.OpenReadStream()))
                    {
                        foreach (var page in document.GetPages())
                        {
                            fileContent += page.Text + " ";
                        }
                    }
                    break;

                default:
                    return BadRequest("Unsupported file type. Please upload .txt, .pdf, or .md");
            }

            
            await _ingestionService.ProcessDocumentAsync(file.FileName, fileContent);

            return Ok(new { message = "Processed successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}

