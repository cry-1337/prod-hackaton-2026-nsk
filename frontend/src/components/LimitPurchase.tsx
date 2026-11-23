import { useId, useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Check, Clock3, Minus, Plus } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { emptyAddress, validAddress } from "../address";
import { api, errorMessage } from "../api";
import { queries, refreshGroup } from "../data";
import { deadlineLabel, groupPath, money, maxQuantity, quantityError, readQuantity, totalPrice } from "../format";
import { previewVolume, roundVolume, units, type OrderUnit } from "../groupPricing";
import type { Group, GroupLimit, LimitQuote, PriceTier, Session } from "../types";
import { AddressFields } from "./AddressFields";
import { Dialog } from "./Dialog";
import { HelpTip } from "./HelpTip";

interface Props {
    group: Group;
    tiers: PriceTier[];
    unit: OrderUnit;
    session: Session | null;
    current?: GroupLimit;
    previousPayment?: Group["myPayment"];
    onFinish: () => void;
    onPay: (quantity: number) => void;
}

function Projection({ preview, quote, maximum, quantity, unit }: {
    preview: ReturnType<typeof previewVolume>;
    quote?: LimitQuote;
    maximum: number;
    quantity: number;
    unit: OrderUnit;
}) {
    const reachable = quote ? quote.reachablePriceKopecks : preview?.remaining === 0 ? preview.tier.priceKopecks : null;
    const remaining = quote ? quote.remainingQuantity : preview?.remaining;
    const matches = preview && (reachable != null ? preview.tier.priceKopecks === reachable && preview.remaining === 0 :
        remaining === preview.remaining);
    let title: string;
    let detail: string;

    if (quote && !quote.canAfford) {
        title = "Недостаточно средств";
        detail = preview?.remaining === 0
            ? "После пополнения этой заявки хватит для цены " + money(preview.tier.priceKopecks) + "."
            : "Для подтверждения заявки понадобится предоплата с кошелька.";
    } else if (reachable != null && reachable > maximum) {
        title = "Цена выше вашего максимума";
        detail = "При цене " + money(reachable) + " заказ не оформится и предоплата вернётся полностью.";
    } else if (reachable != null) {
        title = matches ? "С вами наберётся " + units(preview.total, unit) : "Вашей заявки хватит";
        detail = "Сбор сможет завершиться по " + money(reachable) + " за " + unit.per + ".";
    } else if (remaining != null && remaining > 0) {
        title = "Останется набрать ещё " + units(remaining, unit, "take");
        detail = matches
            ? "С вашей заявкой будет " + preview.total.toLocaleString("ru-RU") + " из " + units(preview.tier.requiredQuantity, unit, "of") + " для цены " + money(preview.tier.priceKopecks) + "."
            : "Столько потребуется для цены в пределах вашего максимума.";
    } else {
        title = "Максимум " + money(maximum) + " за " + unit.per;
        detail = "Заявка подойдёт для этой цены или дешевле.";
    }
    const success = reachable != null && reachable <= maximum;
    const refund = quote ? (quote.canAfford ? quote.refundNowKopecks : 0)
        : success ? totalPrice(maximum, quantity) - totalPrice(reachable, quantity) : 0;
    return <div className="limit-projection" role="status">
        <h3><span className="outcome-icon" aria-hidden="true">{success ? <Check size={16} /> : <Clock3 size={16} />}</span>{title}</h3>
        <div className="projection-detail"><p>{detail}</p>
            {refund > 0 && <p>В кошелёк вернётся {money(refund)}.</p>}
            {quote && !quote.canAfford && <Link className="text-link" to="/me#wallet" target="_blank" rel="noopener">Пополнить кошелёк</Link>}
        </div>
    </div>;
}

export function LimitPurchase({ group, tiers, unit, session, current, previousPayment, onFinish, onPay }: Props) {
    const location = useLocation();
    const params = new URLSearchParams(location.search);
    const editing = current?.status === "pending";
    const [amount, setAmount] = useState(() => {
        const preferred = editing ? current.limitKopecks : Number(params.get("limit"));
        return tiers.find(tier => tier.priceKopecks === preferred)?.priceKopecks ?? tiers[0].priceKopecks;
    });
    const [quantityText, setQuantityText] = useState(() =>
        String((editing ? current.quantity : readQuantity(params.get("quantity") ?? "", group.saleUnit)) ?? 1));
    const [address, setAddress] = useState(current?.address ?? previousPayment?.address ?? emptyAddress);
    const [checkout, setCheckout] = useState(false);
    const quantityId = useId();
    const tierName = useId();
    const key = useRef<{ value: string; id: string }>();
    const quantity = readQuantity(quantityText, group.saleUnit);
    const path = groupPath(group.id) + "/limit";
    const quote = useQuery({
        refetchOnWindowFocus: "always",
        queryKey: ["quote", group.id, session?.user.id, amount, quantity],
        enabled: !!session && quantity != null && group.status === "open",
        queryFn: ({ signal }) => api<LimitQuote>(path + "/quote?limitKopecks=" + amount + "&quantity=" + quantity,
            { token: session!.accessToken, signal }),
        staleTime: 0,
        refetchInterval: 10_000
    });
    const change = useMutation({
        mutationFn: (next: number | null) => {
            const body = next == null ? undefined : { limitKopecks: next, quantity, address: validAddress(address) };
            const value = JSON.stringify(body ?? null);
            if (key.current?.value !== value) key.current = { value, id: crypto.randomUUID() };
            return api<GroupLimit>(path, {
                method: next == null ? "DELETE" : "PUT", body, token: session!.accessToken, creationKey: key.current.id
            });
        },
        onSuccess: async result => {
            setCheckout(false);
            refreshGroup(group);
            await queries.invalidateQueries({ queryKey: ["mine", session!.user.id] });
            onFinish();
            if (result.status === "pending") requestAnimationFrame(() => document.getElementById("my-purchase")?.focus());
        },
        onError: () => { void quote.refetch(); void queries.invalidateQueries({ queryKey: ["group", group.id] }); }
    });

    const fullAmount = quantity == null ? null : totalPrice(amount, quantity);
    const forecast = quantity == null ? undefined : previewVolume(tiers, amount, quantity, current);
    const canProceed = quantity != null && (!session || (quote.isSuccess && quote.data.canAfford)) && !change.isPending;
    const min = group.saleUnit === "piece" ? 1 : 0.001;
    const step = group.saleUnit === "piece" ? 1 : 0.1;
    const upper = maxQuantity(group.saleUnit);
    const loginParams = new URLSearchParams(params);
    loginParams.set("limit", String(amount));
    loginParams.set("quantity", String(quantity ?? 1));
    const login = "/login?next=" + encodeURIComponent(location.pathname + "?" + loginParams);

    function stepQuantity(direction: number) {
        const next = roundVolume((quantity ?? 0) + direction * step);
        setQuantityText(String(Math.min(upper, Math.max(min, next))).replace(".", ","));
    }
    function submit(event: FormEvent) {
        event.preventDefault();
        if (canProceed && session && !quote.isFetching && validAddress(address)) change.mutate(amount);
    }
    function closeCheckout() {
        setCheckout(false);
        change.reset();
    }

    return <section className="limit-builder" aria-label={editing ? "Изменить заявку" : "Моя заявка"}>
        <h2>{editing ? "Изменить заявку" : "Моя заявка"}</h2>
        <fieldset className="limit-choices" disabled={change.isPending}>
            <legend><span className="with-hint">Мой максимум за {unit.per}
                <HelpTip label="Как работает лимит">Вы готовы купить по выбранной цене или дешевле. Если сбор завершится дороже вашего максимума, заказ не оформится и вся предоплата вернётся.</HelpTip>
            </span></legend>
            <div>{tiers.map(tier => <label key={tier.priceKopecks}>
                <input type="radio" name={tierName} checked={tier.priceKopecks === amount} onChange={() => setAmount(tier.priceKopecks)} />
                {money(tier.priceKopecks)}
            </label>)}</div>
        </fieldset>
        <div className="limit-quantity">
            <label htmlFor={quantityId}>{unit.quantity}</label>
            <div className="limit-stepper">
                <button type="button" aria-label="Уменьшить количество" disabled={(quantity ?? min) <= min || change.isPending}
                    onClick={() => stepQuantity(-1)}><Minus size={16} /></button>
                <input id={quantityId} inputMode={group.saleUnit === "piece" ? "numeric" : "decimal"}
                    style={{ width: Math.min(13, Math.max(3, quantityText.length + 1)) + "ch" }}
                    value={quantityText} disabled={change.isPending} aria-invalid={quantity == null || undefined}
                    aria-describedby={quantity == null ? quantityId + "-error" : undefined} onChange={event => setQuantityText(event.target.value)} />
                <button type="button" aria-label="Увеличить количество" disabled={(quantity ?? 0) >= upper || change.isPending}
                    onClick={() => stepQuantity(1)}><Plus size={16} /></button>
            </div>
        </div>
        {quantity == null ? <p className="field-error" id={quantityId + "-error"}>{quantityError(quantityText, group.saleUnit)}</p> :
            session && quote.isError ? <p className="form-error" role="alert">{errorMessage(quote.error)} <button type="button" className="text-link" onClick={() => quote.refetch()}>Повторить</button></p> :
            session && !quote.data ? <p className="limit-projection" role="status">Рассчитываем заявку…</p> :
            <Projection preview={forecast} quote={quote.data} unit={unit} maximum={amount} quantity={quantity} />}
        <div className="limit-total">
            <span className="with-hint">{editing ? "Предоплата за заказ" : "Предоплата"}
                <HelpTip label="Как считается предоплата">При подтверждении списывается ваш максимум за всё количество.
                    Когда итоговая цена ниже, разница за весь заказ возвращается в кошелёк.</HelpTip></span>
            <strong>{fullAmount == null ? "—" : money(fullAmount)}</strong>
        </div>
        {session || !quantity ? <button type="button" className="button button-dark limit-continue" aria-haspopup="dialog"
            disabled={!canProceed} onClick={() => { change.reset(); setCheckout(true); void quote.refetch(); }}>{session ? "К оформлению" : "Войти и участвовать"}</button> :
            <Link className="button button-dark limit-continue" to={login}>Войти и участвовать</Link>}
        {editing ? <div className="order-links">
            <button type="button" className="text-link" disabled={change.isPending} onClick={onFinish}>Назад к заявке</button>
            <button type="button" className="text-link" disabled={change.isPending} onClick={() => change.mutate(null)}>Отозвать заявку</button>
        </div> : session && <button type="button" className="text-link direct-alternative" aria-haspopup="dialog"
            disabled={!quantity} onClick={() => { if (quantity) onPay(quantity); }}>Оплатить сейчас</button>}
        {!checkout && change.isError && <p className="form-error" role="alert">{errorMessage(change.error)}</p>}
        {checkout && session && quantity != null && <Dialog title={editing ? "Подтвердить изменения" : "Оформление заявки"}
            onClose={closeCheckout} closeDisabled={change.isPending}>
            <form className="limit-panel" onSubmit={submit}>
                <dl className="checkout-recap">
                    <div><dt>{group.productName}</dt><dd>{units(quantity, unit)} × {money(amount)}</dd></div>
                    <div><dt>{editing ? "К доплате" : "Предоплата"}</dt><dd>{quote.data ? money(quote.data.chargeNowKopecks) : "—"}</dd></div>
                    {!!quote.data?.refundNowKopecks && <div><dt>Вернётся в кошелёк</dt><dd>{money(quote.data.refundNowKopecks)}</dd></div>}
                </dl>
                <AddressFields value={address} onChange={setAddress} disabled={change.isPending}
                    legacy={!current?.address && !previousPayment?.address ? current?.deliveryAddress ?? previousPayment?.deliveryAddress : null} />
                <details className="checkout-details">
                    <summary>Условия возврата</summary>
                    <p>Если итоговая цена окажется ниже вашей, вернём разницу за весь заказ.
                        При цене выше вашего максимума, отмене сбора или отзыве заявки вернём всю предоплату.</p>
                    <p>Сбор открыт до {deadlineLabel(group.deadline)}.</p>
                </details>
                {quote.isError && <p className="form-error" role="alert">{errorMessage(quote.error)} <button type="button" className="text-link" onClick={() => quote.refetch()}>Повторить</button></p>}
                {quote.data && !quote.data.canAfford && <p className="balance-shortfall">Недостаточно средств. <Link className="text-link" to="/me#wallet" target="_blank" rel="noopener">Пополнить кошелёк</Link></p>}
                {change.isError && <p className="form-error" role="alert">{errorMessage(change.error)}</p>}
                <button className="button button-dark" disabled={!canProceed || quote.isFetching}>
                    {change.isPending ? "Сохраняем…" : editing ? "Сохранить" : "Подтвердить предоплату"}
                </button>
            </form>
        </Dialog>}
    </section>;
}
