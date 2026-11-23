import { fireEvent, screen } from "@testing-library/react";
import { expect, it, vi } from "vitest";
import { InviteForm } from "../src/components/InviteForm";
import { jsonResponse, renderStorefront } from "./storefront";

function search(code) {
    fireEvent.change(screen.getByLabelText("Код сбора"), { target: { value: code } });
    fireEvent.click(screen.getByRole("button", { name: "Найти сбор по коду" }));
}

it("отклоняет некорректный код до запроса к API", () => {
    const request = vi.fn();
    vi.stubGlobal("fetch", request);
    renderStorefront(<InviteForm />);
    search("ошибка!");
    expect(screen.getByText(/Введите код латинскими буквами/)).toBeTruthy();
    expect(request).not.toHaveBeenCalled();
});

it("после 404 остаётся на витрине, повторяет поиск и открывает сбор только по ссылке", async () => {
    const request = vi.fn()
        .mockResolvedValueOnce(jsonResponse({ title: "GROUP_NOT_FOUND" }, 404))
        .mockResolvedValueOnce(jsonResponse({ id: "group-id", productName: "Кофе" }));
    vi.stubGlobal("fetch", request);
    renderStorefront(<InviteForm />);
    search("  COFFEE-1  ");
    await screen.findByText(/Сбор не найден/);
    expect(screen.getByLabelText("Текущий маршрут").textContent).toBe("/");
    expect(screen.queryByRole("link")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Найти сбор по коду" }));
    const result = await screen.findByRole("link", { name: "Открыть сбор «Кофе»" });
    expect(request.mock.calls.map(([url]) => url)).toEqual(["/api/groups/coffee-1", "/api/groups/coffee-1"]);
    expect(screen.getByLabelText("Текущий маршрут").textContent).toBe("/");
    fireEvent.click(result);
    expect(screen.getByLabelText("Текущий маршрут").textContent).toBe("/groups/group-id");
});
