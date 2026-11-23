using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSupport.Api.Data;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Models;
using SmartSupport.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace SmartSupport.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    // How many earlier messages of a chat the model sees (5 questions + 5 answers).
    private const int MemoryMessageCount = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static List<SourceRef> ParseSources(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<SourceRef>();
        try { return JsonSerializer.Deserialize<List<SourceRef>>(json, JsonOptions) ?? new List<SourceRef>(); }
        catch (JsonException) { return new List<SourceRef>(); }
    }

    private readonly ISearchService _searchService;
    private readonly IChatService _chatService;
    private readonly AppDbContext _context;
    private readonly OrgAccessService _orgAccess;

    public AIController(ISearchService searchService, IChatService chatService, AppDbContext context, OrgAccessService orgAccess)
    {
        _searchService = searchService;
        _chatService = chatService;
        _context = context;
        _orgAccess = orgAccess;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null) return StatusCode(StatusCodes.Status403Forbidden);

        var results = await _searchService.SearchRelevantContentAsync(query, membership.OrganizationId);
        return Ok(results);
    }

    [HttpGet("ask-stream")]
    public async IAsyncEnumerable<string> AskStream([FromQuery] string query, [FromQuery] Guid sessionId)
    {
        // Must belong to an organization to chat; answers only use that organization's documents.
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null || string.IsNullOrWhiteSpace(query))
        {
            Response.StatusCode = membership == null ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest;
            yield break;
        }

        var user = membership.User;

        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session == null)
        {
            session = new ChatSession
            {
                Id = sessionId,
                UserId = user.Id,
                OrganizationId = membership.OrganizationId,
                Title = query.Length > 30 ? query.Substring(0, 30) + "..." : query,
                CreatedAt = DateTime.UtcNow
            };
            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();
        }
        else if (session.UserId != user.Id)
        {
            // Someone else's chat: don't let the caller write into it.
            Response.StatusCode = StatusCodes.Status403Forbidden;
            yield break;
        }

        // Conversation memory: the last few messages of this chat, oldest first (loaded before saving the new question).
        var history = (await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ChatSessionId == sessionId)
                .OrderByDescending(m => m.Timestamp)
                .Take(MemoryMessageCount)
                .Select(m => new { m.Role, m.Content })
                .ToListAsync())
            .AsEnumerable()
            .Reverse()
            .Select(m => new ChatTurn(m.Role, m.Content.Length > 2000 ? m.Content[..2000] : m.Content))
            .ToList();

        var userMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "user",
            Content = query,
            UserEmail = user.Email
        };
        _context.ChatMessages.Add(userMessage);
        await _context.SaveChangesAsync();

        var answer = await _chatService.AskQuestionAsync(query, membership.OrganizationId, history, HttpContext.RequestAborted);

        var fullResponse = new System.Text.StringBuilder();
        await foreach (var token in answer.Tokens)
        {
            fullResponse.Append(token);
            yield return token;
        }

        var assistantMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = fullResponse.ToString(),
            UserEmail = user.Email,
            Sources = answer.Sources.Count > 0 ? JsonSerializer.Serialize(answer.Sources, JsonOptions) : null
        };
        _context.ChatMessages.Add(assistantMessage);
        await _context.SaveChangesAsync();
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetUserSessions()
    {
        var googleId = OrgAccessService.GetGoogleId(User);

        var sessions = await _context.ChatSessions
            .Where(s => s.User.GoogleId == googleId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.Title, s.CreatedAt })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSessionMessages(Guid sessionId)
    {
        // Only the chat's owner can read it.
        var googleId = OrgAccessService.GetGoogleId(User);
        var ownsSession = await _context.ChatSessions
            .AnyAsync(s => s.Id == sessionId && s.User.GoogleId == googleId);
        if (!ownsSession) return NotFound();

        var messages = await _context.ChatMessages
         .AsNoTracking()
         .Where(m => m.ChatSessionId == sessionId)
         .OrderBy(m => m.Timestamp)
         .Select(m => new {
             m.Id,
             m.Content,
             m.Role,
             m.Timestamp,
             m.ChatSessionId,
             m.Sources,
             m.Feedback
         })
         .ToListAsync();

        return Ok(messages.Select(m => new
        {
            m.Id,
            m.Content,
            m.Role,
            m.Timestamp,
            m.ChatSessionId,
            Sources = ParseSources(m.Sources),
            m.Feedback
        }));
    }

    public record RenameSessionRequest(string Title);
    public record FeedbackRequest(int Value);

    /// <summary>Renames one of the caller's chats.</summary>
    [HttpPatch("sessions/{sessionId}")]
    public async Task<IActionResult> RenameSession(Guid sessionId, [FromBody] RenameSessionRequest request)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0 || title.Length > 100)
            return BadRequest(new { message = "Title must be 1-100 characters." });

        var session = await FindOwnSessionAsync(sessionId);
        if (session == null) return NotFound();

        session.Title = title;
        await _context.SaveChangesAsync();
        return Ok(new { session.Id, session.Title, session.CreatedAt });
    }

    /// <summary>Deletes one of the caller's chats and its messages.</summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        var session = await FindOwnSessionAsync(sessionId);
        if (session == null) return NotFound();

        _context.ChatSessions.Remove(session); // messages are deleted by cascade
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Rates an answer: 1 = helpful, -1 = not helpful, 0 = clear the rating.</summary>
    [HttpPost("messages/{messageId:int}/feedback")]
    public async Task<IActionResult> SetFeedback(int messageId, [FromBody] FeedbackRequest request)
    {
        if (request.Value is not (-1 or 0 or 1))
            return BadRequest(new { message = "Value must be 1, -1 or 0." });

        var googleId = OrgAccessService.GetGoogleId(User);
        var message = await _context.ChatMessages
            .Where(m => m.Id == messageId && m.Role == "assistant")
            .Where(m => _context.ChatSessions.Any(s => s.Id == m.ChatSessionId && s.User.GoogleId == googleId))
            .FirstOrDefaultAsync();
        if (message == null) return NotFound();

        message.Feedback = request.Value == 0 ? null : request.Value;
        await _context.SaveChangesAsync();
        return Ok(new { message.Id, message.Feedback });
    }

    private Task<ChatSession?> FindOwnSessionAsync(Guid sessionId)
    {
        var googleId = OrgAccessService.GetGoogleId(User);
        return _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.User.GoogleId == googleId);
    }
}
