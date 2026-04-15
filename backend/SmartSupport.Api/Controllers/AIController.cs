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
    public AIController(ISearchService searchService, IChatService chatService, AppDbContext context)
    {
        _searchService = searchService;
        _chatService = chatService;
        _context = context;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var results = await _searchService.SearchRelevantContentAsync(query);
        return Ok(results);
    }
    [HttpGet("ask-stream")]
    public async IAsyncEnumerable<string> AskStream([FromQuery] string query, [FromQuery] Guid sessionId)
    {
        
        var googleId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
        if (user == null)
        {
            user = new User { GoogleId = googleId!, Email = email! };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session == null)
        {
            session = new ChatSession
            {
                Id = sessionId, // ה-GUID שהגיע מהאנגולר
                UserId = user.Id,
                Title = query.Length > 30 ? query.Substring(0, 30) + "..." : query, // כותרת זמנית מהשאלה
                CreatedAt = DateTime.UtcNow
            };
            _context.ChatSessions.Add(session);
            // אנחנו שומרים כאן כדי שההודעה הבאה תוכל להתייחס לסשן הזה ב-DB
            await _context.SaveChangesAsync();
        }
        var userMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "user",
            Content = query,
            UserEmail = email!
        };
        _context.ChatMessages.Add(userMessage);
        await _context.SaveChangesAsync();

        
        var fullResponse = new System.Text.StringBuilder();
        await foreach (var token in _chatService.AskQuestionAsync(query))
        {
            fullResponse.Append(token);
            yield return token;
        }

        var assistantMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = fullResponse.ToString(),
            UserEmail = email!

        };
        _context.ChatMessages.Add(assistantMessage);
        await _context.SaveChangesAsync();
    }
    [HttpGet("sessions")]
    public async Task<IActionResult> GetUserSessions()
    {
        var googleId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var sessions = await _context.ChatSessions
            .Where(s => s.User.GoogleId == googleId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSessionMessages(Guid sessionId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Ok(messages);
    }
}