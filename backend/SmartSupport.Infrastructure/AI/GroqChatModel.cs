using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartSupport.Application.Abstractions.AI;

namespace SmartSupport.Infrastructure.AI;

/// <summary>
/// The answering model, served by Groq through its OpenAI-compatible API.
/// Wraps Semantic Kernel so the rest of the app only sees <see cref="IChatModel"/>.
/// </summary>
internal sealed class GroqChatModel : IChatModel
{
    private readonly IChatCompletionService _completion;

    public GroqChatModel(IChatCompletionService completion)
    {
        _completion = completion;
    }

    public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<PromptMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in _completion.GetStreamingChatMessageContentsAsync(ToChatHistory(messages), cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
                yield return chunk.Content;
        }
    }

    public async Task<string> CompleteAsync(IReadOnlyList<PromptMessage> messages, CancellationToken cancellationToken = default)
    {
        var result = await _completion.GetChatMessageContentAsync(ToChatHistory(messages), cancellationToken: cancellationToken);
        return result.Content ?? string.Empty;
    }

    private static ChatHistory ToChatHistory(IReadOnlyList<PromptMessage> messages)
    {
        var history = new ChatHistory();
        foreach (var message in messages)
        {
            switch (message.Role)
            {
                case PromptRole.System: history.AddSystemMessage(message.Content); break;
                case PromptRole.Assistant: history.AddAssistantMessage(message.Content); break;
                default: history.AddUserMessage(message.Content); break;
            }
        }
        return history;
    }
}
