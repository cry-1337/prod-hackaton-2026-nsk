using Backend.Application.Common.Interfaces;
using Backend.Application.Dto;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Account;

public sealed record GetPaymentHistoryQuery(Guid UserId) : IQuery<PaymentHistoryDto>;

public sealed class GetPaymentHistoryHandler(IAppDbContext dbContext) : IQueryHandler<GetPaymentHistoryQuery, PaymentHistoryDto> {
    public async ValueTask<PaymentHistoryDto> Handle(GetPaymentHistoryQuery query, CancellationToken cancellationToken) {
        var orders = await dbContext.GroupPurchaseParticipants
            .Where(participant => participant.UserId == query.UserId && participant.Order != null)
            .OrderByDescending(participant => participant.Order!.PaidAt).ThenBy(participant => participant.Id)
            .Select(participant => new PaymentHistoryEntryDto(participant.GroupPurchase.InviteCode, participant.Id,
                participant.Order!.PaidKopecks, participant.Order.PaidAt, participant.Order.RefundedAt, participant.Order.CashbackAt) {
                PriceRefundKopecks = participant.Order.PriceRefundKopecks
            })
            .ToArrayAsync(cancellationToken);

        var bonuses = await dbContext.Referrals.Where(referral => referral.Invite.InviterId == query.UserId && referral.RewardedAt != null)
            .OrderByDescending(referral => referral.RewardedAt).ThenBy(referral => referral.FriendId)
            .Select(referral => new ReferralBonusDto(referral.FriendId, referral.Friend.Nickname, referral.RewardGroup!.InviteCode,
                referral.RewardAmountKopecks!.Value, referral.RewardedAt!.Value))
            .ToArrayAsync(cancellationToken);

        var netChange = orders.Sum(order => -order.PaidKopecks +
            (order.RefundedAt is null ? order.PriceRefundKopecks : order.PaidKopecks) + (order.CashbackAt is null ? 0 : order.PaidKopecks));
        return new PaymentHistoryDto(netChange + bonuses.Sum(bonus => bonus.AmountKopecks), orders) { ReferralBonuses = bonuses };
    }
}
