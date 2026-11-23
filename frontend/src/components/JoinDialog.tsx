import { useEffect, useId, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { X } from "lucide-react";
import { api, errorMessage } from "../api";
import { refreshGroup } from "../data";
import { readQuantity, groupPath, money, unitLabel } from "../format";
import type { DeliveryAddress, Group, Session } from "../types";
import { AddressFields } from "./AddressFields";
import { emptyAddress, validAddress } from "../address";
import { QuantityField } from "./QuantityField";

interface Props {
    group: Group;
    session: Session;
    initialQuantity: number;
    prepayment: number;
    initialAddress?: DeliveryAddress | null;
    legacyAddress?: string | null;
    onClose: () => void;
}

export function JoinDialog({ group, session, initialQuantity, prepayment, initialAddress, legacyAddress, onClose }: Props) {
    const ref = useRef<HTMLDialogElement>(null);
    const key = useRef<{ payload: string; id: string }>();
    const title = useId();
    const [quantityText, setQuantityText] = useState(String(initialQuantity));
    const [address, setAddress] = useState(initialAddress ?? emptyAddress);
    const quantity = readQuantity(quantityText, group.saleUnit);
    const path = groupPath(group.id) + "/payment";
    const quote = useQuery({
        queryKey: ["paymentQuote", group.id, session.user.id, quantity],
        enabled: !!quantity,
        queryFn: ({ signal }) => api<{ priceKopecks: number; totalKopecks: number }>(
            path + "/quote?quantity=" + quantity, { token: session.accessToken, signal })
    });
    const payment = useMutation({
        mutationFn: async () => {
            const body = { confirmedPriceKopecks: quote.data!.priceKopecks, address: validAddress(address), quantity };
            const payload = JSON.stringify(body);
            if (key.current?.payload !== payload) key.current = { payload, id: crypto.randomUUID() };
            return api<Group>(path, { method: "POST", body, token: session.accessToken, creationKey: key.current.id });
        },
        onSuccess: updated => { refreshGroup(updated); onClose(); },
        onError: () => { void quote.refetch(); }
    });
    useEffect(() => {
        const dialog = ref.current;
        dialog?.showModal();
        return () => dialog?.close();
    }, []);

    return <dialog ref={ref} className="join-dialog" aria-labelledby={title} onCancel={event => {
        event.preventDefault();
        if (!payment.isPending) onClose();
    }}>
        <div className="flex items-start justify-between gap-3">
            <h2 id={title}>Оплатить заказ</h2>
            <button className="icon-button" aria-label="Закрыть" disabled={payment.isPending} onClick={onClose}><X size={19} /></button>
        </div>
        <form className="form-stack mt-3" onSubmit={event => {
            event.preventDefault();
            if (quantity && quote.data && validAddress(address) && !payment.isPending) payment.mutate();
        }}>
            <strong>{group.productName}</strong>
            <QuantityField unit={group.saleUnit} value={quantityText} onChange={setQuantityText} disabled={payment.isPending} />
            <AddressFields value={address} onChange={setAddress} disabled={payment.isPending} legacy={!initialAddress ? legacyAddress : null} />
            {quote.data && <p>{money(quote.data.priceKopecks)} / {unitLabel(group.saleUnit)} · Итого {money(quote.data.totalKopecks)}</p>}
            {prepayment > 0 && <p className="action-note">Предоплата {money(prepayment)} вернётся в кошелёк и будет доступна для этой оплаты.</p>}
            <p className="action-note">Если общая цена снизится, вернём разницу за каждую единицу. При отмене вернём всю сумму.</p>
            {(quote.isError || payment.isError) && <p className="form-error" role="alert">{errorMessage(payment.error ?? quote.error)}</p>}
            <button className="button button-dark" disabled={!quantity || !quote.isSuccess || payment.isPending}>
                {payment.isPending ? "Оплачиваем…" : "Оплатить с кошелька"}
            </button>
        </form>
    </dialog>;
}
