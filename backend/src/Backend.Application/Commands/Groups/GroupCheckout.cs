using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Backend.Application.Queries.Account;
using Backend.Domain.Catalog;
using Backend.Domain.Groups;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Commands.Groups;

public static class GroupCheckout {
    internal static void ValidateLimitPrice(GroupPurchaseEntity group, long limitKopecks) {
        if (!group.GetApplicablePriceTiers().Any(tier => tier.PriceKopecks == limitKopecks)) {
            throw new ValidationException([new ValidationFailure("LimitKopecks", "PRICE_TIER_UNAVAILABLE") {
                ErrorCode = "PRICE_TIER_UNAVAILABLE"
            }]);
        }
    }

    internal static void ValidateQuantity(decimal quantity, ESaleUnit unit, long maxUnitPrice) {
        if (!PurchaseQuantity.IsValid(quantity, unit, maxUnitPrice)) {
            throw new ValidationException([new ValidationFailure("Quantity", "QUANTITY_INVALID") {
                ErrorCode = "QUANTITY_INVALID"
            }]);
        }
    }

    public static GroupPaymentQuoteDto QuotePayment(this GroupPurchaseEntity group, Guid userId,
        IReadOnlyDictionary<Guid, long> walletBalances, decimal quantity = 1m) {
        ValidateQuantity(quantity, group.SaleUnit, Math.Max(group.RegularPriceKopecks, group.StartingPriceKopecks));
        var startingPrice = group.StartingPriceKopecks;
        var advance = group.Participants.SingleOrDefault(p => p.UserId == userId)?.NetPrepayment ?? 0;
        var purchasingPower = new Dictionary<Guid, long>(walletBalances) {
            [userId] = walletBalances.GetValueOrDefault(userId) + advance
        };
        var settlement = group.FindReachableTier(userId, startingPrice, purchasingPower, quantity)?.PriceKopecks;
        var price = settlement ?? startingPrice;
        return new GroupPaymentQuoteDto(group.InviteCode, group.Deadline, price,
            Math.Max(0, group.RegularPriceKopecks - price), settlement, true) { TotalKopecks = PurchaseQuantity.Total(price, quantity) };
    }

    public static async Task EnsureActiveCardAsync(this IAppDbContext dbContext, Guid userId, Guid? cardId, CancellationToken cancellationToken) {
        if (cardId is { } id && !await dbContext.TestCards.AnyAsync(card =>
            card.Id == id && card.UserId == userId && card.IsActive, cancellationToken)) {
            throw new EntityNotFoundException("TEST_CARD_NOT_FOUND");
        }
    }

    public static async Task<Dictionary<Guid, long>> GetGroupWalletBalancesAsync(
        this IAppDbContext dbContext,
        IEnumerable<GroupPurchaseEntity> groups,
        Guid? applicantId,
        CancellationToken cancellationToken
    ) {
        var userIds = groups.SelectMany(group => group.Participants)
            .Where(participant => participant is { UsesWallet: true, LimitDebitsKopecks: 0, LimitRevoked: false, LimitKopecks: not null, Order: null })
            .Select(participant => participant.UserId);
        if (applicantId is { } userId) {
            userIds = userIds.Append(userId);
        }

        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) {
            return [];
        }

        return await dbContext.GetWalletBalances(ids)
            .ToDictionaryAsync(wallet => wallet.UserId, wallet => wallet.Balance.BalanceKopecks, cancellationToken);
    }
}
