import { fireEvent, screen, within } from "@testing-library/react";
import { Route, Routes } from "react-router-dom";
import { setSession } from "../src/session";
import { Layout } from "../src/components/Layout";
import { expect, it, vi } from "vitest";
import { AccountPage } from "../src/pages/Account";
import { OrganizerPage } from "../src/components/Organizer";
import { buyerSession, jsonResponse, renderStorefront } from "./storefront";

function serveProfile(user, count) {
    vi.stubGlobal("fetch", vi.fn(async url => {
        if (url === "/api/users/" + user.id + "/organizer-profile") {
            return jsonResponse({ id: user.id, nickname: user.nickname, likesCount: count, likedByMe: false,
                openGroupsCount: 0, successfulGroupsCount: 0 });
        }
        if (url === "/api/auth/me") return jsonResponse(user);
        if (url === "/api/me/balance") return jsonResponse({ balanceKopecks: 123456 });
        if (url.startsWith("/api/me/notifications?") || url === "/api/me/groups" || url.startsWith("/api/groups?")) return jsonResponse([]);
        throw new Error("Unexpected request: " + url);
    }));
}

it.each([[0, "0 лайков"], [1, "1 лайк"], [23, "23 лайка"]])("показывает полученные лайки (%i) в своём кабинете на обеих вкладках", async (count, label) => {
    const session = buyerSession();
    serveProfile(session.user, count);
    renderStorefront(<AccountPage session={session} />, "/me");
    const header = within(screen.getByRole("banner"));
    await header.findByText(label);
    fireEvent.click(screen.getByRole("button", { name: "Организую" }));
    expect(header.getByText(label)).toBeTruthy();
    expect(header.queryByRole("button", { name: /нравится/i })).toBeNull();
});

it("показывает гостю лайки в публичном профиле и предлагает войти для оценки", async () => {
    const user = buyerSession().user;
    serveProfile(user, 12);
    renderStorefront(<OrganizerPage id={user.id} organizer />, "/organizers/" + user.id);
    await screen.findByText("12 лайков");
    expect(screen.getByRole("link", { name: "Войти и поставить лайк" }).getAttribute("href"))
        .toBe("/login?next=" + encodeURIComponent("/organizers/" + user.id));
});

it("сохраняет баланс в навбаре при переходах и не запрашивает лайки профиля", async () => {
    const session = buyerSession();
    setSession(session);
    localStorage.setItem("vmeste.onboarding", "finished");
    vi.stubGlobal("scrollTo", vi.fn());
    serveProfile(session.user, 23);
    renderStorefront(<Routes><Route element={<Layout />}>
        <Route index element={<h1>Каталог</h1>} />
        <Route path="/groups" element={<h1>Список сборов</h1>} />
    </Route></Routes>);
    const header = within(screen.getByRole("banner"));
    const wallet = await header.findByRole("link", { name: /Баланс кошелька 1\s234,56\s₽/ });
    expect(wallet.getAttribute("href")).toBe("/me#wallet");
    expect(wallet.target).toBe("_blank");
    fireEvent.click(header.getByRole("link", { name: "Сборы" }));
    await screen.findByRole("heading", { name: "Список сборов" });
    expect(header.getByRole("link", { name: "Лена" }).getAttribute("href")).toBe("/me");
    expect(header.getByRole("link", { name: /Баланс кошелька/ })).toBe(wallet);
    expect(fetch.mock.calls.some(([url]) => url.endsWith("/organizer-profile"))).toBe(false);
});
