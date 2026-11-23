using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_payment_operations")]
[PrimaryKey(nameof(GroupPurchaseId), nameof(UserId), nameof(Key))]
public sealed class GroupPaymentOperationEntity {
    [Column("quantity", TypeName = "numeric(12,3)")]
    public decimal Quantity { get; init; } = 1m;

    [Column("group_purchase_id")]
    public Guid GroupPurchaseId { get; init; }

    [Column("user_id")]
    public Guid UserId { get; init; }

    [MaxLength(128)]
    [Column("key")]
    public string Key { get; init; } = null!;

    [Column("confirmed_price_kopecks")]
    public long ConfirmedPriceKopecks { get; init; }

    [Column("card_id")]
    public Guid? CardId { get; init; }

    [Column("delivery_address", TypeName = "text")]
    public string DeliveryAddress { get; init; } = null!;

    [Column("referral_code")]
    public Guid? ReferralCode { get; init; }

    [Column("result_json")]
    public string ResultJson { get; init; } = null!;

    public GroupPurchaseEntity GroupPurchase { get; init; } = null!;
}
