import { inPractice } from "../practice";
import { HelpTip } from "../components/HelpTip";
import { type FormEvent, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Check, Gift } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { api, errorMessage } from "../api";
import { AddressFields } from "../components/AddressFields";
import { emptyAddress, validAddress } from "../address";
import { PriceTierOptions } from "../components/Purchase";
import { QuantityField } from "../components/QuantityField";
import { Back, Empty, LoadError, Loading, ProductPhoto } from "../components/States";
import { refreshGroup, useBalance, useProduct } from "../data";
import { bonusLabel, deadlineLabel, groupPath, money, orderedTiers, productPath, readQuantity, totalPrice, unitLabel } from "../format";
import type { CreateGroupRequest, Group, Product, Session } from "../types";
import { usePageTitle } from "../usePageTitle";

function localDate(date: Date) {
    return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

const durations = [{ hours: 6, label: "6 часов" }, { hours: 24, label: "Сутки" }, { hours: 72, label: "3 дня" }];

function PracticeBudget({ balance, price, shortfall, product, onQuantity }: {
    balance: number; price: number; shortfall: number; product: Product; onQuantity: (value: string) => void;
}) {
    const maximum = product.saleUnit === "piece" ? Math.floor(balance / price) :
        balance > 0 ? Math.floor((balance * 1000 + 499) / price) / 1000 : 0;
    return <div className="checkout-recap">
        <p role="status">В учебном кошельке {money(balance)}. Для этого заказа не хватает {money(shortfall)}.</p>
        {maximum > 0 && <button type="button" className="text-link justify-self-start" data-guide="budget"
            onClick={() => onQuantity(String(maximum).replace(".", ","))}>
            Взять {maximum.toLocaleString("ru-RU")} {unitLabel(product.saleUnit)}
        </button>}
        {maximum === 0 && <p>Остатка не хватает даже на минимальное количество по этой цене.</p>}
    </div>;
}

function CreateForm({ product, session }: { product: Product; session: Session }) {
    const navigate = useNavigate();
    const balance = useBalance(session);
    const tiers = orderedTiers(product.priceTiers);
    const [price, setPrice] = useState(tiers[0].priceKopecks);
    const [quantityText, setQuantityText] = useState("1");
    const [duration, setDuration] = useState<number | null>(24);
    const [deadline, setDeadline] = useState(() => localDate(new Date(Date.now() + 24 * 3600_000)));
    const [address, setAddress] = useState(emptyAddress);
    const [review, setReview] = useState(false);
    const [validation, setValidation] = useState("");
    const heading = useRef<HTMLHeadingElement>(null);
    const key = useRef<{ payload: string; id: string }>();
    const quantity = readQuantity(quantityText, product.saleUnit);
    const total = quantity == null ? null : totalPrice(price, quantity);
    const shortfall = balance.data && total != null ? Math.max(0, total - balance.data.balanceKopecks) : 0;
    const unit = unitLabel(product.saleUnit);
    const create = useMutation({
        mutationFn: (body: CreateGroupRequest) => {
            const payload = JSON.stringify(body);
            if (key.current?.payload !== payload) key.current = { payload, id: crypto.randomUUID() };
            return api<Group>("/groups", { method: "POST", body, token: session.accessToken, creationKey: key.current.id });
        },
        onSuccess: group => {
            refreshGroup(group);
            navigate(groupPath(group.id), { replace: true });
        }
    });

    function showStep(next: boolean) {
        setReview(next);
        setValidation("");
        create.reset();
        requestAnimationFrame(() => heading.current?.focus());
    }

    function submit(event: FormEvent) {
        event.preventDefault();
        if (create.isPending || !quantity) return;
        const end = new Date(deadline);
        if (!Number.isFinite(end.getTime()) || end.getTime() <= Date.now()) {
            setReview(false);
            setValidation("Выберите срок в будущем.");
            return;
        }
        if (!review) {
            if (inPractice() && (!balance.data || shortfall > 0)) return;
            showStep(true);
            return;
        }
        const delivery = validAddress(address);
        if (shortfall || !delivery) return;
        create.mutate({ productId: product.id, quantity, organizerLimitKopecks: price, deadline: end.toISOString(),
            address: delivery });
    }

    return <div className="create-layout">
        <aside className="create-product">
            <ProductPhoto url={product.imageUrl} name={product.name} className="create-product-photo" />
            <div className="create-product-body">
                <h2>{product.name}</h2>
                <p>Обычная цена · {money(product.regularPriceKopecks)} / {unit}</p>
                <p className="gift-line"><Gift size={16} /> Подарок организатору · {bonusLabel(product.organizerBonus)}</p>
            </div>
        </aside>
        <form className="panel create-form" onSubmit={submit}>
            <ol className="checkout-steps" aria-label="Шаги создания">
                <li aria-current={review ? undefined : "step"}>{review ? <Check size={14} /> : <span>1</span>} Условия</li>
                <li aria-current={review ? "step" : undefined}><span>2</span> Подтверждение</li>
            </ol>
            <h2 tabIndex={-1} ref={heading}>{review ? "Получение и оплата" : "Ваша покупка в сборе"}</h2>
            {!review ? <>
                <fieldset data-guide="price">
                    <legend className="field-title"><span className="with-hint">Куплю до, за 1 {unit}
                        <HelpTip label="Как выбрать свою цену">Это ваша максимальная цена за единицу. Если сбор завершится дешевле, разница вернётся в кошелёк.</HelpTip></span></legend>
                    <PriceTierOptions name="priceTier" unit={product.saleUnit} tiers={tiers}
                        selectedTier={tiers.find(tier => tier.priceKopecks === price)} onChange={tier => setPrice(tier.priceKopecks)} />
                </fieldset>
                <QuantityField unit={product.saleUnit} value={quantityText} onChange={setQuantityText} />
                {inPractice() && shortfall > 0 && balance.data && <PracticeBudget balance={balance.data.balanceKopecks}
                    price={price} shortfall={shortfall} product={product} onQuantity={setQuantityText} />}
                <fieldset>
                    <legend className="field-title"><span className="with-hint">Срок сбора
                            <HelpTip label="Как работает срок">Сбор завершится раньше, если наберётся нужное количество товара. Если срок истечёт раньше, предоплата вернётся.</HelpTip></span></legend>
                    <div className="duration-options" data-guide="duration">
                        {durations.map(item => <button type="button" key={item.hours} aria-pressed={duration === item.hours} onClick={() => {
                            setDuration(item.hours);
                            setDeadline(localDate(new Date(Date.now() + item.hours * 3600_000)));
                            setValidation("");
                        }}>{item.label}</button>)}
                        <button type="button" aria-pressed={duration === null} onClick={() => setDuration(null)}>Своя дата</button>
                    </div>
                    {duration === null && <label className="field mt-2">Дата и время окончания
                        <input type="datetime-local" required min={localDate(new Date())} value={deadline}
                            onChange={event => setDeadline(event.target.value)} />
                    </label>}
                </fieldset>
            </> : <>
                <dl className="checkout-recap">
                    <div><dt>Мой заказ</dt><dd>{quantity?.toLocaleString("ru-RU")} {unit} × {money(price)}</dd></div>
                    <div><dt>Сбор открыт до</dt><dd>{deadlineLabel(new Date(deadline).toISOString())}</dd></div>
                </dl>
                {inPractice() ? (
                    address.house ? <p role="status">Учебный адрес · {address.city}, {address.street}, {address.house}</p> :
                        <button data-guide="address" className="text-link justify-self-start" type="button"
                            onClick={() => setAddress({ city: "Казань", street: "Садовая", house: "12" })}>Подставить учебный адрес</button>
                ) : <AddressFields value={address} onChange={setAddress} disabled={create.isPending} />}
                <p className="field-hint">При снижении общей цены разница за весь заказ вернётся в кошелёк. При отмене вернётся вся сумма.</p>
                {!inPractice() && shortfall > 0 && <p role="status" className="balance-shortfall">Не хватает {money(shortfall)}. <Link className="text-link" to="/me#wallet" target="_blank" rel="noopener">Пополнить кошелёк</Link></p>}
            </>}
            {(validation || create.isError) && <p role="alert" className="form-error">{validation || errorMessage(create.error)}</p>}
            <div className="checkout-buttons">
                {review && <button type="button" className="button button-light" disabled={create.isPending} onClick={() => showStep(false)}>Назад</button>}
                <button data-guide={review ? "confirm" : "review"} className="button button-dark" disabled={create.isPending || !quantity || ((inPractice() || review) && shortfall > 0) || (inPractice() && (!balance.data || (review && !address.house)))}>
                    {create.isPending ? "Создаём…" : review && total != null ? "Создать и оплатить " + money(total) : "Продолжить"}
                </button>
            </div>
        </form>
    </div>;
}

export function CreateGroupPage({ id, session }: { id: string; session: Session }) {
    usePageTitle("Создание сбора");
    const result = useProduct(id);
    const product = result.data;
    return <>
        <Back to={productPath(id)}>К товару</Back>
        <div className="page-heading"><h1>Новый сбор</h1></div>
        {!product ? (result.isError ? <LoadError error={result.error} retry={result.refetch} /> : <Loading />) :
            product.priceTiers.length ? <CreateForm key={product.id} product={product} session={session} /> :
                <Empty title="Для этого товара пока нельзя создать сбор" />}
    </>;
}
