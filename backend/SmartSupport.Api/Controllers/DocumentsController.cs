using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qdrant.Client;
using SmartSupport.Api.Data;
using SmartSupport.Api.Models;
using SmartSupport.Api.Services;

namespace SmartSupport.Api.Controllers;

/// <summary>
/// The organization's knowledge base. Everyone in the org can list documents; only admins can delete.
/// (Uploads go through IngestionController.)
/// </summary>
[Authorize]
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OrgAccessService _orgAccess;
    private readonly QdrantClient _qdrant;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(AppDbContext db, OrgAccessService orgAccess, QdrantClient qdrant, ILogger<DocumentsController> logger)
    {
        _db = db;
        _orgAccess = orgAccess;
        _qdrant = qdrant;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null) return StatusCode(StatusCodes.Status403Forbidden);

        var documents = await _db.Documents
            .AsNoTracking()
            .Where(d => d.OrganizationId == membership.OrganizationId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                d.ContentType,
                d.CreatedAt,
                ChunkCount = d.Chunks.Count
            })
            .ToListAsync();

        return Ok(documents);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null || membership.Role != OrgRole.Admin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only organization admins can delete documents." });

        var document = await _db.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == membership.OrganizationId);
        if (document == null) return NotFound(new { message = "Document not found." });

        // Remove the vectors first, so a deleted document can never show up in answers again.
        var pointIds = document.Chunks
            .Select(c => Guid.TryParse(c.VectorId, out var g) ? g : c.Id)
            .ToList();
        if (pointIds.Count > 0)
        {
            try
            {
                await _qdrant.DeleteAsync(QdrantCollections.KnowledgeBase, pointIds);
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Qdrant collection missing while deleting document {Id}; continuing.", id);
            }
        }

        _db.Documents.Remove(document); // chunks are deleted by cascade
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
