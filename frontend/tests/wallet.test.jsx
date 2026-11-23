import { fireEvent, screen } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { Wallet } from "../src/components/Wallet";
import { buyerSession, jsonResponse, renderStorefront } from "./storefront";

async function openWallet(request) {
    vi.stubGlobal("fetch", request);
    renderStorefront(<Wallet session={buyerSession()} />);
    await screen.findByRole("heading", { name: /Кошелёк · 100/ });
    fireEvent.click(screen.getByRole("button", { name: "Пополнить" }));
    return screen.getByLabelText("Сумма пополнения, ₽");
}

it("объясняет причину некорректной суммы и разрешает пополнение после исправления", async () => {
    const request = vi.fn().mockResolvedValueOnce(jsonResponse({ balanceKopecks: 10000 }));
    const input = await openWallet(request);
    const submit = screen.getByRole("button", { name: "Пополнить кошелёк" });
    for (const [invalid, message] of [
        ["-50", /больше нуля/], ["0", /больше нуля/], ["1,001", /два знака/],
        ["1000000,01", /за одно пополнение/i], ["9".repeat(400), /за одно пополнение/i]
    ]) {
        fireEvent.change(input, { target: { value: invalid } });
        expect(screen.getByRole("alert").textContent).toMatch(message);
        expect(input.getAttribute("aria-describedby")).toBe(screen.getByRole("alert").id);
        expect(submit.disabled).toBe(true);
        fireEvent.click(submit);
    }
    expect(request).toHaveBeenCalledTimes(1);
    fireEvent.change(input, { target: { value: "1000000" } });
    expect(screen.queryByRole("alert")).toBeNull();
    expect(submit.disabled).toBe(false);
});

it("после потери ответа повторяет пополнение с тем же ключом и обновляет баланс", async () => {
    const request = vi.fn()
        .mockResolvedValueOnce(jsonResponse({ balanceKopecks: 10000 }))
        .mockRejectedValueOnce(new TypeError("Connection lost"))
        .mockResolvedValueOnce(jsonResponse({}))
        .mockResolvedValueOnce(jsonResponse({ balanceKopecks: 11250 }));
    const input = await openWallet(request);
    fireEvent.change(input, { target: { value: "12,50" } });
    fireEvent.click(screen.getByRole("button", { name: "Пополнить кошелёк" }));
    await screen.findByRole("alert");
    fireEvent.click(screen.getByRole("button", { name: "Пополнить кошелёк" }));
    await screen.findByRole("heading", { name: /Кошелёк · 112,5/ });
    const payments = request.mock.calls.filter(([url]) => url === "/api/me/balance/top-ups");
    expect(payments).toHaveLength(2);
    const firstKey = payments[0][1].headers.get("Idempotency-Key");
    expect(firstKey).toBeTruthy();
    for (const [, options] of payments) {
        expect(options.method).toBe("POST");
        expect(options.headers.get("Authorization")).toBe("Bearer buyer-test-token");
        expect(options.headers.get("Idempotency-Key")).toBe(firstKey);
        expect(JSON.parse(options.body)).toEqual({ amountKopecks: 1250 });
    }
    expect(screen.queryByLabelText("Сумма пополнения, ₽")).toBeNull();
});
