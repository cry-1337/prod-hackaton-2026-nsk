import { useEffect, useState } from "react";
import { ChevronsUp, LogOut, Grid2X2, Layers2, ShoppingBasket, UserRound, Wallet, WalletCards } from "lucide-react";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { setSession, useSession } from "../session";
import { queries, useBalance } from "../data";
import { NotificationBell } from "./Notifications";
import type { Session } from "../types";
import { money } from "../format";
import { inPractice, leavePractice } from "../practice";
import { PracticeBar } from "./PracticeBar";
import { Onboarding } from "./Onboarding";

const destinations = [
    {
        to: "/",
        label: "Витрина",
        icon: Grid2X2,
        active: (path: string) => path === "/" || path.startsWith("/products/")
    },
    {
        to: "/groups",
        label: "Сборы",
        icon: Layers2,
        active: (path: string) => path.startsWith("/groups")
    },
    { to: "/me", label: "Мои сборы", icon: WalletCards, active: (path: string) => path === "/me" }
];

function Navigation({ mobile = false }: { mobile?: boolean }) {
    const { pathname } = useLocation();
    return (
        <nav
            className={mobile ? "mobile-nav" : "desktop-nav"}
            aria-label={mobile ? "Мобильная навигация" : "Основная навигация"}
        >
            {destinations.map(({ to, label, icon: Icon, active }) => (
                <Link key={to} data-guide={to === "/me" ? "mine" : undefined} to={to} aria-current={active(pathname) ? "page" : undefined}>
                    <Icon size={17} aria-hidden="true" />
                    {label}
                </Link>
            ))}
        </nav>
    );
}

function Brand() {
    return (
        <Link to="/" aria-label="Вместе — на главную" className="brand">
            <ShoppingBasket aria-hidden="true" />
            <span>
                вместе<span className="text-citrus">.</span>
            </span>
        </Link>
    );
}

const compactRubles = new Intl.NumberFormat("ru-RU", { notation: "compact", maximumFractionDigits: 1 });

function HeaderWallet({ session }: { session: Session }) {
    const balance = useBalance(session);
    const amount = balance.data ? money(balance.data.balanceKopecks) : "—";
    const kopecks = balance.data?.balanceKopecks;
    const compact = kopecks != null && Math.abs(kopecks) >= 100_000_000
        ? compactRubles.format(kopecks / 100) + " ₽" : null;
    const label = balance.data ? "Баланс кошелька " + amount : balance.isError ? "Баланс недоступен" : "Загружаем баланс";

    return <Link className="header-wallet" to="/me#wallet" target={inPractice() ? undefined : "_blank"} rel="noopener"
        aria-label={label + (inPractice() ? ". Открыть учебный кошелёк" : ". Открыть кошелёк в новой вкладке")} title={label}>
        <Wallet size={17} aria-hidden="true" />
        {compact ?? amount}
    </Link>;
}

function BackToTop() {
    const [visible, setVisible] = useState(false);
    useEffect(() => {
        const update = () => setVisible(window.scrollY > 200);
        update();
        window.addEventListener("scroll", update, { passive: true });
        return () => window.removeEventListener("scroll", update);
    }, []);

    if (!visible) return null;
    return <button className="back-to-top" type="button" aria-label="Наверх" title="Наверх" onClick={() => {
        document.getElementById("content")?.focus({ preventScroll: true });
        window.scrollTo({ top: 0, behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth" });
    }}>
        <ChevronsUp size={22} aria-hidden="true" />
    </button>;
}

export function Layout() {
    const session = useSession();
    const { pathname, search } = useLocation();
    const navigate = useNavigate();
    const loginUrl = "/login?next=" + encodeURIComponent(pathname + search);
    useEffect(() => {
        window.scrollTo(0, 0);
    }, [pathname]);

    return (
        <div className="app-shell">
            <a className="skip-link" href="#content">
                Перейти к содержимому
            </a>
            <header className="site-header">
                <div className="frame flex min-h-[72px] items-center justify-between gap-4">
                    <Brand />
                    <Navigation />
                    <div className="header-actions">
                        {session && <HeaderWallet session={session} />}
                        {session && <NotificationBell key={session.user.id} session={session} />}
                        <Link
                            className="header-account"
                            to={session ? "/me" : pathname === "/register" || pathname === "/login" ? "/login" + search : loginUrl}
                        >
                            <UserRound size={17} aria-hidden="true" />
                            <span className="header-nickname truncate">{session ? session.user.nickname : "Войти"}</span>
                        </Link>
                        {session && <button className="logout-button" type="button" aria-label="Выйти из аккаунта" title="Выйти из аккаунта" onClick={() => {
                            if (inPractice()) return leavePractice();
                            setSession(null);
                            queries.clear();
                            navigate("/", { replace: true });
                        }}>
                            <LogOut size={17} aria-hidden="true" /> <span>Выйти</span>
                        </button>}
                    </div>
                </div>
                {inPractice() && <PracticeBar />}
            </header>
            <main id="content" className="frame main-frame" tabIndex={-1}>
                <Outlet />
            </main>
            <footer className="site-footer">
                <div className="frame flex flex-wrap items-center justify-between gap-3">
                    <Brand />
                    {!inPractice() && <Onboarding />}
                </div>
            </footer>
            <Navigation mobile />
            <BackToTop />
        </div>
    );
}
