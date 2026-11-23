import { inPractice, leavePractice } from "../practice";
import { useHref } from "react-router-dom";
import { useId, useRef, useState } from "react";
import { Check, Copy, QrCode, Share2, X } from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import { groupPath } from "../format";
import type { Group } from "../types";

function Invitation({ group, link }: { group: Group; link: string }) {
    const input = useRef<HTMLInputElement>(null);
    const [feedback, setFeedback] = useState<"copied" | "manual" | null>(null);

    async function copy() {
        try {
            await navigator.clipboard.writeText(link);
            setFeedback("copied");
        } catch {
            input.current?.focus();
            input.current?.select();
            setFeedback("manual");
        }
    }

    async function share() {
        try {
            await navigator.share({ title: group.productName + " · Вместе", url: link });
        } catch (error) {
            if (!(error instanceof DOMException && error.name === "AbortError")) await copy();
        }
    }

    return (
        <>
            <figure className="invitation-ticket">
                <QRCodeSVG
                    className="invitation-qr"
                    value={link}
                    size={216}
                    level="M"
                    marginSize={4}
                    fgColor="#18372b"
                    role="img"
                    aria-label="QR-код приглашения в сбор"
                />
                <figcaption>
                    <span>Код сбора</span>
                    <strong>{group.inviteCode}</strong>
                </figcaption>
            </figure>
            <label className="field">
                Ссылка на сбор
                <input ref={input} readOnly value={link} onFocus={event => event.target.select()} />
            </label>
            <div className="mt-3 flex flex-wrap gap-2">
                <button type="button" className="button button-dark" onClick={copy}>
                    {feedback === "copied" ? <Check size={16} /> : <Copy size={16} />}
                    Скопировать ссылку
                </button>
                {typeof navigator.share === "function" && (
                    <button type="button" className="button button-light" onClick={share}>
                        <Share2 size={16} />
                        Поделиться
                    </button>
                )}
            </div>
            <p className="share-feedback" role="status">
                {feedback === "copied" && "Ссылка скопирована"}
                {feedback === "manual" && "Скопируйте выделенную ссылку."}
            </p>
        </>
    );
}

export function Share({ group }: { group: Group }) {
    const invitePath = useHref(groupPath(group.inviteCode));
    const dialog = useRef<HTMLDialogElement>(null);
    const title = useId();
    const [open, setOpen] = useState(false);

    function showInvitation() {
        dialog.current?.showModal();
        setOpen(true);
    }

    return (
        <>
            <button type="button" data-guide="invite" className="button button-light invite-trigger" aria-haspopup="dialog" onClick={showInvitation}>
                <QrCode size={17} aria-hidden="true" />
                Пригласить в сбор
            </button>
            <dialog
                ref={dialog}
                className="share-dialog"
                aria-labelledby={title}
                onClose={() => setOpen(false)}
                onClick={event => {
                    if (event.target === event.currentTarget) event.currentTarget.close();
                }}
            >
                <div className="share-dialog-body">
                    <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                            <h2 id={title}>Приглашение в сбор</h2>
                            <p className="mt-1 text-sm text-muted">{group.productName}</p>
                        </div>
                        <button
                            type="button"
                            className="icon-button"
                            aria-label="Закрыть приглашение"
                            onClick={() => dialog.current?.close()}
                        >
                            <X size={19} aria-hidden="true" />
                        </button>
                    </div>
                    {inPractice() && <button type="button" className="text-link" onClick={leavePractice}>Выйти из практики</button>}
                    {open && <Invitation group={group} link={new URL(invitePath, window.location.origin).href} />}
                </div>
            </dialog>
        </>
    );
}
