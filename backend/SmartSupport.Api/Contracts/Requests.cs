namespace SmartSupport.Api.Contracts;

// Request bodies accepted by the API. Response bodies are the Application DTOs.

public record CreateOrganizationRequest(string Name);
public record JoinOrganizationRequest(string Code);
public record RenameSessionRequest(string Title);
public record RateAnswerRequest(int Value);
public record AskRequest(string Query, Guid SessionId);
