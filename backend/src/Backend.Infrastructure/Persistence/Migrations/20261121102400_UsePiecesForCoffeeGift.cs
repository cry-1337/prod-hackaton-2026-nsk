using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Backend.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261121102400_UsePiecesForCoffeeGift")]
public sealed class UsePiecesForCoffeeGift : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.Sql("""
            UPDATE products SET bonus_quantity = 1, bonus_unit = 'шт.'
            WHERE id = '019b0000-0000-7000-8000-000000000001' AND sale_unit = 1
              AND bonus_quantity = 250 AND bonus_unit = 'г';

            UPDATE group_purchases SET bonus_quantity = 1, bonus_unit = 'шт.'
            WHERE product_id = '019b0000-0000-7000-8000-000000000001' AND sale_unit = 1
              AND bonus_quantity = 250 AND bonus_unit = 'г';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.Sql("""
            UPDATE products SET bonus_quantity = 250, bonus_unit = 'г'
            WHERE id = '019b0000-0000-7000-8000-000000000001' AND sale_unit = 1
              AND bonus_quantity = 1 AND bonus_unit = 'шт.';

            UPDATE group_purchases SET bonus_quantity = 250, bonus_unit = 'г'
            WHERE product_id = '019b0000-0000-7000-8000-000000000001' AND sale_unit = 1
              AND bonus_quantity = 1 AND bonus_unit = 'шт.';
            """);
    }
}
