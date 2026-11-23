import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { GroupChat } from "../src/components/GroupChat";

const connections = [];
const session = { user: { id: "buyer", nickname: "Лена", email: "buyer@example.test" }, accessToken: "test-token" };

class TestSocket {
    static OPEN = 1;
    readyState = TestSocket.OPEN;
    sent = [];

    constructor(url) {
        this.url = new URL(url);
        connections.push(this);
    }

    send(payload) {
        this.sent.push(JSON.parse(payload));
    }

    receive(type, data) {
        act(() => this.onmessage({ data: JSON.stringify({ type, data }) }));
    }

    close() {
        this.readyState = 3;
        this.onclose?.();
    }
}

function message(id, text = "Сообщение " + id) {
    return { id, text, authorId: "buyer", authorNickname: "Лена", createdAt: "2026-11-21T12:00:00Z" };
}

function openChat(user = null, canWrite = false) {
    render(<MemoryRouter><GroupChat groupId="coffee" session={user} canWrite={canWrite} /></MemoryRouter>);
    return connections.at(-1);
}

beforeEach(() => {
    connections.length = 0;
    vi.useFakeTimers();
    vi.stubGlobal("WebSocket", TestSocket);
});

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    vi.useRealTimers();
});

it("гость читает историю, получает новые сообщения и не видит форму отправки", () => {
    const socket = openChat();
    expect(socket.url.pathname).toBe("/api/groups/coffee/chat/ws");
    socket.receive("ready", Array.from({ length: 50 }, (_, i) => message(i + 2)));
    expect(screen.queryByRole("textbox", { name: "Сообщение" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Раньше" }));
    expect(socket.sent).toEqual([{ type: "history", beforeId: 2, limit: 50 }]);
    socket.receive("history", [message(1), message(2)]);
    socket.receive("message", message(52));
    const items = within(screen.getByRole("log")).getAllByRole("listitem");
    expect(items.map(item => Number(item.dataset.id))).toEqual(Array.from({ length: 52 }, (_, i) => i + 1));
});

it("после обрыва догружает с последнего id и безопасно повторяет неподтверждённую отправку", () => {
    const socket = openChat(session, true);
    socket.receive("ready", [message(10)]);
    expect(socket.sent).toEqual([{ type: "auth", accessToken: "test-token" }]);
    expect(socket.url.search).toBe("");
    const input = screen.getByRole("textbox", { name: "Сообщение" });
    const send = screen.getByRole("button", { name: "Отправить сообщение" });
    fireEvent.change(input, { target: { value: "  Добрый день  " } });
    expect(send.disabled).toBe(true);
    socket.receive("authenticated");
    fireEvent.click(send);
    const pending = socket.sent.at(-1);
    expect(pending).toEqual({ type: "message", text: "Добрый день", idempotencyKey: expect.any(String) });
    act(() => socket.close());
    act(() => vi.advanceTimersByTime(500));
    const next = connections.at(-1);
    expect(next.url.searchParams.get("afterId")).toBe("10");
    next.receive("ready", []);
    next.receive("authenticated");
    fireEvent.click(send);
    expect(next.sent.at(-1)).toEqual(pending);
    next.receive("sent", message(11, pending.text));
    next.receive("message", message(11, pending.text));
    expect(screen.getAllByText("Добрый день")).toHaveLength(1);
    expect(input.value).toBe("");
});
