using Backend.Application.Dto;
using Backend.Domain.Catalog;
using Backend.Domain.Groups;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Queries.Groups;

internal static class GroupPurchaseReadModel {
    public static IQueryable<GroupPurchaseEntity> OrganizedBy(this IQueryable<GroupPurchaseEntity> groups, Guid userId) =>
        groups.Where(group => group.OrganizerId == userId);

    public static IQueryable<GroupPurchaseEntity> ByIdentifier(
        this IQueryable<GroupPurchaseEntity> groups,
        string identifier,
        bool inviteCodeOnly = false
    ) {
        var code = identifier.Trim().ToLowerInvariant();
        Guid? legacyId = !inviteCodeOnly && Guid.TryParse(code, out var id) ? id : null;

        return groups.Where(group => group.InviteCode == code || group.Id == legacyId)
            .OrderByDescending(group => group.InviteCode == code)
            .ThenBy(group => group.Id);
    }

    public static IQueryable<GroupPurchaseEntity> WithDetails(
        this DbSet<GroupPurchaseEntity> groups,
        QueryTrackingBehavior tracking = QueryTrackingBehavior.NoTracking
    ) => groups.AsTracking(tracking)
        .Include(group => group.Product)
        .Include(group => group.Organizer)
            .ThenInclude(organizer => organizer.OrganizerLikes)
        .Include(group => group.PriceTiers)
        .Include(group => group.Participants)
            .ThenInclude(participant => participant.Order)
        .AsSplitQuery();

    public static IOrderedQueryable<GroupPurchaseEntity> OrderByOrganizerLikes(this IQueryable<GroupPurchaseEntity> groups) =>
        groups.OrderByDescending(group => group.Organizer.OrganizerLikes.Count)
            .ThenBy(group => group.Deadline)
            .ThenBy(group => group.Id);

    public static GroupPurchaseDto ToDto(this GroupPurchaseEntity group, DateTimeOffset now,
        IReadOnlyDictionary<Guid, long> walletBalances) => group.ToDto(group.Product, group.Organizer, now, walletBalances);

    public static GroupPurchaseDto ToDto(
        this GroupPurchaseEntity group,
        ProductEntity product,
        UserEntity organizer,
        DateTimeOffset now,
        IReadOnlyDictionary<Guid, long> walletBalances
    ) {
        var status = group.Status == EGroupPurchaseStatus.Open && group.Deadline <= now
            ? EGroupPurchaseStatus.Cancelled
            : group.Status;

        var tiers = group.GetApplicablePriceTiers().OrderBy(tier => tier.RequiredParticipants).ToArray();
        var progress = group.SettlementPriceKopecks is { } price
            ? tiers.FirstOrDefault(tier => tier.PriceKopecks == price)
            : tiers.OrderBy(tier => tier.RequiredParticipants - group.EligibleQuantity(tier.PriceKopecks, walletBalances: walletBalances))
                .ThenBy(tier => tier.PriceKopecks).FirstOrDefault();

        return new GroupPurchaseDto(
            group.InviteCode,
            group.ProductId,
            product.Name,
            product.ImageKey,
            group.RegularPriceKopecks,
            group.SaleUnit,
            group.OrganizerId,
            organizer.Nickname,
            group.Deadline,
            group.CreatedAt,
            status,
            group.BonusQuantity.HasValue ? EGroupPurchaseReward.FreeProduct : group.RewardKind,
            group.Participants.Count(participant => !participant.LimitRevoked),
            tiers
                .Select(tier => new PriceTierDto(tier.RequiredParticipants, tier.PriceKopecks) {
                    WaitingBuyers = status == EGroupPurchaseStatus.Open
                        ? group.Participants.Count(p => p.LimitKopecks == tier.PriceKopecks && !p.LimitRevoked && p.Order == null)
                        : null,
                    EligibleQuantity = status == EGroupPurchaseStatus.Open
                        ? group.EligibleQuantity(tier.PriceKopecks, walletBalances: walletBalances)
                        : null
                })
                .ToArray()) {
            EligibleQuantity = progress is null ? 0 : group.EligibleQuantity(progress.PriceKopecks, walletBalances: walletBalances),
            EligibleParticipantsCount = progress is null ? 0 : group.CountEligible(progress.PriceKopecks, walletBalances: walletBalances),
            ProgressTier = progress is null ? null : new PriceTierDto(progress.RequiredParticipants, progress.PriceKopecks),
            PendingLimitsCount = status == EGroupPurchaseStatus.Open
                ? group.Participants.Count(person => person.LimitKopecks.HasValue && !person.LimitRevoked && person.Order == null)
                : 0,
            DirectPaymentsCount = group.Participants.Count(person => person.LimitKopecks == null && !person.LimitRevoked &&
                (person.UserId != group.OrganizerId || !group.HasOrganizerReservation)),
            PurchasedLimitsCount = group.Participants.Count(person => person.LimitKopecks != null && person.Order != null),
            OrganizerLikesCount = organizer.OrganizerLikes.Count,
            SettlementPriceKopecks = group.SettlementPriceKopecks
        };
    }

    public static GroupLimitDto? GetLimit(this GroupPurchaseEntity group, Guid userId, DateTimeOffset now, IReadOnlyDictionary<Guid, long> balances) {
        var participant = group.Participants.SingleOrDefault(item => item.UserId == userId);
        if (participant?.LimitKopecks is not { } limit) {
            return null;
        }

        var address = DeliveryAddress.FromStorage(participant.DeliveryAddress);
        if (participant.Order is { } order) {
            return new GroupLimitDto(limit, EGroupLimitStatus.Purchased, order.PaidKopecks, order.ParticipantId) {
                Quantity = participant.Quantity,
                Address = address,
                DeliveryAddress = address?.ToLabel() ?? participant.DeliveryAddress,
                RefundedKopecks = participant.LimitRefundsKopecks
            };
        }

        var status = participant.LimitRevoked ? EGroupLimitStatus.Revoked
            : group.Status != EGroupPurchaseStatus.Open || group.Deadline <= now ? EGroupLimitStatus.Closed
            : EGroupLimitStatus.Pending;
        var remaining = status == EGroupLimitStatus.Pending
            ? group.RemainingQuantity(limit, balances)
            : null;
        return new GroupLimitDto(limit, status, participant.NetPrepayment, null) {
            Quantity = participant.Quantity,
            RefundedKopecks = participant.LimitRefundsKopecks,
            Address = address,
            DeliveryAddress = address?.ToLabel() ?? participant.DeliveryAddress,
            RemainingQuantity = remaining
        };
    }

    public static GroupPaymentDto? GetPayment(this GroupPurchaseEntity group, Guid userId) {
        var participant = group.Participants.SingleOrDefault(item => item.UserId == userId);
        if (participant?.Order is not { } order) {
            return null;
        }

        var address = DeliveryAddress.FromStorage(order.DeliveryAddress);
        return new GroupPaymentDto(order.ParticipantId, order.CardId, address?.ToLabel() ?? order.DeliveryAddress, order.PaidKopecks,
            order.RefundedAt is null ? order.PriceRefundKopecks : order.PaidKopecks, order.CashbackAt is null ? 0 : order.PaidKopecks,
            order.PaidKopecks == 0 && userId == group.OrganizerId && group.HasOrganizerReservation, false) { Quantity = participant.Quantity, Address = address };
    }
}
