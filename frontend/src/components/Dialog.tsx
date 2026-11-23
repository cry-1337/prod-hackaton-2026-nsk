import { useEffect, useId, useRef, type ReactNode } from "react";
import { X } from "lucide-react";

export function Dialog({ title, children, onClose, placement, closeDisabled = false }: {
    title: string;
    children: ReactNode;
    onClose: () => void;
    placement?: "notifications";
    closeDisabled?: boolean;
}) {
    const ref = useRef<HTMLDialogElement>(null);
    const heading = useId();

    useEffect(() => {
        const dialog = ref.current;
        dialog?.showModal();
        return () => dialog?.close();
    }, []);

    return <dialog
        ref={ref}
        className={placement ? "feature-dialog notifications-dialog" : "feature-dialog"}
        aria-labelledby={heading}
        onCancel={event => { event.preventDefault(); if (!closeDisabled) onClose(); }}
        onClick={event => { if (!closeDisabled && event.target === event.currentTarget) onClose(); }}
    >
        <div className="feature-dialog-body">
            <header className="flex items-center justify-between gap-3">
                <h2 id={heading}>{title}</h2>
                <button type="button" className="icon-button" disabled={closeDisabled} onClick={onClose} aria-label="Закрыть">
                    <X size={18} aria-hidden="true" />
                </button>
            </header>
            {children}
        </div>
    </dialog>;
}
