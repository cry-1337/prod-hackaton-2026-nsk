import { type FormEvent, useId, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { errorMessage } from "../api";
import { groupQuery } from "../data";
import { groupPath } from "../format";

export function InviteForm({ submitText }: { submitText?: string }) {
    const location = useLocation();
    const [code, setCode] = useState("");
    const [submitted, setSubmitted] = useState("");
    const [validation, setValidation] = useState("");
    const id = useId();
    const lookup = useQuery({
        ...groupQuery(submitted),
        enabled: Boolean(submitted),
        staleTime: 0,
        retry: false,
        refetchOnWindowFocus: false
    });
    const error = validation || (lookup.isError ? errorMessage(lookup.error) : "");

    function submit(event: FormEvent) {
        event.preventDefault();
        const value = code.trim().toLowerCase();
        if (!/^[a-z0-9-]{1,64}$/.test(value)) {
            setValidation("Введите код латинскими буквами и цифрами. Допускается дефис.");
            return;
        }
        if (submitted === value) {
            void lookup.refetch();
        } else {
            setSubmitted(value);
        }
    }

    return (
        <form
            onSubmit={submit}
            className="invite-compact"
            aria-label="Поиск сбора по коду"
        >
            <label className="sr-only" htmlFor={id}>
                Код сбора
            </label>
            <div className="code-input">
                <input
                    id={id}
                    placeholder={submitText ? "Введите код" : "Код сбора"}
                    value={code}
                    onChange={event => {
                        setCode(event.target.value);
                        setSubmitted("");
                        setValidation("");
                    }}
                    maxLength={96}
                    autoCapitalize="none"
                    spellCheck={false}
                    required
                    aria-describedby={id + "-feedback"}
                />
                <button type="submit" aria-label="Найти сбор по коду" disabled={lookup.isFetching}>
                    <Search size={19} aria-hidden="true" />
                    {submitText}
                </button>
            </div>
            <div id={id + "-feedback"} className="invite-feedback" role="status">
                {lookup.isFetching ? (
                    "Ищем сбор…"
                ) : error ? (
                    <p className="form-error">{error}</p>
                ) : submitted && lookup.data ? (
                    <Link
                        to={groupPath(lookup.data.id)}
                        state={{
                            from: location.pathname + location.search,
                            fromLabel: location.pathname === "/" ? "На витрину" : "К списку сборов"
                        }}
                        className="text-link underline"
                    >
                        Открыть сбор «{lookup.data.productName}»
                    </Link>
                ) : null}
            </div>
        </form>
    );
}
