using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_limit_operations")]
[PrimaryKey(nameof(GroupPurchaseId), nameof(UserId), nameof(Key))]
public sealed class GroupLimitOperationEntity {
    [Column("quantity", TypeName = "numeric(12,3)")]
    public decimal Quantity { get; init; } = 1m;

    [Column("group_purchase_id")]
    public Guid GroupPurchaseId { get; init; }

    [Column("user_id")]
    public Guid UserId { get; init; }

    [MaxLength(128)]
    [Column("key")]
    public string Key { get; init; } = null!;

    [Column("limit_kopecks")]
    public long? LimitKopecks { get; init; }

    [Column("card_id")]
    public Guid? CardId { get; init; }

    [Column("delivery_address", TypeName = "text")]
    public string? DeliveryAddress { get; init; }

    [Column("referral_code")]
    public Guid? ReferralCode { get; init; }

    [Column("result_json")]
    public string ResultJson { get; init; } = null!;

    public GroupPurchaseEntity GroupPurchase { get; init; } = null!;
}
