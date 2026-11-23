namespace Backend.Api.Contracts.Responses;

public sealed record UserResponse(Guid Id, string Email, string Nickname);
