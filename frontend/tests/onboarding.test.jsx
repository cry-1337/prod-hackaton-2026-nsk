import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PracticeBar } from "../src/components/PracticeBar";
import { Onboarding } from "../src/components/Onboarding";
import { queries } from "../src/data";
import { practiceDataKey } from "../src/practice";
import App from "../src/App";
import { setSession } from "../src/session";

function Page() {
    const location = useLocation();
    return <><output>{location.pathname + location.search}</output><Onboarding /></>;
}

function open() {
    return render(<MemoryRouter initialEntries={["/groups?q=test"]}><Page /></MemoryRouter>);
}

function click(name) { fireEvent.click(screen.getByRole("button", { name })); }

beforeEach(() => {
    HTMLDialogElement.prototype.showModal = function () { this.open = true; };
    HTMLDialogElement.prototype.close = function () { this.open = false; };
    vi.stubGlobal("ResizeObserver", class { observe() {} disconnect() {} });
    localStorage.clear();
    sessionStorage.clear();
});

afterEach(() => {
    cleanup();
    history.replaceState(null, "", "/");
    queries.clear();
    setSession(null);
    vi.unstubAllGlobals();
    delete HTMLDialogElement.prototype.showModal;
    delete HTMLDialogElement.prototype.close;
});

it("запоминает пропуск гостя после повторного открытия сайта", () => {
    open();
    click("Пропустить");
    cleanup();
    open();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(localStorage.getItem("vmeste.onboarding")).toBe("finished");
});

it("учитывает ранее пройденное обучение на этом устройстве", () => {
    localStorage.setItem("vmeste.onboarding.previous-user", "finished");
    open();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(localStorage.getItem("vmeste.onboarding")).toBe("finished");
});

it("показывает приглашение гостю до регистрации и не повторяет его после входа", async () => {
    const session = { user: { id: "registered", nickname: "Лена", email: "lena@example.test" },
        accessToken: "test", expiresAt: new Date(Date.now() + 3600000).toISOString() };
    const fetch = vi.fn(async url => Response.json(url === "/api/auth/register" ? session : []));
    vi.stubGlobal("fetch", fetch);
    vi.stubGlobal("scrollTo", vi.fn());
    const client = new QueryClient();
    render(<QueryClientProvider client={client}><MemoryRouter initialEntries={["/register?next=/"]}>
        <App />
    </MemoryRouter></QueryClientProvider>);
    expect(screen.getByRole("dialog", { name: /Добро пожаловать/ })).toBeTruthy();
    click("Пропустить");
    fireEvent.change(screen.getByLabelText("Никнейм"), { target: { value: session.user.nickname } });
    fireEvent.change(screen.getByLabelText("Почта"), { target: { value: session.user.email } });
    fireEvent.change(screen.getByLabelText("Пароль"), { target: { value: "test-password" } });
    click("Зарегистрироваться");
    expect(await screen.findByRole("heading", { name: "Витрина товаров" })).toBeTruthy();
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(fetch).toHaveBeenCalledWith("/api/auth/register", expect.objectContaining({ method: "POST" }));
});

it("создаёт учебный сбор с обычной витрины и открывает его на странице сбора без запросов на сервер", async () => {
    const session = { user: { id: "buyer", nickname: "Лена", email: "lena@example.test" },
        accessToken: "test", expiresAt: new Date(Date.now() + 3600000).toISOString() };
    setSession(session);
    history.replaceState(null, "", "/practice/");
    vi.stubGlobal("scrollTo", vi.fn());
    const request = vi.fn();
    vi.stubGlobal("fetch", request);
    const socket = vi.fn();
    vi.stubGlobal("WebSocket", socket);
    render(<QueryClientProvider client={queries}><MemoryRouter><App /></MemoryRouter></QueryClientProvider>);

    expect(screen.queryByRole("dialog")).toBeNull();
    fireEvent.click(await screen.findByRole("link", { name: /Открыть товар «Груши/ }));
    fireEvent.click(await screen.findByRole("link", { name: "Создать сбор" }));
    fireEvent.click(await screen.findByRole("radio", { name: /189/ }));
    fireEvent.change(screen.getByLabelText(/Количество, кг/), { target: { value: "1,5" } });
    click("Продолжить");
    expect(screen.queryByLabelText("Населённый пункт")).toBeNull();
    expect(screen.getByRole("button", { name: /Создать и оплатить/ }).disabled).toBe(true);
    click("Подставить учебный адрес");
    expect(screen.getByText(/Учебный адрес · Казань, Садовая, 12/)).toBeTruthy();
    click(/Создать и оплатить/);
    expect(await screen.findByRole("heading", { name: "Ход сбора" })).toBeTruthy();
    expect(await screen.findByRole("heading", { name: "Участники" })).toBeTruthy();
    const state = JSON.parse(sessionStorage.getItem(practiceDataKey));
    expect(state.groups).toHaveLength(1);
    expect(state.balance).toBe(500000 - 28350);
    expect(state.groups[0].myLimit.quantity).toBe(1.5);
    expect(JSON.parse(localStorage.getItem("vmeste.session"))).toEqual(session);
    expect(request).not.toHaveBeenCalled();
    expect(socket).not.toHaveBeenCalled();
});

it("переносит подсветку после нажатия на кнопку сайта и оставляет выход при скрытых подсказках", async () => {
    const rect = new DOMRect(12, 96, 150, 40);
    const visible = vi.spyOn(HTMLElement.prototype, "getClientRects").mockReturnValue([rect]);
    const bounds = vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockReturnValue(rect);
    const action = vi.fn();
    try {
        render(<MemoryRouter><PracticeBar />
            <button data-guide="catalog" onClick={action}>Каталог</button>
            <button data-guide="product">Груши</button>
        </MemoryRouter>);
        expect(await screen.findByRole("heading", { name: "Откройте витрину" })).toBeTruthy();
        click("Каталог");
        expect(action).toHaveBeenCalledOnce();
        expect(await screen.findByRole("heading", { name: "Выберите товар" })).toBeTruthy();
        expect(screen.getAllByRole("button", { name: "Выйти из практики" })).toHaveLength(2);
        click("Скрыть подсказки");
        expect(screen.queryByRole("region", { name: "Подсказка практики" })).toBeNull();
        expect(screen.getByRole("button", { name: "Выйти из практики" })).toBeTruthy();
        click("Показать подсказки");
        expect(await screen.findByRole("heading", { name: "Выберите товар" })).toBeTruthy();
    } finally {
        visible.mockRestore();
        bounds.mockRestore();
    }
});

it.each([
    ["guide-pears", "кг", "22,831"],
    ["guide-honey", "шт.", "10"]
])("предлагает только доступный объём при оформлении %s", async (id, unit, maximum) => {
    history.replaceState(null, "", "/practice/");
    vi.stubGlobal("scrollTo", vi.fn());
    const request = vi.fn();
    vi.stubGlobal("fetch", request);
    render(<QueryClientProvider client={queries}><MemoryRouter initialEntries={["/products/" + id + "/create"]}>
        <App />
    </MemoryRouter></QueryClientProvider>);
    const input = await screen.findByLabelText(/Количество,/);
    fireEvent.change(input, { target: { value: "100" } });
    const limit = await screen.findByRole("button", { name: "Взять " + maximum + " " + unit });
    expect(screen.getByRole("button", { name: "Продолжить" }).disabled).toBe(true);
    expect(screen.queryByRole("button", { name: /Добавить .* в учебный кошелёк/ })).toBeNull();
    fireEvent.click(limit);
    expect(input.value).toBe(maximum);
    expect(screen.getByRole("button", { name: "Продолжить" }).disabled).toBe(false);
    click("Продолжить");
    expect(screen.getByRole("heading", { name: "Получение и оплата" })).toBeTruthy();
    expect(request).not.toHaveBeenCalled();
});

it("по прямой ссылке на учебный кошелёк показывает только баланс", async () => {
    history.replaceState(null, "", "/practice/me#wallet");
    vi.stubGlobal("scrollTo", vi.fn());
    const scroll = HTMLElement.prototype.scrollIntoView;
    HTMLElement.prototype.scrollIntoView = vi.fn();
    try {
        render(<QueryClientProvider client={queries}><MemoryRouter initialEntries={["/me#wallet"]}><App /></MemoryRouter></QueryClientProvider>);
        expect(await screen.findByRole("heading", { name: /Кошелёк · 5\s000/ })).toBeTruthy();
        expect(screen.queryByRole("button", { name: /Пополнить/ })).toBeNull();
        expect(screen.queryByLabelText("Сумма пополнения, ₽")).toBeNull();
    } finally {
        HTMLElement.prototype.scrollIntoView = scroll;
    }
});
