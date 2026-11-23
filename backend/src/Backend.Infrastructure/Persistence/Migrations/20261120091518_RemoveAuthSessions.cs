using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations;

public sealed partial class RemoveAuthSessions : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropTable(
            name: "auth_sessions");
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.CreateTable(
            name: "auth_sessions",
            columns: table => new {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => {
                table.PrimaryKey("PK_auth_sessions", x => x.id);
                table.ForeignKey(
                    name: "FK_auth_sessions_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_auth_sessions_user_id_expires_at",
            table: "auth_sessions",
            columns: new[] { "user_id", "expires_at" });
    }
}
