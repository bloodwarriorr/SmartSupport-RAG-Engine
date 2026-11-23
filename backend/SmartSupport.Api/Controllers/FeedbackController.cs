using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartSupport.Application.Feedback;

namespace SmartSupport.Api.Controllers;

/// <summary>Admin only: how members rated the answers in the organization.</summary>
[Authorize]
[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackReportService _feedback;

    public FeedbackController(IFeedbackReportService feedback)
    {
        _feedback = feedback;
    }

    /// <param name="rating">"negative" (default), "positive" or "all".</param>
    [HttpGet]
    public Task<FeedbackReportDto> Get([FromQuery] string? rating, CancellationToken cancellationToken) =>
        _feedback.GetReportAsync(rating, cancellationToken);
}
