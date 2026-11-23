import { fireEvent, screen, waitFor } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { LimitPurchase } from "../src/components/LimitPurchase";
import { orderUnit } from "../src/groupPricing";
import { buyerSession, jsonResponse, renderStorefront } from "./storefront";

const group = { id: "coffee", saleUnit: "piece", status: "open", productName: "Кофе" };
const tiers = [
    { priceKopecks: 85000, requiredQuantity: 5, eligibleQuantity: 4 },
    { priceKopecks: 99000, requiredQuantity: 3, eligibleQuantity: 0 }
];

function openLimit(props = {}) {
    renderStorefront(<LimitPurchase group={group} tiers={tiers} unit={orderUnit("piece")} session={null}
        onFinish={vi.fn()} onPay={vi.fn()} {...props} />, "/groups/coffee");
}

it("гость сохраняет выбранные цену и количество в ссылке входа", () => {
    const request = vi.fn();
    vi.stubGlobal("fetch", request);
    openLimit();
    fireEvent.click(screen.getByRole("radio", { name: /990/ }));
    fireEvent.click(screen.getByRole("button", { name: "Увеличить количество" }));
    const login = new URL(screen.getByRole("link", { name: "Войти и участвовать" }).href);
    expect(login.pathname).toBe("/login");
    expect(login.searchParams.get("next")).toBe("/groups/coffee?limit=99000&quantity=2");
    expect(request).not.toHaveBeenCalled();
});

it("при достижимой по прогнозу цене блокирует оформление, если серверу не хватает средств", async () => {
    const session = buyerSession();
    const request = vi.fn().mockResolvedValueOnce(jsonResponse({
        canAfford: false, reachablePriceKopecks: null, remainingQuantity: 0, refundNowKopecks: 0
    }));
    vi.stubGlobal("fetch", request);
    openLimit({ session });
    await screen.findByRole("heading", { name: "Недостаточно средств" });
    expect(screen.getByRole("button", { name: "К оформлению" }).disabled).toBe(true);
    const wallet = screen.getByRole("link", { name: "Пополнить кошелёк" });
    expect(wallet.getAttribute("href")).toBe("/me#wallet");
    expect(wallet.target).toBe("_blank");
    const [url, options] = request.mock.calls[0];
    expect(url).toBe("/api/groups/coffee/limit/quote?limitKopecks=85000&quantity=1");
    expect(options.headers.get("Authorization")).toBe("Bearer " + session.accessToken);
});

it("отзывает ожидающую заявку и возвращает покупателя к результату после ответа API", async () => {
    const onFinish = vi.fn();
    const request = vi.fn(async (_url, options) => options.method === "DELETE"
        ? jsonResponse({ status: "revoked", refundedKopecks: 170000 })
        : jsonResponse({ canAfford: true, remainingQuantity: 1, refundNowKopecks: 0 }));
    vi.stubGlobal("fetch", request);
    openLimit({
        session: buyerSession(), onFinish,
        current: { status: "pending", limitKopecks: 85000, quantity: 2, chargedKopecks: 170000 }
    });
    expect(screen.getByLabelText("Сколько штук беру").value).toBe("2");
    fireEvent.click(screen.getByRole("button", { name: "Отозвать заявку" }));
    await waitFor(() => expect(onFinish).toHaveBeenCalledTimes(1));
    const deletions = request.mock.calls.filter(([, options]) => options.method === "DELETE");
    expect(deletions).toHaveLength(1);
    const [url, options] = deletions[0];
    expect(url).toBe("/api/groups/coffee/limit");
    expect(options.headers.get("Idempotency-Key")).toBeTruthy();
    expect(options.body).toBeUndefined();
});
