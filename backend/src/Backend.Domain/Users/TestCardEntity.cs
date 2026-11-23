using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Users;

[Table("test_cards")]
[Index(nameof(UserId), nameof(LastFour), IsUnique = true)]
public sealed class TestCardEntity {
    [Key]
    [Column("id")]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    [Column("user_id")]
    public Guid UserId { get; init; }

    [MaxLength(4)]
    [Column("last_four")]
    public string LastFour { get; init; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    public UserEntity User { get; init; } = null!;
}
