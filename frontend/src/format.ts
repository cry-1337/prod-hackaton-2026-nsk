import type { SaleUnit, Group, GroupStatus, PriceTier, GroupLimit, ProductBonus } from "./types";

const currency = new Intl.NumberFormat("ru-RU", {
    style: "currency",
    currency: "RUB",
    minimumFractionDigits: 0,
    maximumFractionDigits: 2
});
const date = new Intl.DateTimeFormat("ru-RU", {
    day: "numeric",
    month: "long",
    hour: "2-digit",
    minute: "2-digit"
});
const plural = new Intl.PluralRules("ru-RU");

export const unitLabel = (unit: SaleUnit) => unit === "kilogram" ? "кг" : "шт.";
export const money = (kopecks: number) => currency.format(kopecks / 100);
export const deadlineLabel = (iso: string) => date.format(new Date(iso));
export const groupPath = (id: string) => "/groups/" + encodeURIComponent(id);
export const productPath = (id: string) => "/products/" + encodeURIComponent(id);
export const bonusLabel = (bonus: ProductBonus) => bonus.quantity.toLocaleString("ru-RU") + " " + bonus.unit;

export function purchaseLabel(limit: GroupLimit, unit: SaleUnit) {
    const quantity = (limit.quantity ?? 1).toLocaleString("ru-RU") + " " + unitLabel(unit);
    const refund = limit.refundedKopecks ? " · Возвращено " + money(limit.refundedKopecks) : "";
    if (limit.status === "purchased") return "Куплено " + quantity + " за " + money(limit.chargedKopecks) + refund;
    if (limit.status === "pending") {
        const remaining = limit.remainingQuantity == null ? "" : " · Осталось собрать " + limit.remainingQuantity.toLocaleString("ru-RU") + " " + unitLabel(unit);
        return "Куплю до " + money(limit.limitKopecks) + " за " + unitLabel(unit) + " · " + quantity +
            " · Предоплата " + money(limit.chargedKopecks) + remaining;
    }
    return (limit.status === "closed" ? "Заявка закрыта" : "Заявка отозвана") + refund +
        (limit.chargedKopecks > 0 ? " · Возврат в обработке" : "");
}

export function directPurchaseLabel(group: Group) {
    const paid = group.myPayment?.paidKopecks ?? group.myDirectPaidKopecks ?? 0;
    const refunded = group.myPayment?.refundedKopecks ?? 0;
    const quantity = (group.myPayment?.quantity ?? 1).toLocaleString("ru-RU") + " " + unitLabel(group.saleUnit);
    if (group.status === "cancelled") return quantity + (refunded ? " · Возвращено " + money(refunded) : " · Возврат в обработке");
    return quantity + " · Оплачено " + money(paid - refunded) + (refunded ? " · Возвращено " + money(refunded) : "");
}

export function parseRubles(value: string) {
    const input = value.trim().replace(",", ".");
    if (!/^\d{1,10}(?:\.\d{1,2})?$/.test(input)) return undefined;
    const [whole, fraction = ""] = input.split(".");
    const amount = Number(whole) * 100 + Number(fraction.padEnd(2, "0"));
    return amount > 0 ? amount : undefined;
}
export const statuses: Record<GroupStatus, string> = {
    open: "Идёт сбор",
    successful: "Доставлен",
    cancelled: "Отменён"
};

export function countLabel(count: number, one: string, few: string, many: string) {
    const form = plural.select(count);
    return count + " " + (form === "one" ? one : form === "few" ? few : many);
}

export function orderedTiers(tiers: PriceTier[]) {
    return [...tiers].sort((a, b) => a.requiredQuantity - b.requiredQuantity);
}

export function groupTier(group: Group) {
    return group.progressTier;
}

export function timeLeft(deadline: string, now: number) {
    const minutes = Math.ceil((Date.parse(deadline) - now) / 60_000);
    if (minutes <= 0) return "Срок истёк";
    if (minutes < 60) return "Ещё " + minutes + " мин";
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return "Ещё " + hours + " ч " + (minutes % 60) + " мин";
    return "Ещё " + Math.floor(hours / 24) + " д " + (hours % 24) + " ч";
}

export function totalPrice(unitPrice: number, quantity: number) {
    const thousandths = BigInt(Math.round(quantity * 1000));
    return unitPrice === 0 ? 0 : Math.max(1, Number((BigInt(unitPrice) * thousandths + 500n) / 1000n));
}

export function maxQuantity(unit: SaleUnit) {
    return unit === "piece" ? 999_999_999 : 999_999_999.999;
}

export function quantityError(value: string, unit: SaleUnit) {
    const amount = Number(value.replace(",", "."));
    const max = maxQuantity(unit);
    if (amount > max || amount < 0) {
        return "Невозможное значение. Введите от " + (unit === "piece" ? "1" : "0,001") +
            " до " + max.toLocaleString("ru-RU") + " " + unitLabel(unit) + ".";
    }
    return unit === "piece" ? "Введите целое положительное количество." : "Введите вес в кг с точностью до грамма.";
}

export function readQuantity(value: string, unit: SaleUnit) {
    const pattern = unit === "piece" ? /^\d+$/ : /^\d+(?:[.,]\d{1,3})?$/;
    const amount = Number(value.replace(",", "."));
    return pattern.test(value) && amount > 0 && amount <= maxQuantity(unit) ? amount : null;
}
