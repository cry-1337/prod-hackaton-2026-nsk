using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261120091518_RemoveAuthSessions")]
public sealed partial class RemoveAuthSessions {
    protected override void BuildTargetModel(ModelBuilder modelBuilder) {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.15")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Backend.Domain.Users.UserEntity", b => {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid")
                .HasColumnName("id");

            b.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(254)
                .HasColumnType("character varying(254)")
                .HasColumnName("email");

            b.Property<string>("Nickname")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)")
                .HasColumnName("nickname");

            b.Property<string>("PasswordHash")
                .IsRequired()
                .HasColumnType("text")
                .HasColumnName("password_hash");

            b.HasKey("Id");

            b.HasIndex(new[] { "Email" }, "ux_users_email")
                .IsUnique();

            b.ToTable("users");
        });
#pragma warning restore 612, 618
    }
}
