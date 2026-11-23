import { type ReactNode, useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { Info } from "lucide-react";

export function HelpTip({ label, children }: { label: string; children: ReactNode }) {
    const id = useId();
    const trigger = useRef<HTMLButtonElement>(null);
    const bubble = useRef<HTMLSpanElement>(null);
    const [source, setSource] = useState<"hover" | "focus" | "press" | null>(null);
    const open = source !== null;

    useLayoutEffect(() => {
        const tip = bubble.current;
        const button = trigger.current;
        if (!open || !tip || !button) return;
        tip.setAttribute("popover", "manual");
        tip.showPopover?.();

        function place() {
            if (!tip || !button) return;
            const anchor = button.getBoundingClientRect();
            const rect = tip.getBoundingClientRect();
            const centeredLeft = anchor.left + (anchor.width - rect.width) / 2;
            const left = Math.max(8, Math.min(centeredLeft, document.documentElement.clientWidth - rect.width - 8));
            const top = anchor.bottom + rect.height + 12 <= window.innerHeight
                ? anchor.bottom + 5 : Math.max(8, anchor.top - rect.height - 5);
            tip.style.left = left + "px";
            tip.style.top = top + "px";
        }
        place();
        window.addEventListener("resize", place);
        document.addEventListener("scroll", place, true);
        return () => {
            if (tip.isConnected) tip.hidePopover?.();
            window.removeEventListener("resize", place);
            document.removeEventListener("scroll", place, true);
        };
    }, [open, children]);

    useEffect(() => {
        if (!open) return;
        function outside(event: PointerEvent) {
            if (event.target instanceof Node && !trigger.current?.contains(event.target) && !bubble.current?.contains(event.target)) setSource(null);
        }
        function escape(event: KeyboardEvent) {
            if (event.key !== "Escape") return;
            event.preventDefault();
            event.stopPropagation();
            setSource(null);
        }
        document.addEventListener("pointerdown", outside);
        document.addEventListener("keydown", escape, true);
        return () => {
            document.removeEventListener("pointerdown", outside);
            document.removeEventListener("keydown", escape, true);
        };
    }, [open]);

    return <span className="hint-control" onPointerEnter={event => {
        if (event.pointerType === "mouse") setSource(state => state ?? "hover");
    }} onPointerLeave={event => {
        if (event.pointerType === "mouse") setSource(state => state === "hover" ? null : state);
    }}>
        <button type="button" className="hint-button" ref={trigger} aria-label={label} aria-expanded={open}
            aria-describedby={open ? id : undefined} onBlur={() => setSource(null)}
            onFocus={event => { if (event.currentTarget.matches(":focus-visible")) setSource("focus"); }}
            onClick={() => setSource(state => state === "press" ? null : "press")}>
            <Info size={16} aria-hidden="true" />
        </button>
        <span ref={bubble} id={id} className="hint-bubble" role="tooltip" hidden={!open}>{children}</span>
    </span>;
}
