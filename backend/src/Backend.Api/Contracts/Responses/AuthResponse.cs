namespace Backend.Api.Contracts.Responses;

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User) {
    public string TokenType => "Bearer";
}
