import { inPractice } from "../practice";
import { useState } from "react";
import { Heart } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { GroupChat } from "../components/GroupChat";
import { GroupPrices } from "../components/GroupPrices";
import { JoinDialog } from "../components/JoinDialog";
import { LimitPurchase } from "../components/LimitPurchase";
import { Participants } from "../components/Participants";
import { PendingPurchase } from "../components/PendingPurchase";
import { Countdown, Progress, Status } from "../components/Purchase";
import { Share } from "../components/Share";
import { LoadError, Loading, ProductPhoto } from "../components/States";
import { useGroup, useMyGroups, useProduct } from "../data";
import { directPurchaseLabel, money, productPath, purchaseLabel, unitLabel } from "../format";
import { groupPrices, orderUnit } from "../groupPricing";
import type { Group } from "../types";
import { useSession } from "../session";
import { usePageTitle } from "../usePageTitle";

function OrderReceipt({ personal }: { personal: Group }) {
    const limit = personal.myLimit;
    const payment = personal.myPayment;
    const finalPrice = personal.settlementPriceKopecks;
    const settled = personal.status === "successful" && finalPrice != null && payment;
    const refunded = limit?.refundedKopecks ?? payment?.refundedKopecks ?? 0;

    return <section className="group-receipt" aria-label="Мой заказ">
        <h2>Мой заказ</h2>
        {settled ? <dl className="checkout-recap">
            <div><dt>{payment.quantity.toLocaleString("ru-RU")} {unitLabel(personal.saleUnit)} × {money(finalPrice)}</dt>
                <dd>{money(limit?.chargedKopecks ?? (payment.paidKopecks - refunded))}</dd></div>
            {refunded > 0 && <div><dt>Вернулось в кошелёк</dt><dd>{money(refunded)}</dd></div>}
        </dl> : <p>{limit ? purchaseLabel(limit, personal.saleUnit) : directPurchaseLabel(personal)}</p>}
    </section>;
}

export function GroupPage({ id }: { id: string }) {
    const location = useLocation();
    const result = useGroup(id);
    const group = result.data;
    usePageTitle(group ? group.productName + " · Сбор" : "Сбор");
    const back = <Link className="back-link" to={location.state?.from ?? "/groups"} state={{ groupOrigin: location.state?.groupOrigin }}>
        {location.state?.fromLabel ?? "К списку сборов"}
    </Link>;
    return <div className="group-detail">{back}{group ? <GroupDetail group={group} /> :
        result.isError ? <LoadError error={result.error} retry={result.refetch} /> : <Loading />}</div>;
}

function GroupDetail({ group }: { group: Group }) {
    const location = useLocation();
    const session = useSession();
    const mine = useMyGroups(session);
    const product = useProduct(group.productId);
    const [editing, setEditing] = useState(false);
    const [payQuantity, setPayQuantity] = useState<number | null>(null);
    const personal = mine.data?.find(item => item.id === group.id);
    const limit = personal?.myLimit;
    const open = group.status === "open";
    const waiting = open && limit?.status === "pending";
    const direct = personal?.myDirectPaidKopecks != null || (!!personal?.myPayment && !limit);
    const price = group.settlementPriceKopecks ?? group.progressTier?.priceKopecks ?? (open ? group.regularPriceKopecks : null);
    const origin = "?group=" + encodeURIComponent(group.id);
    const isOwner = group.organizerId === session?.user.id;
    const here = location.pathname + location.search;
    const canWrite = isOwner || (!!personal && limit?.status !== "revoked");
    const tiers = groupPrices(group);
    const unit = orderUnit(group.saleUnit, product.data?.description);

    function orderPanel() {
        if (session && mine.isError) return <LoadError error={mine.error} retry={mine.refetch} />;
        if (session && mine.isPending) return <Loading />;
        if (inPractice() && personal) return <OrderReceipt personal={personal} />;
        if (waiting && !editing) {
            return <PendingPurchase group={group} limit={limit!} onEdit={() => setEditing(true)}
                onPay={() => setPayQuantity(limit?.quantity ?? 1)} />;
        }
        if (open && !direct && tiers.length > 0) {
            return <LimitPurchase key={group.id + (session?.user.id ?? "guest") + (waiting ? "-edit" : "-new")}
                group={group} tiers={tiers} unit={unit} session={session} current={limit} previousPayment={personal?.myPayment}
                onFinish={() => setEditing(false)} onPay={setPayQuantity} />;
        }
        if (personal && (limit || direct)) return <OrderReceipt personal={personal} />;
        if (!open) return null;
        return <div className="group-join">{session ?
            <button type="button" className="button button-dark" onClick={() => setPayQuantity(1)}>Оплатить сейчас</button> :
            <Link className="button button-dark" to={"/login?next=" + encodeURIComponent(here)}>Войти и участвовать</Link>}</div>;
    }

    return <>
        <header className="group-intro">
            <ProductPhoto url={group.imageUrl} name="" className="group-intro-photo" />
            <div className="group-intro-copy">
                <div className="group-toolbar"><Status value={group.status} /><span className="group-code">#{group.inviteCode}</span></div>
                <h1>{group.productName}</h1>
                <div className="group-intro-meta">
                    <Link className="text-link" to={productPath(group.productId) + origin}
                        state={{ groupId: group.id, groupOrigin: location.state }}>О товаре</Link>
                    <Link className="group-host" to={isOwner ? "/me?role=organizer" : "/organizers/" + group.organizerId + origin}
                        state={{ groupPath: here, groupOrigin: location.state }}>
                        <span>Организатор <strong>{group.organizerNickname}</strong></span>
                        <span className="likes-count"><Heart size={14} aria-label="Лайки" /> {group.organizerLikesCount}</span>
                    </Link>
                </div>
            </div>
            <Share group={group} />
        </header>
        <div className="group-order-layout">
            {open && tiers.length > 0 ? <GroupPrices group={group} tiers={tiers} unit={unit} /> :
                <section className="purchase-meter">
                    <span className="price-caption">{open ? "Обычная цена" : group.status === "successful" ? "Итоговая цена для всех" : "Цена к окончанию сбора"}</span>
                    <div className="price-line">
                        <strong>{price == null ? "—" : money(price)}<small className="unit-price">/ {unitLabel(group.saleUnit)}</small></strong>
                        {price != null && price < group.regularPriceKopecks && <s aria-label="Обычная цена">Обычная цена {money(group.regularPriceKopecks)}</s>}
                    </div>
                    <Progress group={group} />
                    {open && <div className="meter-meta"><Countdown group={group} /></div>}
                </section>}
            {orderPanel()}
        </div>
        <div className="group-community">
            <Participants group={group} />
            <GroupChat key={group.id} groupId={group.id} session={session} canWrite={canWrite} />
        </div>
        {open && session && payQuantity != null && <JoinDialog group={group} session={session} initialQuantity={payQuantity}
            prepayment={limit?.chargedKopecks ?? 0} initialAddress={limit?.address ?? personal?.myPayment?.address}
            legacyAddress={limit?.deliveryAddress ?? personal?.myPayment?.deliveryAddress} onClose={() => setPayQuantity(null)} />}
    </>;
}
