using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Users;

[Table("referrals")]
public sealed class ReferralEntity {
    [Key]
    [ForeignKey(nameof(Friend))]
    [Column("friend_id")]
    public Guid FriendId { get; init; }

    [Column("invite_id")]
    public Guid InviteId { get; init; }

    [Column("reward_group_id")]
    public Guid? RewardGroupId { get; set; }

    [Column("reward_amount_kopecks")]
    public long? RewardAmountKopecks { get; set; }

    [Column("rewarded_at")]
    public DateTimeOffset? RewardedAt { get; set; }

    public UserEntity Friend { get; init; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GroupReferralInviteEntity Invite { get; init; } = null!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GroupPurchaseEntity? RewardGroup { get; set; }
}
