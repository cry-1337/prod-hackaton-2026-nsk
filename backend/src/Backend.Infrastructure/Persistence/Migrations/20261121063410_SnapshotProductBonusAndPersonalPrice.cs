using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    public partial class SnapshotProductBonusAndPersonalPrice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "bonus_quantity",
                table: "products",
                type: "numeric(12,3)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "bonus_unit",
                table: "products",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "шт.");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "bonus_awarded_at",
                table: "group_purchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "bonus_quantity",
                table: "group_purchases",
                type: "numeric(12,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bonus_unit",
                table: "group_purchases",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "organizer_initial_limit_kopecks",
                table: "group_purchases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "direct_paid_kopecks",
                table: "group_purchase_participants",
                type: "bigint",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE products SET bonus_quantity = 250, bonus_unit = 'г'
                WHERE id = '019b0000-0000-7000-8000-000000000001';

                UPDATE group_purchases AS g
                SET settlement_price_kopecks = (
                    SELECT MIN(t.price_kopecks)
                    FROM group_purchase_price_tiers AS t
                    WHERE t.group_purchase_id = g.id
                      AND (g.target_participants IS NULL OR t.required_participants = g.target_participants)
                      AND t.required_participants <= (
                          SELECT COUNT(*) FROM group_purchase_participants AS p WHERE p.group_purchase_id = g.id
                      )
                )
                WHERE g.status = 2 AND g.settlement_price_kopecks IS NULL;

                ALTER TABLE products ADD CONSTRAINT ck_products_bonus CHECK (bonus_quantity > 0 AND trim(bonus_unit) <> '');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE products DROP CONSTRAINT ck_products_bonus");

            migrationBuilder.DropColumn(
                name: "bonus_quantity",
                table: "products");

            migrationBuilder.DropColumn(
                name: "bonus_unit",
                table: "products");

            migrationBuilder.DropColumn(
                name: "bonus_awarded_at",
                table: "group_purchases");

            migrationBuilder.DropColumn(
                name: "bonus_quantity",
                table: "group_purchases");

            migrationBuilder.DropColumn(
                name: "bonus_unit",
                table: "group_purchases");

            migrationBuilder.DropColumn(
                name: "organizer_initial_limit_kopecks",
                table: "group_purchases");

            migrationBuilder.DropColumn(
                name: "direct_paid_kopecks",
                table: "group_purchase_participants");
        }
    }
}
