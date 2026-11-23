namespace SmartSupport.Application.Common.Exceptions;

/// <summary>
/// Expected business failures. The API turns each type into an HTTP status code,
/// and its message is safe to show to the user.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

/// <summary>400: the request is invalid (bad input, unsupported file, ...).</summary>
public sealed class ValidationException(string message) : AppException(message);

/// <summary>401: no signed-in user.</summary>
public sealed class UnauthenticatedException(string message = "Please sign in.") : AppException(message);

/// <summary>403: signed in, but not allowed to do this.</summary>
public sealed class ForbiddenException(string message = "You don't have permission to do this.") : AppException(message);

/// <summary>404: doesn't exist, or the caller isn't allowed to know it exists.</summary>
public sealed class NotFoundException(string message = "Not found.") : AppException(message);

/// <summary>409: conflicts with the current state (e.g. already in an organization).</summary>
public sealed class ConflictException(string message) : AppException(message);
