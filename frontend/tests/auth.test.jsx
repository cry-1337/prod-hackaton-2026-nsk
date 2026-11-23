import { fireEvent, screen, waitFor } from "@testing-library/react";
import { Route, Routes } from "react-router-dom";
import { expect, it, vi } from "vitest";
import { Protected } from "../src/components/Protected";
import { AuthPage } from "../src/pages/Auth";
import { buyerSession, jsonResponse, renderStorefront } from "./storefront";

function openPage(initialPath) {
    renderStorefront(<Routes>
        <Route path="/login" element={<AuthPage register={false} />} />
        <Route path="/register" element={<AuthPage register />} />
        <Route path="/products/:id/create" element={<Protected>{() => <h1>Оформление сбора</h1>}</Protected>} />
        <Route path="/me" element={<h1>Мои сборы</h1>} />
    </Routes>, initialPath);
}

function enterCredentials() {
    fireEvent.change(screen.getByLabelText("Почта"), { target: { value: "buyer@example.test" } });
    fireEvent.change(screen.getByLabelText("Пароль"), { target: { value: "test-password-123" } });
}

it("после входа возвращает покупателя к созданию сбора с исходными параметрами", async () => {
    const session = buyerSession();
    const request = vi.fn().mockResolvedValue(jsonResponse(session));
    vi.stubGlobal("fetch", request);
    openPage("/products/coffee/create?quantity=2");
    await screen.findByRole("heading", { name: "С возвращением!" });
    enterCredentials();
    fireEvent.click(screen.getByRole("button", { name: "Войти" }));

    await screen.findByRole("heading", { name: "Оформление сбора" });
    expect(screen.getByLabelText("Текущий маршрут").textContent).toBe("/products/coffee/create?quantity=2");
    const [url, options] = request.mock.calls[0];
    expect(url).toBe("/api/auth/login");
    expect(options.method).toBe("POST");
    expect(JSON.parse(options.body)).toEqual({ email: session.user.email, password: "test-password-123" });
    expect(JSON.parse(localStorage.getItem("vmeste.session"))).toEqual(session);
});

it("при неверном пароле показывает ошибку и позволяет войти повторно", async () => {
    const request = vi.fn()
        .mockResolvedValueOnce(jsonResponse({ title: "INVALID_CREDENTIALS" }, 401))
        .mockResolvedValueOnce(jsonResponse(buyerSession()));
    vi.stubGlobal("fetch", request);
    openPage("/login");
    enterCredentials();
    fireEvent.click(screen.getByRole("button", { name: "Войти" }));
    expect((await screen.findByRole("alert")).textContent).toBe("Проверьте почту и пароль.");
    expect(localStorage.getItem("vmeste.session")).toBeNull();
    fireEvent.change(screen.getByLabelText("Пароль"), { target: { value: "corrected-test-password" } });
    fireEvent.click(screen.getByRole("button", { name: "Войти" }));
    await screen.findByRole("heading", { name: "Мои сборы" });
    expect(request).toHaveBeenCalledTimes(2);
});

it("регистрирует по нику и открывает свой кабинет при внешнем адресе возврата", async () => {
    const request = vi.fn().mockResolvedValue(jsonResponse(buyerSession()));
    vi.stubGlobal("fetch", request);
    openPage("/register?next=https%3A%2F%2Fexample.test");
    enterCredentials();
    fireEvent.change(screen.getByLabelText("Никнейм"), { target: { value: " Лена " } });
    fireEvent.click(screen.getByRole("button", { name: "Зарегистрироваться" }));
    await waitFor(() => expect(screen.getByLabelText("Текущий маршрут").textContent).toBe("/me"));
    const [url, options] = request.mock.calls[0];
    expect(url).toBe("/api/auth/register");
    expect(JSON.parse(options.body).nickname).toBe("Лена");
});
