import { QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, vi } from "vitest";
import { queries } from "../src/data";
import { setSession } from "../src/session";

export function buyerSession() {
    return {
        user: { id: "buyer-1", nickname: "Лена", email: "buyer@example.test" },
        accessToken: "buyer-test-token",
        expiresAt: new Date(Date.now() + 3600000).toISOString()
    };
}

export function jsonResponse(body, status = 200) {
    return Response.json(body, { status });
}

function CurrentRoute() {
    const { pathname, search } = useLocation();
    return <output aria-label="Текущий маршрут">{pathname + search}</output>;
}

export function renderStorefront(component, initialPath = "/") {
    return render(<QueryClientProvider client={queries}>
        <MemoryRouter initialEntries={[initialPath]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
            {component}
            <CurrentRoute />
        </MemoryRouter>
    </QueryClientProvider>);
}

afterEach(() => {
    cleanup();
    queries.clear();
    setSession(null);
    vi.unstubAllGlobals();
});
