using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveGroupCheckouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "creation_checkout_hash",
                table: "group_purchases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO group_limit_orders (participant_id, paid_kopecks, paid_at, delivery_address, card_id, refunded_at)
                SELECT p.id, p.direct_paid_kopecks, p.joined_at,
                       COALESCE(p.delivery_address, 'Демонстрационный адрес доставки'), p.card_id,
                       CASE WHEN g.status = 3 AND p.direct_paid_kopecks > 0 THEN g.deadline END
                FROM group_purchase_participants p
                JOIN group_purchases g ON g.id = p.group_purchase_id
                WHERE p.direct_paid_kopecks IS NOT NULL
                ON CONFLICT (participant_id) DO NOTHING;

                UPDATE group_purchases g
                SET bonus_quantity = product.bonus_quantity, bonus_unit = product.bonus_unit
                FROM products product
                WHERE g.product_id = product.id AND g.status = 1 AND g.bonus_quantity IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "creation_checkout_hash",
                table: "group_purchases");
        }
    }
}
