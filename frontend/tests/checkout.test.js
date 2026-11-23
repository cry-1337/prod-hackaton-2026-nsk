import { expect, it } from "vitest";
import { validAddress } from "../src/address";
import { quantityError, readQuantity, totalPrice } from "../src/format";
import { previewVolume } from "../src/groupPricing";

const tiers = [
    { priceKopecks: 85000, requiredQuantity: 5, eligibleQuantity: 3 },
    { priceKopecks: 99000, requiredQuantity: 3, eligibleQuantity: 1 }
];

it("при двух подходящих порогах показывает самую низкую достижимую цену", () => {
    const offer = previewVolume(tiers, 99000, 2);
    expect(offer).toMatchObject({ tier: tiers[0], total: 5, remaining: 0 });
    expect(totalPrice(99000, 2) - totalPrice(offer.tier.priceKopecks, 2)).toBe(28000);
});

it("при изменении заменяет старый объём участника и соблюдает его максимум", () => {
    const current = { status: "pending", limitKopecks: 99000, chargedKopecks: 99000, quantity: 1 };
    expect(previewVolume(tiers, 85000, 1.125, current)).toMatchObject({
        tier: tiers[0], total: 3.125, remaining: 1.875
    });
    expect(previewVolume(tiers, 80000, 10)).toBeUndefined();
});

it.each([
    ["1,125", "kilogram", 1.125], ["0,001", "kilogram", 0.001], ["1.1234", "kilogram", null],
    ["2", "piece", 2], ["0,5", "piece", null], ["0", "piece", null]
])("проверяет количество %s для %s", (input, unit, expected) => {
    expect(readQuantity(input, unit)).toBe(expected);
});

it("требует город, улицу и номер дома и очищает пробелы перед отправкой", () => {
    const address = { city: " Казань ", street: " 8 Марта ", house: "12А/2" };
    expect(validAddress(address)).toMatchObject({ city: "Казань", street: "8 Марта", house: "12А/2" });
    for (const invalid of [{ city: "" }, { street: "" }, { house: "0" }, { house: "дом" }]) {
        expect(validAddress({ ...address, ...invalid })).toBeNull();
    }
});

it.each([
    ["2147483648", "piece", /999.999.999 шт/],
    ["1000000000,125", "kilogram", /999.999.999,999 кг/],
    ["9".repeat(400), "kilogram", /Невозможное значение/],
    ["1,5", "piece", /целое/],
    ["1,2345", "kilogram", /точностью до грамма/]
])("объясняет причину недопустимого количества %s в %s", (input, unit, message) => {
    expect(readQuantity(input, unit)).toBeNull();
    expect(quantityError(input, unit)).toMatch(message);
});
