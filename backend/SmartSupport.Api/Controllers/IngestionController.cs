using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Services;
using System.Diagnostics.CodeAnalysis; 

namespace SmartSupport.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]

[Experimental("SKEXP0050")]
public class IngestionController : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024; // 10 MB

    private readonly IIngestionService _ingestionService;
    private readonly OrgAccessService _orgAccess;

    public IngestionController(IIngestionService ingestionService, OrgAccessService orgAccess)
    {
        _ingestionService = ingestionService;
        _orgAccess = orgAccess;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        // Only organization admins can add documents.
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null)
            return StatusCode(StatusCodes.Status403Forbidden, "Join or create an organization first.");
        if (membership.Role != Models.OrgRole.Admin)
            return StatusCode(StatusCodes.Status403Forbidden, "Only organization admins can upload documents.");

        if (file != null && file.Length > MaxUploadBytes)
        {
            return BadRequest("File is too large. The limit is 10 MB.");
        }

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
                case ".md":
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    {
                        fileContent = await reader.ReadToEndAsync();
                    }
                    break;

                case ".pdf":
                    using (var document = UglyToad.PdfPig.PdfDocument.Open(file.OpenReadStream()))
                    {
                        var text = new System.Text.StringBuilder();
                        foreach (var page in document.GetPages())
                        {
                            text.Append(page.Text).Append(' ');
                        }
                        fileContent = text.ToString();
                    }
                    break;

                default:
                    return BadRequest("Unsupported file type. Please upload .txt, .pdf, or .md");
            }

            
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                return BadRequest("No text found in this file. Scanned PDFs (images of text) aren't supported yet.");
            }

            await _ingestionService.ProcessDocumentAsync(file.FileName, fileContent, membership.OrganizationId);

            return Ok(new { message = "Processed successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}

