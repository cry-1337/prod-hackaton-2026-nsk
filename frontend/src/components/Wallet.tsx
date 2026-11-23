import { inPractice } from "../practice";
import { HelpTip } from "./HelpTip";
import { useEffect, useId, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useLocation } from "react-router-dom";
import { api, errorMessage } from "../api";
import { queries, useBalance } from "../data";
import { money, parseRubles } from "../format";
import type { Session } from "../types";

const maxTopUpKopecks = 100_000_000;

function topUpError(value: string) {
    const input = value.trim();
    if (!input) return "Введите сумму пополнения.";
    const rubles = Number(input.replace(",", "."));
    if (rubles <= 0) return "Сумма пополнения должна быть больше нуля.";
    if (rubles > maxTopUpKopecks / 100) return "За одно пополнение можно внести до " + money(maxTopUpKopecks) + ".";
    return "Введите сумму в рублях, максимум два знака после запятой.";
}

export function Wallet({ session }: { session: Session }) {
    const balance = useBalance(session);
    const { hash } = useLocation();
    const section = useRef<HTMLElement>(null);
    const practice = inPractice();
    const [show, setShow] = useState(!practice && hash === "#wallet");
    useEffect(() => {
        if (hash !== "#wallet") return;
        const frame = requestAnimationFrame(() => section.current?.scrollIntoView({ block: "start" }));
        return () => cancelAnimationFrame(frame);
    }, [hash]);
    const [value, setValue] = useState("");
    const key = useRef<{ amount: number; id: string }>();
    const amount = parseRubles(value);
    const valid = amount !== undefined && amount <= maxTopUpKopecks;
    const [touched, setTouched] = useState(false);
    const error = !valid && (value !== "" || touched) ? topUpError(value) : null;
    const errorId = useId();
    const deposit = useMutation({
        mutationFn: (sum: number) => {
            if (key.current?.amount !== sum) key.current = { amount: sum, id: crypto.randomUUID() };
            return api("/me/balance/top-ups", { method: "POST", token: session.accessToken,
                creationKey: key.current.id, body: { amountKopecks: sum } });
        },
        onSuccess: async () => {
            key.current = undefined;
            setTouched(false);
            setValue("");
            setShow(false);
            await queries.invalidateQueries({ queryKey: ["balance", session.user.id] });
        }
    });
    return <section className="panel form-stack mt-4" id="wallet" ref={section}>
        <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="with-hint"><h2>Кошелёк{balance.data && " · " + money(balance.data.balanceKopecks)}</h2>
                <HelpTip label="Как работает кошелёк">{practice ? "В начале практики у вас 5 000 ₽ на учебные покупки." : "Пополнение добавляет тестовые деньги. Отсюда списывается предоплата и сюда приходят возвраты."}</HelpTip></div>
            {!practice && <button className="button button-light" onClick={() => setShow(!show)} disabled={deposit.isPending}>
                {show ? "Закрыть" : "Пополнить"}
            </button>}
        </div>
        {balance.isError && <p role="alert">{errorMessage(balance.error)}</p>}
        {show && <form className="form-stack" onSubmit={event => {
            event.preventDefault();
            if (valid && !deposit.isPending) deposit.mutate(amount);
        }}>
            <label className="field">Сумма пополнения, ₽
                <input inputMode="decimal" value={value} aria-invalid={!!error} aria-describedby={error ? errorId : undefined}
                    onChange={event => setValue(event.target.value)} onBlur={() => setTouched(true)} />
            </label>
            {error && <p id={errorId} className="field-error" role="alert">{error}</p>}
            <button className="button button-dark" disabled={!valid || deposit.isPending}>Пополнить кошелёк</button>
        </form>}
        {deposit.isError && <p role="alert">{errorMessage(deposit.error)}</p>}
    </section>;
}
