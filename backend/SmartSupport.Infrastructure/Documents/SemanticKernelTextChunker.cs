using SmartSupport.Application.Abstractions.AI;
using Microsoft.SemanticKernel.Text;

namespace SmartSupport.Infrastructure.Documents;

/// <summary>Splits text into lines of up to 500 tokens, then groups them into paragraphs of up to 1000 tokens.</summary>
internal sealed class SemanticKernelTextChunker : ITextChunker
{
    private const int MaxTokensPerLine = 500;
    private const int MaxTokensPerParagraph = 1000;

    public IReadOnlyList<string> Split(string text)
    {
#pragma warning disable SKEXP0050 // TextChunker is marked experimental by Semantic Kernel.
        var lines = TextChunker.SplitPlainTextLines(text, MaxTokensPerLine);
        return TextChunker.SplitPlainTextParagraphs(lines, MaxTokensPerParagraph)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
#pragma warning restore SKEXP0050
    }
}
