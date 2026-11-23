using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Users;

[Table("users")]
[Index(nameof(Email), IsUnique = true, Name = "ux_users_email")]
public sealed class UserEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [MaxLength(254)]
    [Column("email")]
    public string Email { get; init; } = null!;

    [MaxLength(64)]
    [Column("nickname")]
    public string Nickname { get; init; } = null!;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    public ICollection<OrganizerLikeEntity> OrganizerLikes { get; } = new List<OrganizerLikeEntity>();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
