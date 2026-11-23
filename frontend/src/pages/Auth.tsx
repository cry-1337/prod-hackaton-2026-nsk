import { type FormEvent, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Eye, EyeOff } from "lucide-react";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router-dom";
import { api, errorMessage } from "../api";
import { Back } from "../components/States";
import { usePageTitle } from "../usePageTitle";
import { setSession, useSession } from "../session";
import type { Session } from "../types";

function returnPath(next: string | null) {
    if (!next || !next.startsWith("/") || next.startsWith("//") || next.includes("\\")) return "/me";
    try {
        const path = new URL(next, window.location.origin);
        if (
            path.origin === window.location.origin &&
            !path.pathname.startsWith("//") &&
            !["/login", "/register"].includes(path.pathname)
        ) {
            return path.pathname + path.search + path.hash;
        }
    } catch {
        return "/me";
    }
    return "/me";
}

export function AuthPage({ register }: { register: boolean }) {
    usePageTitle(register ? "Регистрация" : "Вход");
    const [params] = useSearchParams();
    const next = returnPath(params.get("next"));
    const session = useSession();
    const navigate = useNavigate();
    const [showPassword, setShowPassword] = useState(false);
    const auth = useMutation({
        mutationFn: (body: Record<string, string>) =>
            api<Session>(register ? "/auth/register" : "/auth/login", { method: "POST", body }),
        onSuccess: data => {
            setSession(data);
            navigate(next, { replace: true });
        }
    });
    function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        if (auth.isPending) return;
        const fields = new FormData(event.currentTarget);
        const body: Record<string, string> = {
            email: String(fields.get("email")).trim(),
            password: String(fields.get("password"))
        };
        if (register) body.nickname = String(fields.get("nickname")).trim();
        auth.mutate(body);
    }
    if (session) return <Navigate to={next} replace />;

    return (
        <>
            <Back />
            <div className="auth-card">
                <section className="auth-form">
                    <h1>{register ? "Создать аккаунт" : "С возвращением!"}</h1>
                    <p className="description">
                        {register
                            ? "Один аккаунт для участия и организации сборов."
                            : "Войдите, чтобы участвовать в покупках и открывать свои сборы."}
                    </p>
                    <form onSubmit={submit} className="form-stack">
                        {register && (
                            <label className="field">
                                Никнейм
                                <input
                                    name="nickname"
                                    autoComplete="nickname"
                                    required
                                    maxLength={64}
                                    pattern=".*\S.*"
                                    placeholder="Как вас будут видеть в сборе"
                                />
                            </label>
                        )}
                        <label className="field">
                            Почта
                            <input
                                type="email"
                                name="email"
                                autoComplete="email"
                                placeholder="name@example.ru"
                                required
                                maxLength={254}
                            />
                        </label>
                        <div className="field">
                            <label htmlFor="auth-password">Пароль</label>
                            <span className="password-field">
                                <input
                                    id="auth-password"
                                    type={showPassword ? "text" : "password"}
                                    name="password"
                                    autoComplete={register ? "new-password" : "current-password"}
                                    required
                                    minLength={register ? 12 : undefined}
                                    maxLength={128}
                                    placeholder={register ? "От 12 символов" : "Введите пароль"}
                                />
                                <button
                                    type="button"
                                    aria-label={showPassword ? "Скрыть пароль" : "Показать пароль"}
                                    onClick={() => setShowPassword(!showPassword)}
                                >
                                    {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                                </button>
                            </span>
                        </div>
                        {auth.isError && (
                            <p role="alert" className="form-error">
                                {errorMessage(auth.error)}
                            </p>
                        )}
                        <button type="submit" className="button button-dark" disabled={auth.isPending}>
                            {auth.isPending ? "Подождите…" : register ? "Зарегистрироваться" : "Войти"}
                        </button>
                    </form>
                    <p className="auth-switch">
                        {register ? "Уже есть аккаунт?" : "Впервые здесь?"}{" "}
                        <Link
                            to={
                                (register ? "/login" : "/register") + "?next=" + encodeURIComponent(next)
                            }
                        >
                            {register ? "Войти" : "Зарегистрироваться"}
                        </Link>
                    </p>
                </section>
                <div className="auth-art">
                    <img src="/art/crates.webp" alt="" />
                </div>
            </div>
        </>
    );
}
