using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_purchase_participants")]
[Index(nameof(GroupPurchaseId), nameof(UserId), IsUnique = true, Name = "ux_group_purchase_participants_group_user")]
public sealed class GroupPurchaseParticipantEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [Column("quantity", TypeName = "numeric(12,3)")]
    public decimal Quantity { get; set; } = 1m;

    [Column("group_purchase_id")]
    public Guid GroupPurchaseId { get; init; }

    [Column("user_id")]
    public Guid UserId { get; init; }

    [Column("joined_at")]
    public DateTimeOffset JoinedAt { get; init; }

    [Column("limit_kopecks")]
    public long? LimitKopecks { get; set; }

    [Column("limit_revoked")]
    public bool LimitRevoked { get; set; }

    [Column("delivery_address", TypeName = "text")]
    public string? DeliveryAddress { get; set; }

    [Column("card_id")]
    public Guid? CardId { get; set; }

    [Column("direct_paid_kopecks")]
    public long? DirectPaidKopecks { get; init; }

    [Column("uses_wallet")]
    public bool UsesWallet { get; set; }

    [Column("limit_debits_kopecks")]
    public long LimitDebitsKopecks { get; private set; }

    [Column("limit_refunds_kopecks")]
    public long LimitRefundsKopecks { get; private set; }

    [NotMapped]
    public long NetPrepayment => LimitDebitsKopecks - LimitRefundsKopecks;

    public ICollection<GroupLimitRefundEntity> Refunds { get; } = [];

    public void AdjustPrepayment(long amount, ELimitRefundReason reason = ELimitRefundReason.LimitChanged) {
        var difference = amount - NetPrepayment;
        if (difference > 0) {
            LimitDebitsKopecks += difference;
        } else if (difference < 0) {
            LimitRefundsKopecks -= difference;
            Refunds.Add(new GroupLimitRefundEntity {
                ParticipantId = Id,
                Reason = reason,
                AmountKopecks = -difference
            });
        }
    }

    public GroupOrderEntity? Order { get; set; }

    public GroupPurchaseEntity GroupPurchase { get; init; } = null!;
    public UserEntity User { get; init; } = null!;

    public bool CanBuyAt(long priceKopecks, IReadOnlyDictionary<Guid, long>? walletBalances) => !LimitRevoked &&
        (LimitKopecks is null || (LimitKopecks >= priceKopecks &&
            (!UsesWallet || Order is not null || NetPrepayment >= PurchaseQuantity.Total(priceKopecks, Quantity) ||
             walletBalances?.GetValueOrDefault(UserId) >= PurchaseQuantity.Total(priceKopecks, Quantity))));

    public void RecordOrder(long priceKopecks, DateTimeOffset now) => Order ??= new GroupOrderEntity {
        ParticipantId = Id,
        PaidKopecks = PurchaseQuantity.Total(priceKopecks, Quantity),
        CardId = CardId,
        DeliveryAddress = DeliveryAddress ?? "Демонстрационный адрес доставки",
        PaidAt = now
    };
}
