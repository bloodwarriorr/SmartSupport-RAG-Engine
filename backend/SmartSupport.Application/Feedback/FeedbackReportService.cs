using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Chat;
using SmartSupport.Application.Organizations;

namespace SmartSupport.Application.Feedback;

// ---------- DTOs (JSON shapes the client relies on) ----------

public record FeedbackItemDto(
    int Id,
    int Feedback,
    string? Question,
    string Answer,
    IReadOnlyList<SourceRef> Sources,
    DateTime Timestamp,
    string SessionTitle,
    string User);

public record FeedbackReportDto(FeedbackCounts Summary, IReadOnlyList<FeedbackItemDto> Items);

public interface IFeedbackReportService
{
    /// <param name="rating">"negative" (default), "positive" or "all".</param>
    Task<FeedbackReportDto> GetReportAsync(string? rating, CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin only: how members rated the answers in this organization, so admins can see
/// which questions the documents don't answer well. Only rated answers are visible.
/// </summary>
public sealed class FeedbackReportService : IFeedbackReportService
{
    private const int MaxItems = 200;

    private readonly IOrganizationAccess _access;
    private readonly IFeedbackRepository _feedback;

    public FeedbackReportService(IOrganizationAccess access, IFeedbackRepository feedback)
    {
        _access = access;
        _feedback = feedback;
    }

    public async Task<FeedbackReportDto> GetReportAsync(string? rating, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireAdminAsync("Only organization admins can see feedback.", cancellationToken);
        var orgId = membership.OrganizationId;

        int? ratingValue = rating?.ToLowerInvariant() switch
        {
            "positive" => 1,
            "all" => null,
            _ => -1
        };

        var counts = await _feedback.CountAsync(orgId, cancellationToken);
        var rows = await _feedback.ListRatedAsync(orgId, ratingValue, MaxItems, cancellationToken);

        var items = rows.Select(r => new FeedbackItemDto(
            r.Id,
            r.Feedback,
            r.Question,
            r.Answer,
            ChatService.ParseSources(r.SourcesJson),
            r.Timestamp,
            r.SessionTitle,
            string.IsNullOrWhiteSpace(r.UserName) ? r.UserEmail : r.UserName)).ToList();

        return new FeedbackReportDto(counts, items);
    }
}
