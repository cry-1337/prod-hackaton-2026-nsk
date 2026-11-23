using System.ComponentModel.DataAnnotations.Schema;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_co_organizers")]
[PrimaryKey(nameof(GroupPurchaseId), nameof(UserId))]
public sealed class GroupCoOrganizerEntity {
    [Column("group_purchase_id")]
    public Guid GroupPurchaseId { get; init; }

    [Column("user_id")]
    public Guid UserId { get; init; }

    public GroupPurchaseEntity GroupPurchase { get; init; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public UserEntity User { get; init; } = null!;
}
