using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSupport.Api.Data;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Models;
using SmartSupport.Api.Services;
using Microsoft.EntityFrameworkCore;
namespace SmartSupport.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
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

        var userMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "user",
            Content = query,
            UserEmail = user.Email
        };
        _context.ChatMessages.Add(userMessage);
        await _context.SaveChangesAsync();

        var fullResponse = new System.Text.StringBuilder();
        await foreach (var token in _chatService.AskQuestionAsync(query, membership.OrganizationId))
        {
            fullResponse.Append(token);
            yield return token;
        }

        var assistantMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = fullResponse.ToString(),
            UserEmail = user.Email
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
             m.ChatSessionId
         })
         .ToListAsync();

        return Ok(messages);
    }
}
