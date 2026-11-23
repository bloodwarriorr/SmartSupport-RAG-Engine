using System.Security.Claims;
using SmartSupport.Application.Common.Interfaces;

namespace SmartSupport.Api.Infrastructure;

/// <summary>Reads the signed-in user from the validated Google token on the current request.</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public string? GoogleId => Principal?.Identity?.IsAuthenticated == true
        ? Principal.FindFirstValue(ClaimTypes.NameIdentifier)
        : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string? Name => Principal?.FindFirstValue("name");
}
