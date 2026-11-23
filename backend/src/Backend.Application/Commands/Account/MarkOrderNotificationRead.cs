using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Queries.Account;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Account;

public sealed record MarkOrderNotificationReadCommand(Guid UserId, Guid NotificationId) : ICommand<Unit>;

public sealed class MarkOrderNotificationReadHandler(IAppDbContext dbContext, TimeProvider clock)
    : ICommandHandler<MarkOrderNotificationReadCommand, Unit> {
    public async ValueTask<Unit> Handle(MarkOrderNotificationReadCommand command, CancellationToken cancellationToken) {
        var now = clock.GetUtcNow();
        var updated = await dbContext.GroupOrders.ForUser(command.UserId)
            .Where(order => order.ParticipantId == command.NotificationId)
            .ExecuteUpdateAsync(update => update.SetProperty(order => order.NotificationReadAt,
                order => order.NotificationReadAt ?? now), cancellationToken);
        if (updated == 0) {
            throw new EntityNotFoundException("NOTIFICATION_NOT_FOUND");
        }

        return Unit.Value;
    }
}
