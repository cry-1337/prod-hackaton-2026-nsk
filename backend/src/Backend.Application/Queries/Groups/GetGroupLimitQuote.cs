using Backend.Application.Commands.Groups;
using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Domain.Groups;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

public sealed record GetGroupLimitQuoteQuery(string Identifier, Guid UserId, long LimitKopecks, decimal Quantity = 1m) : IQuery<GroupLimitQuoteDto>;

public sealed class GetGroupLimitQuoteValidator : AbstractValidator<GetGroupLimitQuoteQuery> {
    public GetGroupLimitQuoteValidator() {
        RuleFor(query => query.LimitKopecks).GreaterThan(0).WithErrorCode("LIMIT_MUST_BE_POSITIVE");
    }
}

public sealed class GetGroupLimitQuoteHandler(IAppDbContext dbContext, TimeProvider clock)
    : IQueryHandler<GetGroupLimitQuoteQuery, GroupLimitQuoteDto> {
    public async ValueTask<GroupLimitQuoteDto> Handle(GetGroupLimitQuoteQuery query, CancellationToken cancellationToken) {
        var group = await dbContext.GroupPurchases.WithDetails().ByIdentifier(query.Identifier)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("GROUP_NOT_FOUND");
        if (group.Status != EGroupPurchaseStatus.Open || group.Deadline <= clock.GetUtcNow()) {
            throw new OperationConflictException("GROUP_CLOSED");
        }

        if (group.Participants.Any(participant => participant.UserId == query.UserId && participant.LimitKopecks is null)) {
            throw new OperationConflictException("ALREADY_JOINED");
        }

        var minimum = group.GetApplicablePriceTiers().Min(tier => (long?)tier.PriceKopecks)
            ?? throw new OperationConflictException("PRODUCT_HAS_NO_GROUP_PRICES");
        GroupCheckout.ValidateLimitPrice(group, query.LimitKopecks);
        var balances = await dbContext.GetGroupWalletBalancesAsync([group], query.UserId, cancellationToken);
        GroupCheckout.ValidateQuantity(query.Quantity, group.SaleUnit, Math.Max(group.RegularPriceKopecks, group.StartingPriceKopecks));
        var advance = group.Participants.SingleOrDefault(p => p.UserId == query.UserId)?.NetPrepayment ?? 0;
        var total = PurchaseQuantity.Total(query.LimitKopecks, query.Quantity);
        var difference = total - advance;
        var canAfford = balances.GetValueOrDefault(query.UserId) >= difference;
        balances[query.UserId] = balances.GetValueOrDefault(query.UserId) + advance;
        var price = canAfford ? group.FindReachableTier(query.UserId, query.LimitKopecks, balances, query.Quantity)?.PriceKopecks : null;
        var finalCost = price is { } reachable && reachable <= query.LimitKopecks
            ? PurchaseQuantity.Total(reachable, query.Quantity)
            : price.HasValue ? 0 : total;
        var otherLimitsPurchased = price is { } tierPrice ? group.Participants.Count(participant =>
            participant.UserId != query.UserId && participant.LimitKopecks.HasValue && participant.CanBuyAt(tierPrice, balances)) : 0;

        return new GroupLimitQuoteDto(group.InviteCode, group.Deadline, query.LimitKopecks, minimum,
            false, price, Math.Max(0, difference), otherLimitsPurchased) {
            RemainingQuantity = group.RemainingQuantity(query.LimitKopecks, balances, query.UserId, query.Quantity),
            CanAfford = canAfford,
            RefundNowKopecks = Math.Max(0, -difference) + total - finalCost
        };
    }
}
