using Backend.Application.Dto;

namespace Backend.Application.Common.Interfaces;

public interface IAccessTokenIssuer {
    public AuthTokenDto Issue(UserDto user);
}
