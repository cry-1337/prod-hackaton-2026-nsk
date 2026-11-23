using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Groups;

[Table("group_referral_invites")]
[Index(nameof(GroupPurchaseId), nameof(InviterId), IsUnique = true)]
public sealed class GroupReferralInviteEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Column("group_purchase_id")]
    public Guid GroupPurchaseId { get; init; }

    [Column("inviter_id")]
    public Guid InviterId { get; init; }

    public GroupPurchaseEntity GroupPurchase { get; init; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public UserEntity Inviter { get; init; } = null!;
}
