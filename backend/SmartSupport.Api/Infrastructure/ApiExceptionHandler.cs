using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartSupport.Application.Common.Exceptions;

namespace SmartSupport.Api.Infrastructure;

/// <summary>
/// Turns exceptions into JSON problem details. Expected business errors keep their (user-safe) message;
/// anything unexpected is logged and returned as a generic 500, so internals never leak to clients.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The client went away (closed the tab, navigated): nothing to respond to.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request aborted by the client: {Path}", httpContext.Request.Path);
            return true;
        }

        var (status, title, detail) = exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Message),
            UnauthenticatedException e => (StatusCodes.Status401Unauthorized, "Not signed in", e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, "Forbidden", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            BadHttpRequestException e => (e.StatusCode, "Bad request", "The request could not be processed."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "Something went wrong. Please try again.")
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled error on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        // The response may already be streaming (e.g. an answer failed halfway); headers can't change then.
        if (httpContext.Response.HasStarted)
            return true;

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        // The web client reads "message" for error toasts.
        problem.Extensions["message"] = detail;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
