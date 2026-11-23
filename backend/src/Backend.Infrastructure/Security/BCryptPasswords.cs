using System.Security.Cryptography;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Users;
using Microsoft.AspNetCore.Identity;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Backend.Infrastructure.Security;

internal sealed class BCryptPasswords : IUserPasswords {
    private const int m_WorkFactor = 12;

    private readonly PasswordHasher<UserEntity> m_LegacyHasher = new();
    private readonly string m_DummyHash = BCryptNet.EnhancedHashPassword(
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        m_WorkFactor);

    public string Hash(string password) => BCryptNet.EnhancedHashPassword(password, m_WorkFactor);

    public bool Verify(UserEntity? user, string password) {
        var hash = user?.PasswordHash ?? m_DummyHash;

        if (IsBCrypt(hash)) {
            return BCryptNet.EnhancedVerify(password, hash) && user is not null;
        }

        return user is not null
            && m_LegacyHasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
    }

    public bool NeedsRehash(string hash) =>
        !IsBCrypt(hash) || BCryptNet.PasswordNeedsRehash(hash, m_WorkFactor);

    private static bool IsBCrypt(string hash) => hash.StartsWith("$2", StringComparison.Ordinal);
}
