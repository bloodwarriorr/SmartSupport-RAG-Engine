using SmartSupport.Domain.Entities;

namespace SmartSupport.Application.Abstractions.Persistence;

// Data access contracts (DAL). Implemented with EF Core in SmartSupport.Infrastructure.
// Writes are staged by the repositories and committed together through IUnitOfWork.

// ---------- Read models: lightweight projections for list screens ----------

public record MemberSummary(string? Name, string Email, string Role, DateTime JoinedAt);

public record DocumentSummary(Guid Id, string FileName, string ContentType, DateTime CreatedAt, int ChunkCount);

public record SessionSummary(Guid Id, string Title, DateTime CreatedAt);

public record FeedbackCounts(int Positive, int Negative);

public record RatedAnswerRow(
    int Id,
    int Feedback,
    string? Question,
    string Answer,
    string? SourcesJson,
    DateTime Timestamp,
    string SessionTitle,
    string? UserName,
    string UserEmail);

// ---------- Repositories ----------

public interface IUserRepository
{
    Task<User?> FindByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);
    void Add(User user);
}

public interface IOrganizationRepository
{
    /// <summary>The user's membership with its User and Organization loaded.</summary>
    Task<Membership?> FindMembershipByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);

    /// <summary>The user's membership with its Organization loaded.</summary>
    Task<Membership?> FindMembershipByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<Organization?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken = default);
    Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberSummary>> ListMembersAsync(Guid organizationId, CancellationToken cancellationToken = default);

    void Add(Organization organization);
    void AddMembership(Membership membership);
}

public interface IDocumentRepository
{
    Task<IReadOnlyList<DocumentSummary>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>A document of this organization with its chunks loaded, or null.</summary>
    Task<KnowledgeDocument?> FindWithChunksAsync(Guid documentId, Guid organizationId, CancellationToken cancellationToken = default);

    void Add(KnowledgeDocument document);
    void Remove(KnowledgeDocument document);
}

public interface IChatRepository
{
    Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(int userId, CancellationToken cancellationToken = default);
    Task<ChatSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ChatSession?> FindOwnedSessionAsync(Guid sessionId, int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> ListMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>The last <paramref name="count"/> messages of a chat, oldest first.</summary>
    Task<IReadOnlyList<ChatMessage>> ListRecentMessagesAsync(Guid sessionId, int count, CancellationToken cancellationToken = default);

    /// <summary>An assistant message in one of the user's own chats, or null.</summary>
    Task<ChatMessage?> FindOwnedAnswerAsync(int messageId, int userId, CancellationToken cancellationToken = default);

    void AddSession(ChatSession session);
    void AddMessage(ChatMessage message);
    void RemoveSession(ChatSession session);
}

public interface IFeedbackRepository
{
    Task<FeedbackCounts> CountAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Rated answers in the organization's chats, newest first. A null rating means both.</summary>
    Task<IReadOnlyList<RatedAnswerRow>> ListRatedAsync(Guid organizationId, int? rating, int max, CancellationToken cancellationToken = default);
}
