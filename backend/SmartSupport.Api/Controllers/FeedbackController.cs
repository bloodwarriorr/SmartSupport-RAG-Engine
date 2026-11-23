using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartSupport.Api.Data;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Models;
using SmartSupport.Api.Services;

namespace SmartSupport.Api.Controllers;

/// <summary>
/// Admin only: how members rated the answers in this organization, so admins can see
/// which questions the documents don't answer well.
/// </summary>
[Authorize]
[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private const int MaxItems = 200;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly OrgAccessService _orgAccess;

    public FeedbackController(AppDbContext db, OrgAccessService orgAccess)
    {
        _db = db;
        _orgAccess = orgAccess;
    }

    /// <param name="rating">"negative" (default), "positive" or "all".</param>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string rating = "negative")
    {
        var membership = await _orgAccess.GetMembershipAsync(User);
        if (membership == null || membership.Role != OrgRole.Admin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only organization admins can see feedback." });

        var orgId = membership.OrganizationId;

        // Rated answers in chats that belong to this organization.
        var rated = from m in _db.ChatMessages.AsNoTracking()
                    join s in _db.ChatSessions on m.ChatSessionId equals s.Id
                    where s.OrganizationId == orgId && m.Role == "assistant" && m.Feedback != null
                    select new { Message = m, Session = s };

        var summary = new
        {
            Positive = await rated.CountAsync(x => x.Message.Feedback == 1),
            Negative = await rated.CountAsync(x => x.Message.Feedback == -1)
        };

        var filtered = rating switch
        {
            "positive" => rated.Where(x => x.Message.Feedback == 1),
            "all" => rated,
            _ => rated.Where(x => x.Message.Feedback == -1)
        };

        var rows = await filtered
            .OrderByDescending(x => x.Message.Timestamp)
            .Take(MaxItems)
            .Select(x => new
            {
                x.Message.Id,
                x.Message.Feedback,
                Answer = x.Message.Content,
                x.Message.Sources,
                x.Message.Timestamp,
                SessionTitle = x.Session.Title,
                UserName = x.Session.User.Name,
                UserEmail = x.Session.User.Email,
                // The question this answer replied to: the latest user message before it in the same chat.
                Question = _db.ChatMessages
                    .Where(q => q.ChatSessionId == x.Message.ChatSessionId && q.Role == "user" && q.Timestamp <= x.Message.Timestamp)
                    .OrderByDescending(q => q.Timestamp)
                    .Select(q => q.Content)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var items = rows.Select(r => new
        {
            r.Id,
            r.Feedback,
            r.Question,
            r.Answer,
            Sources = ParseSources(r.Sources),
            r.Timestamp,
            r.SessionTitle,
            User = string.IsNullOrWhiteSpace(r.UserName) ? r.UserEmail : r.UserName
        });

        return Ok(new { summary, items });
    }

    private static List<SourceRef> ParseSources(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new List<SourceRef>();
        try { return JsonSerializer.Deserialize<List<SourceRef>>(json, JsonOptions) ?? new List<SourceRef>(); }
        catch (JsonException) { return new List<SourceRef>(); }
    }
}
