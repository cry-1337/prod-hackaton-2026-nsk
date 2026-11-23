using Backend.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Users;

public sealed record RemoveTestCardCommand(Guid UserId, Guid CardId) : ICommand<Unit>;

public sealed class RemoveTestCardHandler(IAppDbContext dbContext) : ICommandHandler<RemoveTestCardCommand, Unit> {
    public async ValueTask<Unit> Handle(RemoveTestCardCommand command, CancellationToken cancellationToken) {
        await dbContext.TestCards.Where(card => card.Id == command.CardId && card.UserId == command.UserId && card.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(card => card.IsActive, false), cancellationToken);
        return Unit.Value;
    }
}
