using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePriceColumnsToKopecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "regular_price_cents",
                table: "products",
                newName: "regular_price_kopecks");

            migrationBuilder.RenameColumn(
                name: "price_cents",
                table: "product_price_tiers",
                newName: "price_kopecks");

            migrationBuilder.RenameColumn(
                name: "regular_price_cents",
                table: "group_purchases",
                newName: "regular_price_kopecks");

            migrationBuilder.RenameColumn(
                name: "price_cents",
                table: "group_purchase_price_tiers",
                newName: "price_kopecks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "regular_price_kopecks",
                table: "products",
                newName: "regular_price_cents");

            migrationBuilder.RenameColumn(
                name: "price_kopecks",
                table: "product_price_tiers",
                newName: "price_cents");

            migrationBuilder.RenameColumn(
                name: "regular_price_kopecks",
                table: "group_purchases",
                newName: "regular_price_cents");

            migrationBuilder.RenameColumn(
                name: "price_kopecks",
                table: "group_purchase_price_tiers",
                newName: "price_cents");
        }
    }
}
