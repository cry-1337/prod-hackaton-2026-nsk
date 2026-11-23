import { useId } from "react";
import { money, unitLabel } from "../format";
import { roundVolume, units, type OrderUnit } from "../groupPricing";
import type { Group, PriceTier } from "../types";
import { Countdown } from "./Purchase";
import { HelpTip } from "./HelpTip";

function GoalMeter({ tier, pieces, unit }: { tier: PriceTier; pieces: boolean; unit: OrderUnit }) {
    const collected = tier.eligibleQuantity;
    const target = tier.requiredQuantity;
    if (collected == null || target <= 0) return null;
    const value = Math.min(target, Math.max(0, collected));
    const segmented = pieces && Number.isInteger(target) && target <= 12;
    const volume = collected.toLocaleString("ru-RU") + " из " + units(target, unit, "of");

    return <>
        <div className="goal-meter" role="progressbar" aria-label={"Объём для цены " + money(tier.priceKopecks)}
            aria-valuemin={0} aria-valuemax={target} aria-valuenow={value} aria-valuetext={volume}>
            {segmented ? Array.from({ length: target }, (_, index) =>
                <span key={index} className={index < value ? "filled" : undefined} />) :
                <span className="continuous"><i style={{ width: (value / target * 100) + "%" }} /></span>}
        </div>
        <p className="goal-volume">Уже в сборе <strong>{volume}</strong></p>
    </>;
}

export function GroupPrices({ group, tiers, unit }: { group: Group; tiers: PriceTier[]; unit: OrderUnit }) {
    const titleId = useId();
    return <section className="group-prices" aria-labelledby={titleId}>
        <div className="group-prices-heading">
            <h2 id={titleId}>Ход сбора</h2>
            <HelpTip label="Как считаем">Для каждой цены считаем количество товара в подходящих заказах.
                Если покупатель согласен на более высокую цену, его заказ учитывается и для более низкой.
                При достижении нескольких порогов действует самая низкая цена.</HelpTip>
        </div>
        <ul className="price-goals">{tiers.map((tier, index) => {
            const missing = tier.eligibleQuantity == null ? null : Math.max(0, roundVolume(tier.requiredQuantity - tier.eligibleQuantity));
            const goal = missing == null ? "Нужно собрать " + units(tier.requiredQuantity, unit, "take")
                : missing === 0 ? "Нужный объём набран" : "Ещё " + units(missing, unit);
            return <li key={tier.priceKopecks} className="price-goal">
                <h3><strong>{goal}</strong>{" "}
                    <span>{missing === 0 ? "для цены" : "до цены"} <b>{money(tier.priceKopecks)}</b>{index === 0 && " за " + unit.per}</span>
                </h3>
                <GoalMeter tier={tier} pieces={group.saleUnit === "piece"} unit={unit} />
            </li>;
        })}</ul>
        <div className="group-prices-meta"><span>Обычная цена {money(group.regularPriceKopecks)} / {unitLabel(group.saleUnit)}</span><Countdown group={group} /></div>
    </section>;
}
