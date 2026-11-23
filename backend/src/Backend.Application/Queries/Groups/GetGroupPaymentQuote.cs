using Backend.Application.Commands.Groups;
using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Groups;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetGroupPaymentQuoteQuery(string Identifier, Guid UserId, decimal Quantity = 1m) : IQuery<GroupPaymentQuoteDto>;

public sealed class GetGroupPaymentQuoteHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetGroupPaymentQuoteQuery, GroupPaymentQuoteDto> {
    public async ValueTask<GroupPaymentQuoteDto> Handle(GetGroupPaymentQuoteQuery query, CancellationToken cancellationToken) {
        var group = await dbContext.GroupPurchases.WithDetails().ByIdentifier(query.Identifier)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");
        if (group.Status != EGroupPurchaseStatus.Open || group.Deadline <= clock.GetUtcNow()) {
            throw new OperationConflictException("GROUP_CLOSED");
        }

        if (group.Participants.Any(participant => participant.UserId == query.UserId && participant.LimitKopecks is null)) {
            throw new OperationConflictException("ALREADY_JOINED");
        }

        var balances = await dbContext.GetGroupWalletBalancesAsync([group], query.UserId, cancellationToken);
        return group.QuotePayment(query.UserId, balances, query.Quantity);
    }
}
