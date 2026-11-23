using Microsoft.EntityFrameworkCore.Migrations;

namespace Backend.Infrastructure.Persistence.Migrations;

public partial class SelectGroupPriceTier : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<int>(
            name: "target_participants",
            table: "group_purchases",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(
            name: "target_participants",
            table: "group_purchases");
    }
}
