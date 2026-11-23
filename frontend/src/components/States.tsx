import { type ReactNode, useState } from "react";
import { CircleAlert, LoaderCircle, PackageOpen } from "lucide-react";
import { Link } from "react-router-dom";
import { errorMessage } from "../api";

export function Loading({ children = "Загружаем…" }: { children?: ReactNode }) {
    return (
        <div className="notice" role="status">
            <LoaderCircle className="animate-spin motion-reduce:animate-none" size={20} />
            {children}
        </div>
    );
}

export function LoadError({ error, retry }: { error: unknown; retry: () => void }) {
    return (
        <div className="notice notice-error" role="alert">
            <CircleAlert size={20} className="shrink-0" />
            <p>{errorMessage(error)}</p>
            <button onClick={retry} className="text-link">
                Повторить
            </button>
        </div>
    );
}

export function Empty({ title, children }: { title: string; children?: ReactNode }) {
    return (
        <div className="empty">
            <h3>{title}</h3>
            {children}
        </div>
    );
}

export function Back({ to = "/", children = "Витрина" }: { to?: string; children?: ReactNode }) {
    return (
        <Link className="back-link" to={to}>
            {children}
        </Link>
    );
}

export function ProductPhoto({
    url,
    name,
    className = ""
}: {
    url: string | null;
    name: string;
    className?: string;
}) {
    const [failed, setFailed] = useState<string | null>(null);
    return (
        <div className={"product-photo " + className}>
            {url && url !== failed ? (
                <img src={url} alt={name} onError={() => setFailed(url)} />
            ) : (
                <PackageOpen
                    size={42}
                    strokeWidth={1.4}
                    aria-label="Фото товара отсутствует"
                    role="img"
                />
            )}
        </div>
    );
}
