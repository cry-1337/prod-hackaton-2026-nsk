using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    image_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    regular_price_cents = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invite_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    creation_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    regular_price_cents = table.Column<long>(type: "bigint", nullable: false),
                    reward_kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_purchases", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_purchases_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_purchases_users_organizer_id",
                        column: x => x.organizer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_price_tiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_participants = table.Column<int>(type: "integer", nullable: false),
                    price_cents = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_price_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_price_tiers_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_purchase_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_purchase_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_purchase_participants_group_purchases_group_purchase_~",
                        column: x => x.group_purchase_id,
                        principalTable: "group_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_purchase_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_purchase_price_tiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_participants = table.Column<int>(type: "integer", nullable: false),
                    price_cents = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_purchase_price_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_purchase_price_tiers_group_purchases_group_purchase_id",
                        column: x => x.group_purchase_id,
                        principalTable: "group_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_purchase_participants_user_id",
                table: "group_purchase_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_group_purchase_participants_group_user",
                table: "group_purchase_participants",
                columns: new[] { "group_purchase_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_purchase_price_tiers_group_purchase_id_required_parti~",
                table: "group_purchase_price_tiers",
                columns: new[] { "group_purchase_id", "required_participants" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_purchases_product_id_status_deadline",
                table: "group_purchases",
                columns: new[] { "product_id", "status", "deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_group_purchases_status_deadline",
                table: "group_purchases",
                columns: new[] { "status", "deadline" });

            migrationBuilder.CreateIndex(
                name: "ux_group_purchases_creation_key",
                table: "group_purchases",
                columns: new[] { "organizer_id", "creation_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_group_purchases_invite_code",
                table: "group_purchases",
                column: "invite_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_price_tiers_product_threshold",
                table: "product_price_tiers",
                columns: new[] { "product_id", "required_participants" },
                unique: true);

            var productId = Guid.Parse("019b0000-0000-7000-8000-000000000001");
            migrationBuilder.InsertData(
                table: "products",
                columns: new[] { "id", "name", "description", "regular_price_cents" },
                values: new object[] { productId, "Кофе в зёрнах", "Демонстрационный товар для групповой закупки.", 120_000L });

            migrationBuilder.InsertData(
                table: "product_price_tiers",
                columns: new[] { "id", "product_id", "required_participants", "price_cents" },
                values: new object[,] {
                    { Guid.Parse("019b0000-0000-7000-8000-000000000002"), productId, 3, 99_000L },
                    { Guid.Parse("019b0000-0000-7000-8000-000000000003"), productId, 5, 85_000L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_purchase_participants");

            migrationBuilder.DropTable(
                name: "group_purchase_price_tiers");

            migrationBuilder.DropTable(
                name: "product_price_tiers");

            migrationBuilder.DropTable(
                name: "group_purchases");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
