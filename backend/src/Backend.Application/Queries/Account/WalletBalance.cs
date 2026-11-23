using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Account;

internal static class WalletBalance {
    public static IQueryable<WalletBalanceRow> GetWalletBalances(this IAppDbContext dbContext, Guid[] userIds) {
        var orders = dbContext.GroupOrders;
        var walletOrders = orders.Where(order => order.Participant.UsesWallet &&
            (order.Participant.LimitKopecks == null || order.Participant.LimitDebitsKopecks == 0));
        var participants = dbContext.GroupPurchaseParticipants;
        var referralBonuses = dbContext.Referrals.Where(referral => referral.RewardedAt != null);

        return dbContext.Users.Where(user => userIds.Contains(user.Id)).Select(user => new WalletBalanceRow(user.Id, new BalanceDto(
            dbContext.WalletTopUps.Where(topUp => topUp.UserId == user.Id).Sum(topUp => topUp.AmountKopecks),
            walletOrders.Where(order => order.Participant.UserId == user.Id).Sum(order => order.PaidKopecks) +
                participants.Where(p => p.UserId == user.Id).Sum(p => p.LimitDebitsKopecks),
            walletOrders.Where(order => order.Participant.UserId == user.Id)
                .Sum(order => order.RefundedAt != null ? order.PaidKopecks : order.PriceRefundKopecks) +
                participants.Where(p => p.UserId == user.Id).Sum(p => p.LimitRefundsKopecks),
            orders.Where(order => order.Participant.UserId == user.Id && order.CashbackAt != null).Sum(order => order.PaidKopecks),
            referralBonuses.Where(referral => referral.Invite.InviterId == user.Id).Sum(referral => referral.RewardAmountKopecks ?? 0))));
    }

    public sealed record WalletBalanceRow(Guid UserId, BalanceDto Balance);
}
