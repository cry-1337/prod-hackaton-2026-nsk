using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Backend.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261121120241_StoreStructuredDeliveryAddresses")]
public sealed class StoreStructuredDeliveryAddresses : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_purchase_participants",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(512)",
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_payment_operations",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(512)",
            oldMaxLength: 512);

        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_limit_orders",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(512)",
            oldMaxLength: 512);

        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_limit_operations",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(512)",
            oldMaxLength: 512,
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_purchase_participants",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_payment_operations",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_limit_orders",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AlterColumn<string>(
            name: "delivery_address",
            table: "group_limit_operations",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }
}
