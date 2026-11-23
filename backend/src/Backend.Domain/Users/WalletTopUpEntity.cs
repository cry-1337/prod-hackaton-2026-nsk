using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Users;

[Table("wallet_top_ups")]
[Index(nameof(UserId), nameof(IdempotencyKey), IsUnique = true)]
public sealed class WalletTopUpEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [Column("user_id")]
    public Guid UserId { get; init; }

    [MaxLength(128)]
    [Column("idempotency_key")]
    public string IdempotencyKey { get; init; } = null!;

    [Column("amount_kopecks")]
    public long AmountKopecks { get; init; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    public UserEntity User { get; init; } = null!;
}
