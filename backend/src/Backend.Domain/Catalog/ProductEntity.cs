using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.Domain.Groups;

namespace Backend.Domain.Catalog;

[Table("products")]
public sealed class ProductEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [MaxLength(256)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(2000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(1024)]
    [Column("image_key")]
    public string? ImageKey { get; set; }

    [Column("sale_unit")]
    public ESaleUnit SaleUnit { get; set; } = ESaleUnit.Piece;

    [Column("regular_price_kopecks")]
    public long RegularPriceKopecks { get; set; }

    [Column("bonus_quantity", TypeName = "numeric(12,3)")]
    public decimal BonusQuantity { get; set; } = 1;

    [MaxLength(16)]
    [Column("bonus_unit")]
    public string BonusUnit { get; set; } = "шт.";

    public ICollection<ProductPriceTierEntity> PriceTiers { get; } = new List<ProductPriceTierEntity>();
    public ICollection<GroupPurchaseEntity> GroupPurchases { get; } = new List<GroupPurchaseEntity>();
}
