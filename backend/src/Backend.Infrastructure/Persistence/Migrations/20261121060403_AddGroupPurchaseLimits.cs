using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupPurchaseLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "settlement_price_kopecks",
                table: "group_purchases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "limit_kopecks",
                table: "group_purchase_participants",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "limit_revoked",
                table: "group_purchase_participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "group_limit_operations",
                columns: table => new
                {
                    group_purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    limit_kopecks = table.Column<long>(type: "bigint", nullable: true),
                    result_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_limit_operations", x => new { x.group_purchase_id, x.user_id, x.key });
                    table.ForeignKey(
                        name: "FK_group_limit_operations_group_purchases_group_purchase_id",
                        column: x => x.group_purchase_id,
                        principalTable: "group_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_limit_orders",
                columns: table => new
                {
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_kopecks = table.Column<long>(type: "bigint", nullable: false),
                    delivery_address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_limit_orders", x => x.participant_id);
                    table.ForeignKey(
                        name: "FK_group_limit_orders_group_purchase_participants_participant_~",
                        column: x => x.participant_id,
                        principalTable: "group_purchase_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_limit_operations");

            migrationBuilder.DropTable(
                name: "group_limit_orders");

            migrationBuilder.DropColumn(
                name: "settlement_price_kopecks",
                table: "group_purchases");

            migrationBuilder.DropColumn(
                name: "limit_kopecks",
                table: "group_purchase_participants");

            migrationBuilder.DropColumn(
                name: "limit_revoked",
                table: "group_purchase_participants");
        }
    }
}
