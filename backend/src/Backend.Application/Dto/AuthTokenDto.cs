namespace Backend.Application.Dto;

public sealed record AuthTokenDto(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
