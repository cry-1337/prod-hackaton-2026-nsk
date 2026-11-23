using Microsoft.EntityFrameworkCore.Migrations;

namespace Backend.Infrastructure.Persistence.Migrations;

public partial class SeedCatalogSaleUnits : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<int>(
            name: "sale_unit",
            table: "products",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "sale_unit",
            table: "group_purchases",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.Sql("""
            UPDATE products SET description = 'Зерновой кофе, пачка 250 г.'
            WHERE id = '019b0000-0000-7000-8000-000000000001'
              AND description = 'Демонстрационный товар для групповой закупки.';

            INSERT INTO products (id, name, description, image_key, regular_price_kopecks, sale_unit, bonus_quantity, bonus_unit)
            VALUES
                ('019b0000-0000-7000-8000-000000000010', 'Яблоки Гала', 'Хрустящие сладкие яблоки с тонкой кожицей.',
                 'products/gala-apples.webp', 18900, 2, 5, 'кг'),
                ('019b0000-0000-7000-8000-000000000020', 'Груши Конференция', 'Зелёные груши с нежной сочной мякотью.',
                 'products/conference-pears.webp', 26900, 2, 2, 'кг'),
                ('019b0000-0000-7000-8000-000000000030', 'Мёд цветочный', 'Цветочный мёд в стеклянной банке, 450 г.',
                 'products/flower-honey.webp', 59000, 1, 1, 'шт.');

            INSERT INTO product_price_tiers (id, product_id, required_participants, price_kopecks)
            VALUES
                ('019b0000-0000-7000-8000-000000000011', '019b0000-0000-7000-8000-000000000010', 3, 15900),
                ('019b0000-0000-7000-8000-000000000012', '019b0000-0000-7000-8000-000000000010', 5, 13900),
                ('019b0000-0000-7000-8000-000000000021', '019b0000-0000-7000-8000-000000000020', 3, 21900),
                ('019b0000-0000-7000-8000-000000000022', '019b0000-0000-7000-8000-000000000020', 5, 18900),
                ('019b0000-0000-7000-8000-000000000031', '019b0000-0000-7000-8000-000000000030', 3, 49000),
                ('019b0000-0000-7000-8000-000000000032', '019b0000-0000-7000-8000-000000000030', 5, 42000);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.Sql("""
            DELETE FROM products WHERE id IN (
                '019b0000-0000-7000-8000-000000000010',
                '019b0000-0000-7000-8000-000000000020',
                '019b0000-0000-7000-8000-000000000030'
            );

            UPDATE products SET description = 'Демонстрационный товар для групповой закупки.'
            WHERE id = '019b0000-0000-7000-8000-000000000001' AND description = 'Зерновой кофе, пачка 250 г.';
            """);

        migrationBuilder.DropColumn(name: "sale_unit", table: "group_purchases");
        migrationBuilder.DropColumn(name: "sale_unit", table: "products");
    }
}
