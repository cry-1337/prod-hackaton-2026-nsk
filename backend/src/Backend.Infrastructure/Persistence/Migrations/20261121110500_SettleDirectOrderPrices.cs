using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Backend.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261121110500_SettleDirectOrderPrices")]
public sealed class SettleDirectOrderPrices : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<long>(
            name: "price_refund_kopecks", table: "group_limit_orders", type: "bigint",
            nullable: false, defaultValue: 0L);

        migrationBuilder.Sql("""
            UPDATE group_limit_orders AS receipt
            SET price_refund_kopecks = GREATEST(0, receipt.paid_kopecks -
                CASE WHEN g.settlement_price_kopecks = 0 THEN 0
                     ELSE GREATEST(1, ROUND(g.settlement_price_kopecks * p.quantity)) END)
            FROM group_purchase_participants AS p
            JOIN group_purchases AS g ON g.id = p.group_purchase_id
            WHERE receipt.participant_id = p.id AND g.status = 2 AND g.settlement_price_kopecks IS NOT NULL
              AND p.limit_kopecks IS NULL AND NOT p.limit_revoked AND receipt.refunded_at IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "price_refund_kopecks", table: "group_limit_orders");
    }
}
