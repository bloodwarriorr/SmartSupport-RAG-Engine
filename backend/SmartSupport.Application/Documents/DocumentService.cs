using Microsoft.Extensions.Logging;
using SmartSupport.Application.Abstractions.AI;
using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Common.Exceptions;
using SmartSupport.Application.Common.Interfaces;
using SmartSupport.Application.Organizations;
using SmartSupport.Domain.Entities;

namespace SmartSupport.Application.Documents;

/// <summary>An uploaded file as the API received it.</summary>
public record UploadDocumentCommand(string FileName, long Length, Stream Content);

/// <summary>Upload limits shared by the API (request size) and the service (validation).</summary>
public static class DocumentRules
{
    public const long MaxUploadBytes = 10 * 1024 * 1024; // 10 MB
    public const int MaxFileNameLength = 200;
    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".txt", ".md" };
}

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<DocumentSummary> UploadAsync(UploadDocumentCommand command, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The organization's knowledge base. Everyone in the org can list documents; only admins can upload or delete.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private readonly IOrganizationAccess _access;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingGenerator _embeddings;
    private readonly IVectorStore _vectorStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IOrganizationAccess access,
        IDocumentRepository documents,
        IDocumentTextExtractor textExtractor,
        ITextChunker chunker,
        IEmbeddingGenerator embeddings,
        IVectorStore vectorStore,
        IUnitOfWork unitOfWork,
        ILogger<DocumentService> logger)
    {
        _access = access;
        _documents = documents;
        _textExtractor = textExtractor;
        _chunker = chunker;
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(cancellationToken);
        return await _documents.ListAsync(membership.OrganizationId, cancellationToken);
    }

    public async Task<DocumentSummary> UploadAsync(UploadDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireAdminAsync("Only organization admins can upload documents.", cancellationToken);

        var fileName = SanitizeFileName(command.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!DocumentRules.AllowedExtensions.Contains(extension))
            throw new ValidationException("Unsupported file type. Please upload a .pdf, .txt or .md file.");
        if (command.Length <= 0)
            throw new ValidationException("The file is empty.");
        if (command.Length > DocumentRules.MaxUploadBytes)
            throw new ValidationException("File is too large. The limit is 10 MB.");

        var text = await _textExtractor.ExtractTextAsync(command.Content, extension, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            throw new ValidationException("No text found in this file. Scanned PDFs (images of text) aren't supported yet.");

        var chunks = _chunker.Split(text);
        if (chunks.Count == 0)
            throw new ValidationException("No text found in this file.");

        var document = new KnowledgeDocument
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = extension.TrimStart('.'),
            OrganizationId = membership.OrganizationId,
            CreatedAt = DateTime.UtcNow
        };

        var vectors = await _embeddings.EmbedAsync(chunks, cancellationToken);
        var records = new List<VectorRecord>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunkId = Guid.NewGuid();
            records.Add(new VectorRecord(chunkId, vectors[i], membership.OrganizationId, document.Id, fileName, chunks[i]));
            document.Chunks.Add(new DocumentChunk { Id = chunkId, TextContent = chunks[i], VectorId = chunkId.ToString() });
        }

        await _vectorStore.UpsertAsync(records, cancellationToken);
        try
        {
            _documents.Add(document);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Don't leave searchable vectors behind for a document that was never saved.
            await TryDeleteVectorsAsync(records.Select(r => r.Id).ToList());
            throw;
        }

        return new DocumentSummary(document.Id, document.FileName, document.ContentType, document.CreatedAt, document.Chunks.Count);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireAdminAsync("Only organization admins can delete documents.", cancellationToken);

        var document = await _documents.FindWithChunksAsync(documentId, membership.OrganizationId, cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        // Remove the vectors first, so a deleted document can never show up in answers again.
        var vectorIds = document.Chunks
            .Select(c => Guid.TryParse(c.VectorId, out var id) ? id : c.Id)
            .ToList();
        if (vectorIds.Count > 0)
            await _vectorStore.DeleteAsync(vectorIds, cancellationToken);

        _documents.Remove(document); // chunks are deleted by cascade
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Keeps only the file name (no folders from the client) and limits its length.</summary>
    private static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new ValidationException("The file has no name.");
        if (name.Length > DocumentRules.MaxFileNameLength)
        {
            var extension = Path.GetExtension(name);
            name = name[..(DocumentRules.MaxFileNameLength - extension.Length)] + extension;
        }
        return name;
    }

    private async Task TryDeleteVectorsAsync(IReadOnlyList<Guid> ids)
    {
        try
        {
            await _vectorStore.DeleteAsync(ids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove {Count} orphaned vectors after a failed upload.", ids.Count);
        }
    }
}
