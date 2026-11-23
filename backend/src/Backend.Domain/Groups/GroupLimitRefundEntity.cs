using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Domain.Groups;

[Table("group_limit_refunds")]
public sealed class GroupLimitRefundEntity {
    [Key]
    [Column("id")]
    public long Id { get; init; }

    [Column("participant_id")]
    [ForeignKey(nameof(Participant))]
    public Guid ParticipantId { get; init; }

    [Column("reason")]
    public ELimitRefundReason Reason { get; init; }

    [Column("amount_kopecks")]
    public long AmountKopecks { get; init; }

    public GroupPurchaseParticipantEntity Participant { get; init; } = null!;
}
