using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Api.Auth;

internal sealed class JwtTokens(IConfiguration configuration, TimeProvider clock) : IAccessTokenIssuer {
    private const string m_TokenType = "JWT";

    private readonly string m_Issuer = configuration["Jwt:Issuer"] ?? "vmeste-api";
    private readonly string m_Audience = configuration["Jwt:Audience"] ?? "vmeste-web";
    private readonly SymmetricSecurityKey m_Key = ReadKey(configuration["Jwt:SigningKey"]);

    public AuthTokenDto Issue(UserDto user) {
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = issuedAt.AddHours(1);

        var descriptor = new SecurityTokenDescriptor {
            Issuer = m_Issuer,
            Audience = m_Audience,
            TokenType = m_TokenType,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(m_Key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object> {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AuthTokenDto(token, expiresAt, user);
    }

    public TokenValidationParameters ValidationParameters() => new() {
        ValidateIssuer = true,
        ValidIssuer = m_Issuer,

        ValidateAudience = true,
        ValidAudience = m_Audience,

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = m_Key,
        RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidTypes = [m_TokenType],

        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private static SymmetricSecurityKey ReadKey(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException("JWT_SIGNING_KEY_REQUIRED");
        }

        byte[] bytes;

        try {
            bytes = Convert.FromBase64String(value);
        } catch (FormatException exception) {
            throw new InvalidOperationException("JWT_SIGNING_KEY_INVALID", exception);
        }

        if (bytes.Length < 32) {
            throw new InvalidOperationException("JWT_SIGNING_KEY_TOO_SHORT");
        }

        return new SymmetricSecurityKey(bytes);
    }
}
