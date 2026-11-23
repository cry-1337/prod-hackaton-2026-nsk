using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreProductImageKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "image_url",
                table: "products",
                newName: "image_key");

            migrationBuilder.Sql("""
                UPDATE products
                SET image_key = 'products/coffee-cerrado.webp'
                WHERE id = '019b0000-0000-7000-8000-000000000001' AND image_key IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE products
                SET image_key = NULL
                WHERE id = '019b0000-0000-7000-8000-000000000001'
                    AND image_key = 'products/coffee-cerrado.webp';
                """);

            migrationBuilder.RenameColumn(
                name: "image_key",
                table: "products",
                newName: "image_url");
        }
    }
}
