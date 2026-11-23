export const onboardingKey = "vmeste.onboarding";
let finishedWithoutStorage = false;

export function finishOnboarding() {
    try {
        localStorage.setItem(onboardingKey, "finished");
    } catch {
        finishedWithoutStorage = true;
    }
}

export function needsOnboarding() {
    if (finishedWithoutStorage) return false;
    try {
        if (localStorage.getItem(onboardingKey) === "finished") return false;
        // Carry forward completions saved by the account-based version on this browser.
        if (Object.keys(localStorage).some(key => key.startsWith(onboardingKey + ".") && localStorage.getItem(key) === "finished")) {
            finishOnboarding();
            return false;
        }
    } catch { /* Storage may be unavailable in private browsing. */ }
    return true;
}
