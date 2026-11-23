using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLimitPrepayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "limit_debits_kopecks",
                table: "group_purchase_participants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "limit_refunds_kopecks",
                table: "group_purchase_participants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "limit_debits_kopecks",
                table: "group_purchase_participants");

            migrationBuilder.DropColumn(
                name: "limit_refunds_kopecks",
                table: "group_purchase_participants");
        }
    }
}
