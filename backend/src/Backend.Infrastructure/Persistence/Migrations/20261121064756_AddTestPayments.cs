using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "confirmed_creation_price_kopecks",
                table: "group_purchases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "card_id",
                table: "group_purchase_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_address",
                table: "group_purchase_participants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "card_id",
                table: "group_limit_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cashback_at",
                table: "group_limit_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "refunded_at",
                table: "group_limit_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "card_id",
                table: "group_limit_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_address",
                table: "group_limit_operations",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_payment_operations",
                columns: table => new
                {
                    group_purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    confirmed_price_kopecks = table.Column<long>(type: "bigint", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_payment_operations", x => new { x.group_purchase_id, x.user_id, x.key });
                    table.ForeignKey(
                        name: "FK_group_payment_operations_group_purchases_group_purchase_id",
                        column: x => x.group_purchase_id,
                        principalTable: "group_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "test_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_four = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_cards", x => x.id);
                    table.ForeignKey(
                        name: "FK_test_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_test_cards_user_id_last_four",
                table: "test_cards",
                columns: new[] { "user_id", "last_four" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_payment_operations");

            migrationBuilder.DropTable(
                name: "test_cards");

            migrationBuilder.DropColumn(
                name: "confirmed_creation_price_kopecks",
                table: "group_purchases");

            migrationBuilder.DropColumn(
                name: "card_id",
                table: "group_purchase_participants");

            migrationBuilder.DropColumn(
                name: "delivery_address",
                table: "group_purchase_participants");

            migrationBuilder.DropColumn(
                name: "card_id",
                table: "group_limit_orders");

            migrationBuilder.DropColumn(
                name: "cashback_at",
                table: "group_limit_orders");

            migrationBuilder.DropColumn(
                name: "refunded_at",
                table: "group_limit_orders");

            migrationBuilder.DropColumn(
                name: "card_id",
                table: "group_limit_operations");

            migrationBuilder.DropColumn(
                name: "delivery_address",
                table: "group_limit_operations");
        }
    }
}
