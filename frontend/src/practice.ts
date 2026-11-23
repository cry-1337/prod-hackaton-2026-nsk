import type { Session } from "./types";

const prefix = "/practice";
const returnKey = "vmeste.practice.return";
export const practiceDataKey = "vmeste.practice.data";
export const demoSession: Session = {
    user: { id: "guide-anna", nickname: "Анна", email: "anna@example.test" },
    accessToken: "", expiresAt: "2099-01-01T00:00:00Z"
};

export function inPractice() {
    return location.pathname === prefix || location.pathname.startsWith(prefix + "/");
}

export function startPractice() {
    sessionStorage.removeItem(practiceDataKey);
    sessionStorage.setItem(returnKey, location.pathname + location.search + location.hash);
    location.assign(prefix + "/");
}

export function leavePractice() {
    const origin = new URL(sessionStorage.getItem(returnKey) || "/", location.origin);
    sessionStorage.removeItem(practiceDataKey);
    sessionStorage.removeItem(returnKey);
    location.replace(origin.origin === location.origin ? origin.href : "/");
}
