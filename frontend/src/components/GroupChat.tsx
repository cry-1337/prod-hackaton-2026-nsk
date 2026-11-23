import { inPractice } from "../practice";
import { api } from "../api";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { MessageCircle, Send } from "lucide-react";
import { Link } from "react-router-dom";
import { expireSession } from "../session";
import { deadlineLabel, groupPath } from "../format";
import type { ChatMessage, Session } from "../types";
import { HelpTip } from "./HelpTip";
import { Loading } from "./States";

interface ChatProps {
    groupId: string;
    session: Session | null;
    canWrite: boolean;
}

type ChatFrame =
    | { type: "ready" | "history"; data: ChatMessage[] }
    | { type: "message" | "sent"; data: ChatMessage }
    | { type: "authenticated" }
    | { type: "error"; status: number; title: string };

const pageSize = 50;
const chatErrors: Record<string, string> = {
    GROUP_CHAT_FORBIDDEN: "Писать могут участники и организаторы сбора.",
    VALIDATION_FAILED: "Проверьте сообщение и попробуйте ещё раз.",
    IDEMPOTENCY_KEY_REUSED: "Сообщение изменилось. Попробуйте отправить его снова.",
    RATE_LIMIT_EXCEEDED: "Слишком много сообщений. Попробуйте через минуту.",
    UNAUTHORIZED: "Войдите снова, чтобы отправить сообщение."
};

function Messages({ groupId, session, canWrite }: ChatProps) {
    const [messages, setMessages] = useState<ChatMessage[]>();
    const [hasOlder, setHasOlder] = useState(false);
    const [connected, setConnected] = useState(false);
    const [authenticated, setAuthenticated] = useState(false);
    const [reconnecting, setReconnecting] = useState(false);
    const [pending, setPending] = useState<"history" | "message">();
    const [error, setError] = useState<string>();
    const [draft, setDraft] = useState("");
    const socket = useRef<WebSocket>();
    const cursor = useRef<number>();
    const timeout = useRef<number>();
    const operation = useRef<"history" | "message">();
    const retry = useRef<{ text: string; key: string; userId: string }>();
    const scroll = useRef<HTMLDivElement>(null);
    const atBottom = useRef(true);
    const anchor = useRef<{ id: number; position: number }>();
    const token = session?.accessToken;

    useEffect(() => {
        let active = true;
        if (inPractice()) {
            void api<ChatMessage[]>(groupPath(groupId) + "/chat").then(history => {
                if (!active) return;
                setMessages(history);
                setConnected(true);
                setAuthenticated(true);
            });
            return () => { active = false; };
        }
        let reconnect: number | undefined;
        let handshake: number | undefined;
        let delay = 500;
        setConnected(false);
        setAuthenticated(false);
        setPending(undefined);
        operation.current = undefined;

        function merge(batch: ChatMessage[]) {
            setMessages(current => {
                const byId = new Map((current ?? []).map(message => [message.id, message]));
                for (const message of batch) byId.set(message.id, message);
                return [...byId.values()].sort((a, b) => a.id - b.id);
            });
        }

        function connect() {
            const afterId = cursor.current;
            const url = new URL("/api" + groupPath(groupId) + "/chat/ws", window.location.href);
            url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
            if (afterId !== undefined) url.searchParams.set("afterId", String(afterId));
            const connection = new WebSocket(url);
            socket.current = connection;
            handshake = window.setTimeout(() => connection.close(), 15000);

            connection.onmessage = ({data}: MessageEvent<string>) => {
                if (!active) return;
                const frame = JSON.parse(data) as ChatFrame;
                switch (frame.type) {
                    case "ready":
                        if (!token) window.clearTimeout(handshake);
                        delay = 500;
                        setConnected(true);
                        setReconnecting(false);
                        if (afterId === undefined) setHasOlder(frame.data.length === pageSize);
                        cursor.current = frame.data.at(-1)?.id ?? afterId ?? 0;
                        merge(frame.data);
                        if (token) connection.send(JSON.stringify({type: "auth", accessToken: token}));
                        break;
                    case "authenticated":
                        window.clearTimeout(handshake);
                        setAuthenticated(true);
                        break;
                    case "message":
                        cursor.current = Math.max(cursor.current ?? 0, frame.data.id);
                        merge([frame.data]);
                        break;
                    case "history":
                        window.clearTimeout(timeout.current);
                        operation.current = undefined;
                        setHasOlder(frame.data.length === pageSize);
                        merge(frame.data);
                        setPending(undefined);
                        break;
                    case "sent":
                        window.clearTimeout(timeout.current);
                        operation.current = undefined;
                        retry.current = undefined;
                        atBottom.current = true;
                        merge([frame.data]);
                        setDraft("");
                        setPending(undefined);
                        break;
                    case "error":
                        window.clearTimeout(timeout.current);
                        operation.current = undefined;
                        anchor.current = undefined;
                        setPending(undefined);
                        setError(chatErrors[frame.title] ?? "Запрос к чату не удался. Попробуйте ещё раз.");
                        if (frame.status === 401 && token) expireSession(token);
                        if (frame.title === "IDEMPOTENCY_KEY_REUSED") retry.current = undefined;
                        break;
                }
            };

            connection.onclose = () => {
                if (!active) return;
                window.clearTimeout(handshake);
                window.clearTimeout(timeout.current);
                operation.current = undefined;
                anchor.current = undefined;
                setConnected(false);
                setAuthenticated(false);
                setPending(undefined);
                setReconnecting(true);
                reconnect = window.setTimeout(connect, delay);
                delay = Math.min(delay * 2, 10000);
            };
        }

        connect();
        return () => {
            active = false;
            window.clearTimeout(reconnect);
            window.clearTimeout(handshake);
            window.clearTimeout(timeout.current);
            socket.current?.close();
            socket.current = undefined;
        };
    }, [groupId, token]);

    function request(type: "history" | "message", payload: object) {
        const connection = socket.current;
        if (operation.current || connection?.readyState !== WebSocket.OPEN) return;
        setError(undefined);
        operation.current = type;
        setPending(type);
        connection.send(JSON.stringify({ type, ...payload }));
        timeout.current = window.setTimeout(() => connection.close(), 15000);
    }

    const first = messages?.[0]?.id;
    const last = messages?.at(-1)?.id;
    useLayoutEffect(() => {
        const pane = scroll.current;
        if (!pane) return;
        if (anchor.current && first !== anchor.current.id) {
            const old = pane.querySelector('[data-id="' + anchor.current.id + '"]');
            if (old) pane.scrollTop += old.getBoundingClientRect().top - anchor.current.position;
            anchor.current = undefined;
        } else if (atBottom.current) pane.scrollTop = pane.scrollHeight;
    }, [first, last]);

    return <>
        <div className="chat-scroll" ref={scroll} role="log" aria-label="Сообщения" onScroll={event => {
            const pane = event.currentTarget;
            atBottom.current = pane.scrollHeight - pane.scrollTop - pane.clientHeight < 48;
        }}>
            {hasOlder && first != null && <button type="button" className="text-link" disabled={!connected || !!pending} onClick={() => {
                const old = scroll.current?.querySelector("[data-id]");
                if (old) anchor.current = { id: first, position: old.getBoundingClientRect().top };
                request("history", { beforeId: first, limit: pageSize });
            }}>Раньше</button>}
            {messages === undefined ? (!reconnecting && <Loading />) : messages.length ?
                <ul className="chat-list">{messages.map(message => <li key={message.id} data-id={message.id}
                    className={message.authorId === session?.user.id ? "own-message" : undefined}>
                    <Link to={"/users/" + message.authorId + "?group=" + encodeURIComponent(groupId)}>{message.authorNickname}</Link>
                    <p>{message.text}</p><time dateTime={message.createdAt}>{deadlineLabel(message.createdAt)}</time>
                </li>)}</ul> : <p className="field-hint">Сообщений пока нет.</p>}
        </div>
        {reconnecting && <p className="field-hint" role="status">Переподключаемся к чату…</p>}
        {error && <p className="form-error" role="alert">{error}</p>}
        {canWrite && session ? <form className="chat-compose" onSubmit={event => {
            event.preventDefault();
            const text = draft.trim();
            if (!text || pending || !connected || !authenticated) return;
            if (inPractice()) {
                setPending("message");
                void api<ChatMessage>(groupPath(groupId) + "/chat", { method: "POST", body: { text } })
                    .then(message => { setMessages(previous => [...(previous ?? []), message]); setDraft(""); })
                    .catch(() => setError("Не удалось сохранить учебное сообщение."))
                    .finally(() => setPending(undefined));
                return;
            }
            const userId = session.user.id;
            if (retry.current?.text !== text || retry.current.userId !== userId) {
                retry.current = { text, key: crypto.randomUUID(), userId };
            }
            request("message", { text, idempotencyKey: retry.current.key });
        }}>
            <input aria-label="Сообщение" placeholder="Сообщение" maxLength={1000} value={draft} disabled={pending === "message"}
                onChange={event => setDraft(event.target.value)} autoComplete="off" />
            <button aria-label="Отправить сообщение" className="icon-button" disabled={!draft.trim() || !!pending || !connected || !authenticated}><Send size={17} /></button>
        </form> : <p className="field-hint">Писать могут участники и организаторы сбора.</p>}
    </>;
}

export function GroupChat(props: ChatProps) {
    return <section id="chat" className="panel group-chat" aria-labelledby="chat-heading">
        <header className="group-section-heading">
            <MessageCircle size={20} aria-hidden="true" />
            <h2 id="chat-heading">Чат сбора</h2>
            <HelpTip label="Кто может писать">Чат открыт для чтения всем. Сообщения могут отправлять организатор и участники с действующей заявкой или покупкой.</HelpTip>
        </header>
        <Messages {...props} />
    </section>;
}
