using System.Text;
using Microsoft.Extensions.Logging;
using SmartSupport.Application.Abstractions.AI;
using SmartSupport.Application.Common.Exceptions;
using UglyToad.PdfPig;

namespace SmartSupport.Infrastructure.Documents;

/// <summary>Reads text from PDF (PdfPig) and plain-text / Markdown files.</summary>
internal sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    private readonly ILogger<DocumentTextExtractor> _logger;

    public DocumentTextExtractor(ILogger<DocumentTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        return extension switch
        {
            ".txt" or ".md" => await ReadTextAsync(content, cancellationToken),
            ".pdf" => await ReadPdfAsync(content, cancellationToken),
            _ => throw new ValidationException("Unsupported file type. Please upload a .pdf, .txt or .md file.")
        };
    }

    private static async Task<string> ReadTextAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private async Task<string> ReadPdfAsync(Stream content, CancellationToken cancellationToken)
    {
        // PdfPig needs a seekable stream; uploads are capped at 10 MB, so buffering in memory is fine.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        // Check the file really is a PDF, not just named ".pdf".
        if (bytes.Length < PdfSignature.Length || !bytes.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature))
            throw new ValidationException("This file isn't a valid PDF.");

        try
        {
            using var document = PdfDocument.Open(bytes);
            var text = new StringBuilder();
            foreach (var page in document.GetPages())
                text.Append(page.Text).Append(' ');
            return text.ToString();
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            _logger.LogWarning(ex, "Could not read an uploaded PDF.");
            throw new ValidationException("Couldn't read this PDF. It may be damaged or password-protected.");
        }
    }
}
