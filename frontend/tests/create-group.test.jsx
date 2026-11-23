import { fireEvent, screen, waitFor } from "@testing-library/react";
import { Route, Routes } from "react-router-dom";
import { expect, it, vi } from "vitest";
import { CreateGroupPage } from "../src/pages/CreateGroup";
import { buyerSession, jsonResponse, renderStorefront } from "./storefront";

const apples = {
    id: "apples", name: "Яблоки Гала", saleUnit: "kilogram", regularPriceKopecks: 18900,
    organizerBonus: { quantity: 5, unit: "кг" },
    priceTiers: [{ requiredQuantity: 3, priceKopecks: 15900 }, { requiredQuantity: 5, priceKopecks: 13900 }]
};

function openForm({ balance = 100000, product = apples } = {}) {
    const request = vi.fn(async (url, options) => {
        if (url === "/api/catalog/products/apples") return jsonResponse(product);
        if (url === "/api/me/balance") return jsonResponse({ balanceKopecks: balance });
        if (url === "/api/groups" && options.method === "POST") return jsonResponse({ id: "apple-group", productId: "apples" });
        throw new Error("Unexpected request: " + url);
    });
    vi.stubGlobal("fetch", request);
    renderStorefront(<Routes>
        <Route path="/products/apples/create" element={<CreateGroupPage id="apples" session={buyerSession()} />} />
        <Route path="/groups/:id" element={<h1>Страница нового сбора</h1>} />
    </Routes>, "/products/apples/create");
    return request;
}

it("для товара без ступеней показывает причину и не предлагает оплату", async () => {
    const request = openForm({ product: { ...apples, priceTiers: [] } });
    await screen.findByText("Для этого товара пока нельзя создать сбор");
    expect(screen.queryByRole("button", { name: "Продолжить" })).toBeNull();
    expect(request).toHaveBeenCalledTimes(1);
});

it("при нехватке денег показывает переход к кошельку и блокирует оплату", async () => {
    const request = openForm({ balance: 100 });
    fireEvent.click(await screen.findByRole("button", { name: "Продолжить" }));
    const walletLink = await screen.findByRole("link", { name: "Пополнить кошелёк" });
    expect(walletLink.getAttribute("href")).toBe("/me#wallet");
    const submit = screen.getByRole("button", { name: /Создать и оплатить/ });
    expect(submit.disabled).toBe(true);
    fireEvent.click(submit);
    expect(request.mock.calls.some(([, options]) => options.method === "POST")).toBe(false);
});

it("создаёт сбор с выбранной ступенью, дробным весом и адресом после подтверждения", async () => {
    const request = openForm();
    fireEvent.click(await screen.findByRole("radio", { name: /139/ }));
    fireEvent.change(screen.getByLabelText(/Количество, кг/), { target: { value: "1,5" } });
    fireEvent.click(screen.getByRole("button", { name: "Продолжить" }));
    const submit = screen.getByRole("button", { name: /Создать и оплатить/ });
    fireEvent.click(submit);
    expect(request.mock.calls.some(([, options]) => options.method === "POST")).toBe(false);
    fireEvent.change(screen.getByLabelText("Населённый пункт"), { target: { value: " Казань " } });
    fireEvent.change(screen.getByLabelText("Улица"), { target: { value: "8 Марта" } });
    fireEvent.change(screen.getByLabelText("Дом"), { target: { value: "12А" } });
    fireEvent.click(submit);

    await screen.findByRole("heading", { name: "Страница нового сбора" });
    const posts = request.mock.calls.filter(([, options]) => options.method === "POST");
    expect(posts).toHaveLength(1);
    const [url, options] = posts[0];
    expect(url).toBe("/api/groups");
    expect(options.headers.get("Idempotency-Key")).toBeTruthy();
    expect(options.headers.get("Authorization")).toBe("Bearer buyer-test-token");
    const body = JSON.parse(options.body);
    expect(body).toMatchObject({
        productId: "apples", quantity: 1.5, organizerLimitKopecks: 13900,
        address: { city: "Казань", street: "8 Марта", house: "12А" }
    });
    expect(Date.parse(body.deadline)).toBeGreaterThan(Date.now());
    await waitFor(() => expect(screen.getByLabelText("Текущий маршрут").textContent).toBe("/groups/apple-group"));
});
