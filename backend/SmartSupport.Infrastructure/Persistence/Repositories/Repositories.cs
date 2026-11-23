using Microsoft.EntityFrameworkCore;
using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Common.Interfaces;
using SmartSupport.Domain.Entities;

namespace SmartSupport.Infrastructure.Persistence.Repositories;

// EF Core implementations of the Application's data access contracts.
// Queries that only read for display use AsNoTracking projections; writes are staged and
// committed by UnitOfWork so one use case = one transaction.

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId, cancellationToken);

    public void Add(User user) => db.Users.Add(user);
}

internal sealed class OrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public Task<Membership?> FindMembershipByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default) =>
        db.Memberships
            .Include(m => m.User)
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(m => m.User.GoogleId == googleId, cancellationToken);

    public Task<Membership?> FindMembershipByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Memberships
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

    public Task<Organization?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken = default) =>
        db.Organizations.FirstOrDefaultAsync(o => o.InviteCode == inviteCode, cancellationToken);

    public Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken cancellationToken = default) =>
        db.Organizations.AnyAsync(o => o.InviteCode == inviteCode, cancellationToken);

    public async Task<IReadOnlyList<MemberSummary>> ListMembersAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await db.Memberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId)
            .OrderByDescending(m => m.Role == OrgRole.Admin).ThenBy(m => m.JoinedAt)
            .Select(m => new MemberSummary(m.User.Name, m.User.Email, m.Role.ToString(), m.JoinedAt))
            .ToListAsync(cancellationToken);

    public void Add(Organization organization) => db.Organizations.Add(organization);
    public void AddMembership(Membership membership) => db.Memberships.Add(membership);
}

internal sealed class DocumentRepository(AppDbContext db) : IDocumentRepository
{
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await db.Documents
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentSummary(d.Id, d.FileName, d.ContentType, d.CreatedAt, d.Chunks.Count))
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        db.Documents.CountAsync(d => d.OrganizationId == organizationId, cancellationToken);

    public Task<KnowledgeDocument?> FindWithChunksAsync(Guid documentId, Guid organizationId, CancellationToken cancellationToken = default) =>
        db.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.OrganizationId == organizationId, cancellationToken);

    public void Add(KnowledgeDocument document) => db.Documents.Add(document);
    public void Remove(KnowledgeDocument document) => db.Documents.Remove(document);
}

internal sealed class ChatRepository(AppDbContext db) : IChatRepository
{
    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(int userId, CancellationToken cancellationToken = default) =>
        await db.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionSummary(s.Id, s.Title, s.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<ChatSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public Task<ChatSession?> FindOwnedSessionAsync(Guid sessionId, int userId, CancellationToken cancellationToken = default) =>
        db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<ChatMessage>> ListMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatMessage>> ListRecentMessagesAsync(Guid sessionId, int count, CancellationToken cancellationToken = default)
    {
        var newestFirst = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
        newestFirst.Reverse();
        return newestFirst;
    }

    public Task<ChatMessage?> FindOwnedAnswerAsync(int messageId, int userId, CancellationToken cancellationToken = default) =>
        db.ChatMessages
            .Where(m => m.Id == messageId && m.Role == ChatMessage.AssistantRole)
            .Where(m => db.ChatSessions.Any(s => s.Id == m.ChatSessionId && s.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

    public void AddSession(ChatSession session) => db.ChatSessions.Add(session);
    public void AddMessage(ChatMessage message) => db.ChatMessages.Add(message);
    public void RemoveSession(ChatSession session) => db.ChatSessions.Remove(session);
}

internal sealed class FeedbackRepository(AppDbContext db) : IFeedbackRepository
{
    public async Task<FeedbackCounts> CountAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var ratings = RatedAnswers(organizationId).Select(m => m.Feedback);
        var positive = await ratings.CountAsync(f => f == 1, cancellationToken);
        var negative = await ratings.CountAsync(f => f == -1, cancellationToken);
        return new FeedbackCounts(positive, negative);
    }

    public async Task<IReadOnlyList<RatedAnswerRow>> ListRatedAsync(Guid organizationId, int? rating, int max, CancellationToken cancellationToken = default)
    {
        var query = RatedAnswers(organizationId);
        if (rating.HasValue)
            query = query.Where(m => m.Feedback == rating.Value);

        return await (
                from m in query
                join s in db.ChatSessions on m.ChatSessionId equals s.Id
                orderby m.Timestamp descending
                select new RatedAnswerRow(
                    m.Id,
                    m.Feedback!.Value,
                    // The question this answer replied to: the latest user message before it in the same chat.
                    db.ChatMessages
                        .Where(q => q.ChatSessionId == m.ChatSessionId && q.Role == ChatMessage.UserRole && q.Timestamp <= m.Timestamp)
                        .OrderByDescending(q => q.Timestamp)
                        .Select(q => q.Content)
                        .FirstOrDefault(),
                    m.Content,
                    m.Sources,
                    m.Timestamp,
                    s.Title,
                    s.User.Name,
                    s.User.Email))
            .AsNoTracking()
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Rated assistant messages in chats that belong to the organization.</summary>
    private IQueryable<ChatMessage> RatedAnswers(Guid organizationId) =>
        db.ChatMessages
            .AsNoTracking()
            .Where(m => m.Role == ChatMessage.AssistantRole && m.Feedback != null)
            .Where(m => db.ChatSessions.Any(s => s.Id == m.ChatSessionId && s.OrganizationId == organizationId));
}
