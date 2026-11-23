using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartSupport.Api.Contracts;
using SmartSupport.Api.Extensions;
using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Chat;

namespace SmartSupport.Api.Controllers;

/// <summary>Asking questions and managing the caller's own chats.</summary>
[Authorize]
[ApiController]
[Route("api/ai")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;

    public ChatController(IChatService chat)
    {
        _chat = chat;
    }

    /// <summary>
    /// Asks a question in a chat (creating the chat on the first question).
    /// The answer streams back as a JSON array of text pieces.
    /// </summary>
    [HttpPost("ask")]
    [EnableRateLimiting(ApiServiceExtensions.AskPolicy)]
    public async Task<IActionResult> Ask(AskRequest request, CancellationToken cancellationToken)
    {
        var tokens = await _chat.AskAsync(new AskCommand(request.Query, request.SessionId), cancellationToken);
        return Ok(tokens);
    }

    /// <summary>Old GET version of <see cref="Ask"/> (question in the URL). Kept so deployed clients keep working.</summary>
    [HttpGet("ask-stream")]
    [EnableRateLimiting(ApiServiceExtensions.AskPolicy)]
    public async Task<IActionResult> AskStream([FromQuery] string query, [FromQuery] Guid sessionId, CancellationToken cancellationToken)
    {
        var tokens = await _chat.AskAsync(new AskCommand(query, sessionId), cancellationToken);
        return Ok(tokens);
    }

    [HttpGet("sessions")]
    public Task<IReadOnlyList<SessionSummary>> ListSessions(CancellationToken cancellationToken) =>
        _chat.ListSessionsAsync(cancellationToken);

    [HttpGet("sessions/{sessionId:guid}")]
    public Task<IReadOnlyList<MessageDto>> GetMessages(Guid sessionId, CancellationToken cancellationToken) =>
        _chat.GetMessagesAsync(sessionId, cancellationToken);

    [HttpPatch("sessions/{sessionId:guid}")]
    public Task<SessionSummary> RenameSession(Guid sessionId, RenameSessionRequest request, CancellationToken cancellationToken) =>
        _chat.RenameSessionAsync(sessionId, request.Title, cancellationToken);

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken cancellationToken)
    {
        await _chat.DeleteSessionAsync(sessionId, cancellationToken);
        return NoContent();
    }

    /// <summary>Rates an answer: 1 = helpful, -1 = not helpful, 0 = clear.</summary>
    [HttpPost("messages/{messageId:int}/feedback")]
    public Task<FeedbackResultDto> RateAnswer(int messageId, RateAnswerRequest request, CancellationToken cancellationToken) =>
        _chat.RateAnswerAsync(messageId, request.Value, cancellationToken);
}
