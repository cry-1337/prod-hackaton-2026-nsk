using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_purchase_price_tiers")]
[Index(nameof(GroupPurchaseId), nameof(RequiredParticipants), IsUnique = true)]
public sealed class GroupPurchasePriceTierEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [Column("group_purchase_id")]
    public Guid GroupPurchaseId { get; init; }

    [Column("required_participants")]
    public int RequiredParticipants { get; init; }

    [Column("price_kopecks")]
    public long PriceKopecks { get; init; }

    public GroupPurchaseEntity GroupPurchase { get; init; } = null!;
}
