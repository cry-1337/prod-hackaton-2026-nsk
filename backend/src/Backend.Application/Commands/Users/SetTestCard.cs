using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Users;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Users;

public sealed record SetTestCardCommand(Guid UserId, string LastFour) : ICommand<TestCardDto>;

public sealed class SetTestCardValidator : AbstractValidator<SetTestCardCommand> {
    public SetTestCardValidator() {
        RuleFor(command => command.LastFour).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("CARD_LAST_FOUR_REQUIRED")
            .Matches(@"\A[0-9]{4}\z").WithErrorCode("CARD_LAST_FOUR_INVALID");
    }
}

public sealed class SetTestCardHandler(IAppDbContext dbContext) : ICommandHandler<SetTestCardCommand, TestCardDto> {
    public async ValueTask<TestCardDto> Handle(SetTestCardCommand command, CancellationToken cancellationToken) {
        if (!await dbContext.Users.AnyAsync(user => user.Id == command.UserId, cancellationToken)) {
            throw new InvalidCredentialsException();
        }

        await dbContext.AttachTestCardAsync(new TestCardEntity { UserId = command.UserId, LastFour = command.LastFour }, cancellationToken);
        return await dbContext.TestCards.Where(card => card.UserId == command.UserId && card.LastFour == command.LastFour)
            .Select(card => new TestCardDto(card.Id, card.LastFour)).SingleAsync(cancellationToken);
    }
}
