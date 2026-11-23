using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Users;

[Table("organizer_likes")]
[PrimaryKey(nameof(OrganizerId), nameof(UserId))]
public sealed class OrganizerLikeEntity {
    [Column("organizer_id")]
    public Guid OrganizerId { get; init; }

    [Column("user_id")]
    public Guid UserId { get; init; }

    [ForeignKey(nameof(OrganizerId))]
    [InverseProperty(nameof(UserEntity.OrganizerLikes))]
    public UserEntity Organizer { get; init; } = null!;

    [ForeignKey(nameof(UserId))]
    public UserEntity User { get; init; } = null!;
}
