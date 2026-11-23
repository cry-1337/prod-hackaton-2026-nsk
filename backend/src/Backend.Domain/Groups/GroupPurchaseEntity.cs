using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.Domain.Catalog;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_purchases")]
[Index(nameof(InviteCode), IsUnique = true, Name = "ux_group_purchases_invite_code")]
[Index(nameof(OrganizerId), nameof(CreationKey), IsUnique = true, Name = "ux_group_purchases_creation_key")]
[Index(nameof(ProductId), nameof(Status), nameof(Deadline))]
[Index(nameof(Status), nameof(Deadline))]
public sealed class GroupPurchaseEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [Column("product_id")]
    public Guid ProductId { get; init; }

    [Column("organizer_id")]
    public Guid OrganizerId { get; init; }

    [MaxLength(32)]
    [Column("invite_code")]
    public string InviteCode { get; set; } = null!;

    [MaxLength(128)]
    [Column("creation_key")]
    public string? CreationKey { get; init; }

    [MaxLength(64)]
    [Column("creation_checkout_hash")]
    public string? CreationCheckoutHash { get; init; }

    [Column("confirmed_creation_price_kopecks")]
    public long? ConfirmedCreationPriceKopecks { get; init; }

    [Column("deadline")]
    public DateTimeOffset Deadline { get; init; }

    [Column("status")]
    public EGroupPurchaseStatus Status { get; set; } = EGroupPurchaseStatus.Open;

    [Column("settlement_price_kopecks")]
    public long? SettlementPriceKopecks { get; private set; }

    [Column("successful_at")]
    public DateTimeOffset? SuccessfulAt { get; private set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [Column("sale_unit")]
    public ESaleUnit SaleUnit { get; init; } = ESaleUnit.Piece;

    [Column("regular_price_kopecks")]
    public long RegularPriceKopecks { get; init; }

    [Column("target_participants")]
    public int? TargetParticipants { get; init; }

    [Column("organizer_initial_quantity", TypeName = "numeric(12,3)")]
    public decimal OrganizerInitialQuantity { get; init; } = 1m;

    [Column("organizer_initial_limit_kopecks")]
    public long? OrganizerInitialLimitKopecks { get; init; }

    [Column("organizer_wallet_balance_at_creation_kopecks")]
    public long? OrganizerWalletBalanceAtCreationKopecks { get; init; }

    [Column("bonus_quantity", TypeName = "numeric(12,3)")]
    public decimal? BonusQuantity { get; init; }

    [MaxLength(16)]
    [Column("bonus_unit")]
    public string? BonusUnit { get; init; }

    [Column("bonus_awarded_at")]
    public DateTimeOffset? BonusAwardedAt { get; private set; }

    [Column("reward_kind")]
    public EGroupPurchaseReward RewardKind { get; init; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ProductEntity Product { get; init; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public UserEntity Organizer { get; init; } = null!;

    public ICollection<GroupPurchasePriceTierEntity> PriceTiers { get; } = new List<GroupPurchasePriceTierEntity>();
    public ICollection<GroupPurchaseParticipantEntity> Participants { get; } = new List<GroupPurchaseParticipantEntity>();
    public ICollection<GroupCoOrganizerEntity> CoOrganizers { get; } = new List<GroupCoOrganizerEntity>();

    public IEnumerable<GroupPurchasePriceTierEntity> GetApplicablePriceTiers() =>
        TargetParticipants is int target
            ? PriceTiers.Where(tier => tier.RequiredParticipants == target)
            : PriceTiers;

    private IEnumerable<GroupPurchaseParticipantEntity> EligibleBuyers(long priceKopecks, Guid? excluding, IReadOnlyDictionary<Guid, long>? balances) =>
        Participants.Where(p => p.UserId != excluding && p.CanBuyAt(priceKopecks, balances) &&
            (Status != EGroupPurchaseStatus.Successful || p.LimitKopecks is null || p.Order is not null));

    public decimal EligibleQuantity(long priceKopecks, Guid? excluding = null, IReadOnlyDictionary<Guid, long>? walletBalances = null) =>
        EligibleBuyers(priceKopecks, excluding, walletBalances).Sum(p => p.Quantity);

    public decimal? RemainingQuantity(long limit, IReadOnlyDictionary<Guid, long> walletBalances, Guid? excluding = null, decimal proposedQuantity = 0) =>
        GetApplicablePriceTiers().Where(tier => tier.PriceKopecks <= limit)
            .Select(tier => (decimal?)Math.Max(0, tier.RequiredParticipants - EligibleQuantity(tier.PriceKopecks, excluding, walletBalances) - proposedQuantity))
            .Min();

    public int CountEligible(long priceKopecks, Guid? excluding = null, IReadOnlyDictionary<Guid, long>? walletBalances = null) =>
        EligibleBuyers(priceKopecks, excluding, walletBalances).Count();

    public GroupPurchasePriceTierEntity? FindReachableTier(Guid? applicantId = null, long? proposedLimit = null,
        IReadOnlyDictionary<Guid, long>? walletBalances = null, decimal applicantQuantity = 1m) =>
        GetApplicablePriceTiers().OrderBy(tier => tier.PriceKopecks).ThenBy(tier => tier.RequiredParticipants)
            .FirstOrDefault(tier => EligibleQuantity(tier.PriceKopecks, applicantId, walletBalances) +
                (proposedLimit >= tier.PriceKopecks && (applicantId is not { } buyer || walletBalances is null ||
                    walletBalances.GetValueOrDefault(buyer) >= PurchaseQuantity.Total(tier.PriceKopecks, applicantQuantity)) ? applicantQuantity : 0m) >= tier.RequiredParticipants);

    public long StartingPriceKopecks => GetApplicablePriceTiers().Max(tier => tier.PriceKopecks);

    public bool HasOrganizerReservation => RewardKind == EGroupPurchaseReward.FreeProduct &&
        OrganizerInitialLimitKopecks is null && TargetParticipants is null;

    public void UpdateOpenStatus(DateTimeOffset now, IReadOnlyDictionary<Guid, long>? walletBalances = null) {
        if (Status != EGroupPurchaseStatus.Open) {
            return;
        }

        if (now >= Deadline) {
            Status = EGroupPurchaseStatus.Cancelled;
            foreach (var participant in Participants) {
                participant.AdjustPrepayment(0, ELimitRefundReason.GroupCancelled);
                if (participant.Order is { PaidKopecks: > 0 } order) {
                    order.RefundedAt ??= now;
                }
            }
        } else if (FindReachableTier(walletBalances: walletBalances) is { } tier) {
            foreach (var participant in Participants) {
                if (participant.LimitKopecks is null) {
                    if (!participant.LimitRevoked && participant.Order is { RefundedAt: null } directOrder) {
                        directOrder.SettlePrice(PurchaseQuantity.Total(tier.PriceKopecks, participant.Quantity));
                    }

                    continue;
                }

                if (participant.CanBuyAt(tier.PriceKopecks, walletBalances)) {
                    participant.RecordOrder(tier.PriceKopecks, now);
                }
                if (participant.LimitDebitsKopecks > 0) {
                    participant.AdjustPrepayment(participant.Order?.PaidKopecks ?? 0,
                        participant.Order is null ? ELimitRefundReason.Unmatched : ELimitRefundReason.PriceDrop);
                }
            }

            if (BonusQuantity.HasValue) {
                BonusAwardedAt = now;
            }

            SettlementPriceKopecks = tier.PriceKopecks;
            SuccessfulAt = now;
            Status = EGroupPurchaseStatus.Successful;
        }
    }

    public GroupPurchaseParticipantEntity AddParticipant(Guid userId, DateTimeOffset joinedAt, long? limitKopecks = null) {
        var participant = new GroupPurchaseParticipantEntity {
            GroupPurchaseId = Id,
            UserId = userId,
            JoinedAt = joinedAt,
            LimitKopecks = limitKopecks
        };

        Participants.Add(participant);
        return participant;
    }

    public void ConfirmDirectParticipation(GroupPurchaseParticipantEntity participant, DateTimeOffset now,
        IReadOnlyDictionary<Guid, long>? walletBalances = null) {
        participant.LimitKopecks = null;
        participant.LimitRevoked = false;
        UpdateOpenStatus(now, walletBalances);
    }
}
