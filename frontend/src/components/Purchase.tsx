import { useEffect, useState } from "react";
import { Clock3, Heart, UsersRound } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import {
    bonusLabel,
    countLabel,
    directPurchaseLabel,
    purchaseLabel,
    deadlineLabel,
    groupPath,
    groupTier,
    money,
    orderedTiers,
    productPath,
    statuses,
    unitLabel,
    timeLeft
} from "../format";
import type { Group, GroupStatus, PriceTier, ProductCardData, SaleUnit } from "../types";
import { ProductPhoto } from "./States";

export function Status({ value }: { value: GroupStatus }) {
    return (
        <span className="status" data-status={value}>
            <i aria-hidden="true" />
            {statuses[value]}
        </span>
    );
}

export function Countdown({ group }: { group: Group }) {
    const [now, setNow] = useState(Date.now);
    useEffect(() => {
        if (group.status !== "open") return;
        const timer = window.setInterval(() => setNow(Date.now()), 10_000);
        return () => window.clearInterval(timer);
    }, [group.status]);

    return (
        <time
            dateTime={group.deadline}
            title={deadlineLabel(group.deadline)}
            className="inline-flex items-center gap-1.5"
        >
            <Clock3 size={15} />
            {group.status === "open"
                ? timeLeft(group.deadline, now)
                : "Дедлайн " + deadlineLabel(group.deadline)}
        </time>
    );
}

export function Progress({ group }: { group: Group }) {
    const goal = group.progressTier?.requiredQuantity;
    if (!goal)
        return <p>{countLabel(group.participantsCount, "участник", "участника", "участников")}</p>;
    const quantity = group.eligibleQuantity;
    const unit = unitLabel(group.saleUnit);
    if (group.status === "successful") {
        return <p className="small-meta">Общий заказ · {quantity.toLocaleString("ru-RU")} {unit}</p>;
    }
    return (
        <div className="group-progress">
            <div className="flex items-center justify-between gap-2">
                <span>
                    <UsersRound size={15} /> Объём по этой цене
                </span>
                <strong>
                    {quantity.toLocaleString("ru-RU")} из {goal} {unit}
                </strong>
            </div>
            <progress aria-label="Объём сбора" aria-valuetext={quantity.toLocaleString("ru-RU") + " из " + goal + " " + unit} max={goal} value={quantity} />
        </div>
    );
}

function TierLabel({ tier, unit }: { tier: PriceTier; unit: SaleUnit }) {
    return (
        <span className="tier-label">
            <span>от {tier.requiredQuantity.toLocaleString("ru-RU")} {unitLabel(unit)}</span>
            <strong>{money(tier.priceKopecks)}</strong>
        </span>
    );
}

export function Prices({ tiers, unit }: { tiers: PriceTier[]; unit: SaleUnit }) {
    if (!tiers.length) return null;

    return (
        <div className="tier-panel tier-strip" role="group" aria-label={"Цены за 1 " + unitLabel(unit)}>
            <div className="tier-items">
                {orderedTiers(tiers).map(tier => <TierLabel key={tier.requiredQuantity} tier={tier} unit={unit} />)}
            </div>
        </div>
    );
}

export function PriceTierOptions({ tiers, unit, name, selectedTier, onChange }: {
    tiers: PriceTier[];
    unit: SaleUnit;
    name: string;
    selectedTier: PriceTier | undefined;
    onChange: (tier: PriceTier) => void;
}) {
    if (!tiers.length) return null;

    return (
        <div className="tier-panel tier-options">
            {tiers.map(tier => (
                <label className="tier-choice" key={tier.requiredQuantity}>
                    <input
                        type="radio"
                        name={name}
                        value={tier.priceKopecks}
                        checked={tier.requiredQuantity === selectedTier?.requiredQuantity}
                        onChange={() => onChange(tier)}
                    />
                    <TierLabel tier={tier} unit={unit} />
                </label>
            ))}
        </div>
    );
}

export function ProductCard({
    product,
    choosing = false
}: {
    product: ProductCardData;
    choosing?: boolean;
}) {
    const tier = product.startingTier;
    const saving =
        tier && product.regularPriceKopecks > tier.priceKopecks
            ? product.regularPriceKopecks - tier.priceKopecks
            : 0;
    const path = productPath(product.id) + (choosing ? "/create" : "");
    const label = choosing ? "Создать сбор для товара" : "Открыть товар";
    return (
        <Link data-guide="product" className="product-card" to={path} aria-label={label + " «" + product.name + "»"}>
            <div className="relative product-card-image">
                <ProductPhoto url={product.imageUrl} name={product.name} />
                {saving > 0 && <span className="saving">−{money(saving)}</span>}
            </div>
            <div className="product-card-body">
                <h3>{product.name}</h3>
                <div className={tier ? "tier-panel card-tier mt-auto" : "mt-auto"}>
                    <p className="price-caption">
                        {tier ? (
                            <>В сборе от <span className="whitespace-nowrap">{tier.requiredQuantity} {unitLabel(product.saleUnit)}</span></>
                        ) : "Обычная цена"}
                    </p>
                    <div className="product-prices">
                        <strong className="card-price">
                            {money(tier?.priceKopecks ?? product.regularPriceKopecks)}
                            <small className="unit-price">/ {unitLabel(product.saleUnit)}</small>
                        </strong>
                        {saving > 0 && (
                            <s>
                                <span className="sr-only">Обычная цена </span>
                                {money(product.regularPriceKopecks)}
                            </s>
                        )}
                    </div>
                </div>
            </div>
        </Link>
    );
}

const backLabels: Record<string, string> = {
    "": "На витрину",
    me: "К моим сборам",
    organizers: "К профилю организатора",
    products: "К товару"
};

export function GroupCard({ group, showOrganizer = true }: { group: Group; showOrganizer?: boolean }) {
    const tier = groupTier(group);
    const location = useLocation();
    const section = location.pathname.split("/")[1];
    const people = countLabel(group.participantsCount, "участник", "участника", "участников");
    if (section === "groups") {
        return <Link to={groupPath(group.id)} state={{ from: location.pathname + location.search, fromLabel: "К списку сборов" }}
            className="feed-group" aria-label={group.productName + " · " + group.organizerNickname + " · " + people}>
            <ProductPhoto url={group.imageUrl} name="" className="group-thumb" />
            <div className="feed-group-name"><h3>{group.productName}</h3><small>{group.organizerNickname}</small></div>
            <span className="feed-people"><UsersRound size={16} aria-hidden="true" />{people}</span>
            <Status value={group.status} />
        </Link>;
    }
    const price = group.settlementPriceKopecks ?? tier?.priceKopecks;
    const lowest = group.priceTiers.length ? Math.min(...group.priceTiers.map(item => item.priceKopecks)) : null;
    return (
        <Link
            to={groupPath(group.id)}
            state={{
                from: location.pathname + location.search,
                fromLabel: backLabels[section] ?? "К списку сборов",
                groupOrigin: section === "organizers" ? location.state?.groupOrigin : undefined
            }}
            className="group-card"
            aria-label={"Открыть сбор «" + group.productName + "» · " + group.organizerNickname + " · " + deadlineLabel(group.deadline)}
        >
            <div className="card-status">
                <Status value={group.status} />
            </div>
            <div className="card-product my-5 flex items-center gap-3">
                <ProductPhoto url={group.imageUrl} name="" className="group-thumb" />
                <div className="min-w-0">
                    <h3>{group.productName}</h3>
                    <p className="small-meta mt-1 max-w-full">
                        {showOrganizer ? (
                            <>
                                <span className="min-w-0 [overflow-wrap:anywhere]">{group.organizerNickname}</span>
                                <span className="inline-flex shrink-0 items-center gap-1">
                                    <Heart size={13} aria-label="Лайки" /> {group.organizerLikesCount}
                                </span>
                            </>
                        ) : (
                            <>
                                <UsersRound size={15} aria-hidden="true" />
                                {countLabel(group.participantsCount, "участник", "участника", "участников")}
                            </>
                        )}
                    </p>
                </div>
            </div>
            <div className="tier-panel card-terms">
                <div className="card-amount"><strong>{price == null ? "—" : money(price)}<small className="unit-price">/ {unitLabel(group.saleUnit)}</small></strong>
                    {group.status === "open" && lowest != null && price != null && lowest < price &&
                        <small>Есть ступень за {money(lowest)}</small>}
                </div>
                <Progress group={group} />
            </div>
            {group.status !== "successful" && <div className="card-deadline"><Countdown group={group} /></div>}
            {group.myLimit && <p className="personal-line">{purchaseLabel(group.myLimit, group.saleUnit)}</p>}
            {group.myDirectPaidKopecks != null && <p className="personal-line">{directPurchaseLabel(group)}</p>}
            {group.myBonus && <p className="personal-line">Подарок · {bonusLabel(group.myBonus)}</p>}
        </Link>
    );
}
