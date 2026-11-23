import { orderedTiers } from "./format";
import type { Group, GroupLimit, PriceTier, SaleUnit } from "./types";

const unitForms = {
    pack: { one: "пачка", few: "пачки", many: "пачек", per: "пачку", quantity: "Сколько пачек беру" },
    jar: { one: "банка", few: "банки", many: "банок", per: "банку", quantity: "Сколько банок беру" },
    piece: { one: "штука", few: "штуки", many: "штук", per: "штуку", quantity: "Сколько штук беру" },
    kilogram: { one: "кг", few: "кг", many: "кг", per: "кг", quantity: "Сколько кг беру" }
};

export type OrderUnit = typeof unitForms.piece;
const plural = new Intl.PluralRules("ru-RU");

export function orderUnit(saleUnit: SaleUnit, description?: string | null): OrderUnit {
    if (saleUnit === "kilogram") return unitForms.kilogram;
    if (description && /(?:^|[^\p{L}])пачк[аеу](?!\p{L})/iu.test(description)) return unitForms.pack;
    if (description && /(?:^|[^\p{L}])банк[аеу](?!\p{L})/iu.test(description)) return unitForms.jar;
    return unitForms.piece;
}

export function units(value: number, unit: OrderUnit, form: "count" | "take" | "of" = "count") {
    const count = plural.select(value);
    let noun = count === "one" ? unit.one : count === "few" ? unit.few : unit.many;
    if (form === "take" && count === "one") noun = unit.per;
    if (form === "of") noun = count === "one" ? unit.few : unit.many;
    return value.toLocaleString("ru-RU") + " " + noun;
}

export function groupPrices(group: Group) {
    const prices = new Map<number, PriceTier>();
    for (const tier of orderedTiers(group.priceTiers)) {
        if (prices.has(tier.priceKopecks)) continue;
        const isProgressTier = tier.priceKopecks === group.progressTier?.priceKopecks &&
            tier.requiredQuantity === group.progressTier.requiredQuantity;
        prices.set(tier.priceKopecks, {
            ...tier,
            eligibleQuantity: tier.eligibleQuantity ?? (isProgressTier ? group.eligibleQuantity : undefined)
        });
    }
    return [...prices.values()].sort((a, b) => a.priceKopecks - b.priceKopecks);
}

export function roundVolume(value: number) {
    return Math.round(value * 1000) / 1000;
}

export function previewVolume(tiers: PriceTier[], maximum: number, quantity: number, current?: GroupLimit) {
    const editing = current?.status === "pending";
    if (editing && current.chargedKopecks === 0) return undefined;

    const proposals = tiers.filter(tier => tier.priceKopecks <= maximum && tier.eligibleQuantity != null).map(tier => {
        const previous = editing && current.limitKopecks >= tier.priceKopecks ? current.quantity ?? 1 : 0;
        const total = roundVolume(Math.max(0, tier.eligibleQuantity! - previous) + quantity);
        return { tier, total, remaining: Math.max(0, roundVolume(tier.requiredQuantity - total)) };
    });

    return proposals.sort((a, b) => a.remaining - b.remaining || a.tier.priceKopecks - b.tier.priceKopecks)[0];
}
