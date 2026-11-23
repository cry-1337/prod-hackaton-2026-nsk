using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Validation;
using Backend.Application.Dto;
using Backend.Domain.Users;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Auth;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokenDto>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand> {
    public LoginCommandValidator() {
        RuleFor(command => command.Email).ValidEmail();

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("PASSWORD_REQUIRED")
            .MaximumLength(128).WithErrorCode("PASSWORD_TOO_LONG");
    }
}

public sealed class LoginHandler(
    IAppDbContext dbContext,
    IUserPasswords passwords,
    IAccessTokenIssuer tokens
) : ICommandHandler<LoginCommand, AuthTokenDto> {
    public async ValueTask<AuthTokenDto> Handle(LoginCommand command, CancellationToken cancellationToken) {
        var email = UserEntity.NormalizeEmail(command.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

        if (!passwords.Verify(user, command.Password) || user is null) {
            throw new InvalidCredentialsException();
        }

        if (passwords.NeedsRehash(user.PasswordHash)) {
            user.PasswordHash = passwords.Hash(command.Password);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return tokens.Issue(new UserDto(user.Id, user.Email, user.Nickname));
    }
}
