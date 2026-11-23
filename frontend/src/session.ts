import { demoSession, inPractice } from "./practice";
import { useSyncExternalStore } from "react";
import type { Session } from "./types";

const key = "vmeste.session";
const listeners = new Set<() => void>();

function read(): Session | null {
    try {
        const session = JSON.parse(localStorage.getItem(key) || "null");
        if (
            typeof session?.accessToken === "string" &&
            typeof session.user?.id === "string" &&
            typeof session.user.nickname === "string" &&
            typeof session.user.email === "string" &&
            Date.parse(session.expiresAt) > Date.now()
        ) {
            return session;
        }
    } catch {
        return null;
    }
    return null;
}

let current = inPractice() ? null : read();
let expiryTimer: number | undefined;

function scheduleExpiry() {
    window.clearTimeout(expiryTimer);
    if (current) {
        const token = current.accessToken;
        expiryTimer = window.setTimeout(
            () => expireSession(token),
            Date.parse(current.expiresAt) - Date.now()
        );
    }
}

export function setSession(session: Session | null) {
    if (inPractice()) return;
    current = session;
    scheduleExpiry();
    listeners.forEach(notify => notify());
    try {
        if (session) localStorage.setItem(key, JSON.stringify(session));
        else localStorage.removeItem(key);
    } catch {
        return;
    }
}

export function expireSession(token: string) {
    if (current?.accessToken === token) setSession(null);
}

window.addEventListener("storage", event => {
    if (inPractice()) return;
    if (event.key !== key && event.key !== null) return;
    current = read();
    scheduleExpiry();
    listeners.forEach(notify => notify());
});
scheduleExpiry();

function subscribe(notify: () => void) {
    listeners.add(notify);
    return () => {
        listeners.delete(notify);
    };
}

export function useSession() {
    return useSyncExternalStore(subscribe, () => inPractice() ? demoSession : current);
}
