using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "organizer_initial_quantity",
                table: "group_purchases",
                type: "numeric(12,3)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                table: "group_purchase_participants",
                type: "numeric(12,3)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                table: "group_payment_operations",
                type: "numeric(12,3)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                table: "group_limit_operations",
                type: "numeric(12,3)",
                nullable: false,
                defaultValue: 1m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "organizer_initial_quantity",
                table: "group_purchases");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "group_purchase_participants");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "group_payment_operations");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "group_limit_operations");
        }
    }
}
