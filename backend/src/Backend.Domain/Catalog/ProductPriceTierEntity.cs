using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Catalog;

[Table("product_price_tiers")]
[Index(nameof(ProductId), nameof(RequiredParticipants), IsUnique = true, Name = "ux_product_price_tiers_product_threshold")]
public sealed class ProductPriceTierEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [Column("product_id")]
    public Guid ProductId { get; init; }

    [Column("required_participants")]
    public int RequiredParticipants { get; set; }

    [Column("price_kopecks")]
    public long PriceKopecks { get; set; }

    public ProductEntity Product { get; init; } = null!;
}
