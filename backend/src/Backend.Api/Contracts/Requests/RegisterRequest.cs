namespace Backend.Api.Contracts.Requests;

public sealed record RegisterRequest(string Email, string Nickname, string Password);
