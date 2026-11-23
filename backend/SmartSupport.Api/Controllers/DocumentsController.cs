using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartSupport.Api.Extensions;
using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Common.Exceptions;
using SmartSupport.Application.Documents;

namespace SmartSupport.Api.Controllers;

/// <summary>The organization's knowledge base. Everyone can list; only admins can upload or delete.</summary>
[Authorize]
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents)
    {
        _documents = documents;
    }

    [HttpGet]
    public Task<IReadOnlyList<DocumentSummary>> List(CancellationToken cancellationToken) =>
        _documents.ListAsync(cancellationToken);

    /// <summary>Uploads a PDF, TXT or Markdown file (max 10 MB) and makes it searchable.</summary>
    [HttpPost]
    [HttpPost("/api/Ingestion/upload")] // Old route, kept so already-deployed clients keep working.
    [EnableRateLimiting(ApiServiceExtensions.UploadPolicy)]
    [RequestSizeLimit(DocumentRules.MaxUploadBytes + 64 * 1024)]
    public async Task<ActionResult<DocumentSummary>> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null)
            throw new ValidationException("No file uploaded.");

        await using var stream = file.OpenReadStream();
        var document = await _documents.UploadAsync(new UploadDocumentCommand(file.FileName, file.Length, stream), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, document);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _documents.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
