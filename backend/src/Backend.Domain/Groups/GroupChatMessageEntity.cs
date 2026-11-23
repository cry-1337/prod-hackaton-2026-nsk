using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_chat_messages")]
[Index(nameof(GroupPurchaseId), nameof(Id))]
[Index(nameof(GroupPurchaseId), nameof(AuthorId), nameof(IdempotencyKey), IsUnique = true)]
public sealed class GroupChatMessageEntity {
    [Key]
    [Column("id")]
    public long Id { get; init; }

    [Column("group_purchase_id")]
    public Guid GroupPurchaseId { get; init; }

    [Column("author_id")]
    public Guid AuthorId { get; init; }

    [MaxLength(1000)]
    [Column("text")]
    public string Text { get; init; } = null!;

    [MaxLength(128)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; init; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    public GroupPurchaseEntity GroupPurchase { get; init; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public UserEntity Author { get; init; } = null!;
}
