using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Validation;
using Backend.Application.Dto;
using Backend.Domain.Users;
using FluentValidation;
using Mediator;

namespace Backend.Application.Commands.Auth;

public sealed record RegisterCommand(string Email, string Nickname, string Password) : ICommand<AuthTokenDto>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand> {
    public RegisterCommandValidator() {
        RuleFor(command => command.Email).ValidEmail();

        RuleFor(command => command.Nickname)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("NICKNAME_REQUIRED")
            .MaximumLength(64).WithErrorCode("NICKNAME_TOO_LONG");

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("PASSWORD_REQUIRED")
            .Length(12, 128).WithErrorCode("PASSWORD_LENGTH_INVALID");
    }
}

public sealed class RegisterHandler(
    IAppDbContext dbContext,
    IUserPasswords passwords,
    IAccessTokenIssuer tokens
) : ICommandHandler<RegisterCommand, AuthTokenDto> {
    public async ValueTask<AuthTokenDto> Handle(RegisterCommand command, CancellationToken cancellationToken) {
        var user = new UserEntity {
            Email = UserEntity.NormalizeEmail(command.Email),
            Nickname = command.Nickname.Trim(),
            PasswordHash = passwords.Hash(command.Password)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tokens.Issue(new UserDto(user.Id, user.Email, user.Nickname));
    }
}
