using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Common.Exceptions;
using SmartSupport.Application.Common.Interfaces;
using SmartSupport.Application.Organizations;
using SmartSupport.Domain.Entities;

namespace SmartSupport.Application.Chat;

// ---------- DTOs (JSON shapes the client relies on) ----------

public record AskCommand(string Query, Guid SessionId);
public record MessageDto(int Id, string Content, string Role, DateTime Timestamp, Guid ChatSessionId, IReadOnlyList<SourceRef> Sources, int? Feedback);
public record FeedbackResultDto(int Id, int? Feedback);

public interface IChatService
{
    Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<SessionSummary> RenameSessionAsync(Guid sessionId, string title, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<FeedbackResultDto> RateAnswerAsync(int messageId, int value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the request, saves the question and prepares the answer. All checks run before this returns,
    /// so errors become proper HTTP responses; the returned stream then saves the full answer when it ends.
    /// </summary>
    Task<IAsyncEnumerable<string>> AskAsync(AskCommand command, CancellationToken cancellationToken = default);
}

public sealed class ChatService : IChatService
{
    public const int MaxTitleLength = 100;
    private const int AutoTitleLength = 30;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOrganizationAccess _access;
    private readonly IUserRepository _users;
    private readonly IChatRepository _chats;
    private readonly IAnswerGenerator _answerGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RagOptions _options;

    public ChatService(
        IOrganizationAccess access,
        IUserRepository users,
        IChatRepository chats,
        IAnswerGenerator answerGenerator,
        IUnitOfWork unitOfWork,
        IOptions<RagOptions> options)
    {
        _access = access;
        _users = users;
        _chats = chats;
        _answerGenerator = answerGenerator;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var user = await _access.GetOrCreateCurrentUserAsync(cancellationToken);
        return await _chats.ListSessionsAsync(user.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await RequireOwnSessionAsync(sessionId, cancellationToken);
        var messages = await _chats.ListMessagesAsync(session.Id, cancellationToken);
        return messages
            .Select(m => new MessageDto(m.Id, m.Content, m.Role, m.Timestamp, m.ChatSessionId, ParseSources(m.Sources), m.Feedback))
            .ToList();
    }

    public async Task<SessionSummary> RenameSessionAsync(Guid sessionId, string title, CancellationToken cancellationToken = default)
    {
        title = title?.Trim() ?? string.Empty;
        if (title.Length == 0 || title.Length > MaxTitleLength)
            throw new ValidationException($"Title must be 1-{MaxTitleLength} characters.");

        var session = await RequireOwnSessionAsync(sessionId, cancellationToken);
        session.Title = title;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SessionSummary(session.Id, session.Title, session.CreatedAt);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await RequireOwnSessionAsync(sessionId, cancellationToken);
        _chats.RemoveSession(session); // messages are deleted by cascade
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeedbackResultDto> RateAnswerAsync(int messageId, int value, CancellationToken cancellationToken = default)
    {
        if (value is not (-1 or 0 or 1))
            throw new ValidationException("Value must be 1, -1 or 0.");

        var user = await _access.GetOrCreateCurrentUserAsync(cancellationToken);
        var message = await _chats.FindOwnedAnswerAsync(messageId, user.Id, cancellationToken)
            ?? throw new NotFoundException();

        message.Feedback = value == 0 ? null : value;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new FeedbackResultDto(message.Id, message.Feedback);
    }

    public async Task<IAsyncEnumerable<string>> AskAsync(AskCommand command, CancellationToken cancellationToken = default)
    {
        var question = command.Query?.Trim() ?? string.Empty;
        if (question.Length == 0)
            throw new ValidationException("Please type a question.");
        if (question.Length > _options.MaxQuestionLength)
            throw new ValidationException($"Questions are limited to {_options.MaxQuestionLength} characters.");
        if (command.SessionId == Guid.Empty)
            throw new ValidationException("A chat id is required.");

        // Chatting needs an organization: answers only use that organization's documents.
        var membership = await _access.RequireMembershipAsync(cancellationToken);
        var user = membership.User;

        var session = await _chats.FindSessionAsync(command.SessionId, cancellationToken);
        if (session == null)
        {
            session = new ChatSession
            {
                Id = command.SessionId,
                UserId = user.Id,
                OrganizationId = membership.OrganizationId,
                Title = question.Length > AutoTitleLength ? question[..AutoTitleLength] + "..." : question,
                CreatedAt = DateTime.UtcNow
            };
            _chats.AddSession(session);
        }
        else if (session.UserId != user.Id)
        {
            // Someone else's chat (or a guessed id): don't reveal that it exists.
            throw new NotFoundException();
        }

        // Conversation memory: loaded before the new question is saved.
        var history = (await _chats.ListRecentMessagesAsync(session.Id, _options.MemoryMessageCount, cancellationToken))
            .Select(m => new ChatTurn(m.Role == ChatMessage.UserRole, Truncate(m.Content, _options.MemoryMessageMaxLength)))
            .ToList();

        _chats.AddMessage(new ChatMessage
        {
            ChatSessionId = session.Id,
            Role = ChatMessage.UserRole,
            Content = question,
            UserEmail = user.Email
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var answer = await _answerGenerator.GenerateAsync(question, membership.OrganizationId, history, cancellationToken);
        return StreamAndSaveAsync(answer, session.Id, user.Email, cancellationToken);
    }

    /// <summary>Passes the answer through to the client, then saves the full text with its sources.</summary>
    private async IAsyncEnumerable<string> StreamAndSaveAsync(
        GeneratedAnswer answer, Guid sessionId, string userEmail, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fullResponse = new StringBuilder();
        await foreach (var token in answer.Tokens.WithCancellation(cancellationToken))
        {
            fullResponse.Append(token);
            yield return token;
        }

        _chats.AddMessage(new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = ChatMessage.AssistantRole,
            Content = fullResponse.ToString(),
            UserEmail = userEmail,
            Sources = answer.Sources.Count > 0 ? JsonSerializer.Serialize(answer.Sources, JsonOptions) : null
        });
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<ChatSession> RequireOwnSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var user = await _access.GetOrCreateCurrentUserAsync(cancellationToken);
        return await _chats.FindOwnedSessionAsync(sessionId, user.Id, cancellationToken)
            ?? throw new NotFoundException();
    }

    internal static IReadOnlyList<SourceRef> ParseSources(string? json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<SourceRef>();
        try { return JsonSerializer.Deserialize<List<SourceRef>>(json, JsonOptions) ?? new List<SourceRef>(); }
        catch (JsonException) { return Array.Empty<SourceRef>(); }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
