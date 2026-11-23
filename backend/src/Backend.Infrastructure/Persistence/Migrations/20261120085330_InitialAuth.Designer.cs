using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261120085330_InitialAuth")]
public sealed partial class InitialAuth {
    protected override void BuildTargetModel(ModelBuilder modelBuilder) {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.15")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Backend.Domain.Users.AuthSessionEntity", b => {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid")
                .HasColumnName("id");

            b.Property<DateTimeOffset>("ExpiresAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("expires_at");

            b.Property<Guid>("UserId")
                .HasColumnType("uuid")
                .HasColumnName("user_id");

            b.HasKey("Id");

            b.HasIndex("UserId", "ExpiresAt");

            b.ToTable("auth_sessions");
        });

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

        modelBuilder.Entity("Backend.Domain.Users.AuthSessionEntity", b => {
            b.HasOne("Backend.Domain.Users.UserEntity", "User")
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("User");
        });
#pragma warning restore 612, 618
    }
}
