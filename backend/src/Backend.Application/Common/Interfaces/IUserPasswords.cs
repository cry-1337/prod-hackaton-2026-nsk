using Backend.Domain.Users;

namespace Backend.Application.Common.Interfaces;

public interface IUserPasswords {
    public string Hash(string password);

    public bool Verify(UserEntity? user, string password);

    public bool NeedsRehash(string hash);
}
