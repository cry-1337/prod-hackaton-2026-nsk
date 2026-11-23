import { Clock3, ReceiptText } from "lucide-react";
import { Link } from "react-router-dom";
import { deadlineLabel, money, totalPrice, unitLabel } from "../format";
import type { Group, GroupLimit } from "../types";

interface Props {
    group: Group;
    limit: GroupLimit;
    onEdit: () => void;
    onPay: () => void;
}

export function PendingPurchase({ group, limit, onEdit, onPay }: Props) {
    const unit = unitLabel(group.saleUnit);
    const quantity = limit.quantity ?? 1;
    const lowest = Math.min(...group.priceTiers.map(tier => tier.priceKopecks));
    const lowerTotal = totalPrice(lowest, quantity);
    const saving = limit.chargedKopecks - lowerTotal;
    return <section className="order-summary" id="my-purchase" aria-label="Моя заявка" tabIndex={-1}>
        <header><ReceiptText size={22} aria-hidden="true" /><h2>Моя заявка</h2></header>
        <div className="order-prepaid">
            <span>Предоплата</span>
            <strong>{money(limit.chargedKopecks)}</strong>
            <p>{quantity.toLocaleString("ru-RU")} {unit} × {money(limit.limitKopecks)}</p>
        </div>
        <div className="order-wait">
            {limit.remainingQuantity != null && <strong>Осталось собрать {limit.remainingQuantity.toLocaleString("ru-RU")} {unit}</strong>}
            <p><Clock3 size={15} aria-hidden="true" /> До {deadlineLabel(group.deadline)}</p>
        </div>
        <p className="order-note">{saving > 0 ? "При цене " + money(lowest) + " вернём " + money(saving) + "." :
            limit.chargedKopecks > 0 ? "Если покупка не состоится, вернём всю предоплату." : "Оплата с кошелька пройдёт при достижении вашей цены."}</p>
        <button className="button button-dark" onClick={onEdit}>Изменить заявку</button>
        <div className="order-links">
            <Link className="text-link" to="/me">Мои сборы</Link>
            <button className="text-link" onClick={onPay}>Купить по текущей цене</button>
        </div>
    </section>;
}
