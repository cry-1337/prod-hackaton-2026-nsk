using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Domain.Groups;

[Table("group_limit_orders")]
public sealed class GroupOrderEntity {
    [Key]
    [ForeignKey(nameof(Participant))]
    [Column("participant_id")]
    public Guid ParticipantId { get; init; }

    [Column("paid_kopecks")]
    public long PaidKopecks { get; init; }

    [Column("price_refund_kopecks")]
    public long PriceRefundKopecks { get; private set; }

    public void SettlePrice(long finalTotalKopecks) {
        PriceRefundKopecks = Math.Max(0, PaidKopecks - finalTotalKopecks);
    }

    [Column("delivery_address", TypeName = "text")]
    public string DeliveryAddress { get; init; } = null!;

    [Column("paid_at")]
    public DateTimeOffset PaidAt { get; init; }

    [Column("card_id")]
    public Guid? CardId { get; init; }

    [Column("refunded_at")]
    public DateTimeOffset? RefundedAt { get; set; }

    [Column("cashback_at")]
    public DateTimeOffset? CashbackAt { get; set; }

    [Column("notification_read_at")]
    public DateTimeOffset? NotificationReadAt { get; set; }

    public GroupPurchaseParticipantEntity Participant { get; init; } = null!;
}
