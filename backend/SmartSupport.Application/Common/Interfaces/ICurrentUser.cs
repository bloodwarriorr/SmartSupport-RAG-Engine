namespace SmartSupport.Application.Common.Interfaces;

/// <summary>The signed-in caller, as described by their (already validated) Google token.</summary>
public interface ICurrentUser
{
    /// <summary>Google account id ("sub" claim), or null when not signed in.</summary>
    string? GoogleId { get; }
    string? Email { get; }
    string? Name { get; }
}

/// <summary>Commits everything the repositories staged in this request as one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
