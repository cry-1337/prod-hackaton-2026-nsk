using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoOrganizersAndReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "referral_code",
                table: "group_payment_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "referral_code",
                table: "group_limit_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_co_organizers",
                columns: table => new
                {
                    group_purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_co_organizers", x => new { x.group_purchase_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_group_co_organizers_group_purchases_group_purchase_id",
                        column: x => x.group_purchase_id,
                        principalTable: "group_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_co_organizers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_referral_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inviter_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_referral_invites", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_referral_invites_group_purchases_group_purchase_id",
                        column: x => x.group_purchase_id,
                        principalTable: "group_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_referral_invites_users_inviter_id",
                        column: x => x.inviter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "referrals",
                columns: table => new
                {
                    friend_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reward_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reward_amount_kopecks = table.Column<long>(type: "bigint", nullable: true),
                    rewarded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referrals", x => x.friend_id);
                    table.ForeignKey(
                        name: "FK_referrals_group_purchases_reward_group_id",
                        column: x => x.reward_group_id,
                        principalTable: "group_purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_referrals_group_referral_invites_invite_id",
                        column: x => x.invite_id,
                        principalTable: "group_referral_invites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_referrals_users_friend_id",
                        column: x => x.friend_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_co_organizers_user_id",
                table: "group_co_organizers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_referral_invites_group_purchase_id_inviter_id",
                table: "group_referral_invites",
                columns: new[] { "group_purchase_id", "inviter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_referral_invites_inviter_id",
                table: "group_referral_invites",
                column: "inviter_id");

            migrationBuilder.CreateIndex(
                name: "IX_referrals_invite_id",
                table: "referrals",
                column: "invite_id");

            migrationBuilder.CreateIndex(
                name: "IX_referrals_reward_group_id",
                table: "referrals",
                column: "reward_group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_co_organizers");

            migrationBuilder.DropTable(
                name: "referrals");

            migrationBuilder.DropTable(
                name: "group_referral_invites");

            migrationBuilder.DropColumn(
                name: "referral_code",
                table: "group_payment_operations");

            migrationBuilder.DropColumn(
                name: "referral_code",
                table: "group_limit_operations");
        }
    }
}
