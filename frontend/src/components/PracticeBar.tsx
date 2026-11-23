import { useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useLocation } from "react-router-dom";
import { leavePractice } from "../practice";

const tips = {
    catalog: ["Откройте витрину", "Нажмите «Выбрать товар», чтобы перейти к карточкам."],
    product: ["Выберите товар", "Нажмите на карточку груш или мёда."],
    create: ["Начните свой сбор", "Нажмите «Создать сбор»."],
    price: ["Выберите цену", "Нажмите на ступень с максимальной ценой, которую готовы заплатить."],
    quantity: ["Укажите количество", "Введите нужный объём или оставьте предложенный. Затем перейдите к выбору срока."],
    duration: ["Выберите срок", "Нажмите на один из вариантов, чтобы задать время окончания сбора."],
    review: ["Проверьте условия", "Нажмите «Продолжить», чтобы перейти к адресу и предоплате."],
    address: ["Укажите адрес", "Нажмите «Подставить учебный адрес»."],
    confirm: ["Создайте учебный сбор", "Проверьте сумму и нажмите «Создать и оплатить». Спишутся учебные деньги."],
    invite: ["Пригласите участников", "Откройте «Пригласить в сбор», чтобы посмотреть код и QR."],
    mine: ["Найдите свой сбор", "Откройте «Мои сборы» в навигации, чтобы найти свой заказ."],
    finish: ["Практика пройдена", "Ваш заказ появился в кабинете. Можно вернуться на сайт."]
} as const;

type Step = keyof typeof tips;
const sequence = Object.keys(tips) as Step[];
const budgetTip = ["Проверьте учебный баланс", "Нажмите «Взять», чтобы выбрать количество по вашему бюджету."];

function routeSteps(path: string, hash: string): Step[] {
    if (path === "/") return hash === "#catalog" ? ["product"] : ["catalog", "product"];
    if (path === "/new") return ["product"];
    if (path.endsWith("/create")) return ["price", "quantity", "duration", "review", "address", "confirm"];
    if (path.startsWith("/products/") || path === "/groups") return ["create"];
    if (path.startsWith("/groups/") || path.startsWith("/invite/")) return ["invite", "mine"];
    return path === "/me" ? ["finish"] : [];
}

function Spotlight({ path, hash, visible }: { path: string; hash: string; visible: boolean }) {
    const completedSteps = useRef(new Set<Step>());
    const goToDuration = useRef<() => void>();
    const card = useRef<HTMLDivElement>(null);
    const [anchor, setAnchor] = useState<{ step: Step; budget: boolean; rect: DOMRect; height: number }>();

    useLayoutEffect(() => {
        if (!visible) return;
        const order = routeSteps(path, hash);
        const done = completedSteps.current;
        let current: HTMLElement | undefined;
        let frame = 0;

        function measure() {
            frame = 0;
            let target: HTMLElement | undefined;
            let step: Step | undefined;
            const budget = document.querySelector<HTMLElement>('[data-guide="budget"]');
            if (budget && !document.querySelector("dialog[open]")) {
                target = budget;
                step = "quantity";
            } else if (!document.querySelector("dialog[open]")) {
                for (const candidate of order) {
                    if (done.has(candidate)) continue;
                    target = Array.from(document.querySelectorAll<HTMLElement>('[data-guide="' + candidate + '"]'))
                        .find(el => el.getClientRects().length > 0);
                    if (target) { step = candidate; break; }
                }
            }
            if (target !== current) {
                current = target;
                if (target) {
                    const rect = target.getBoundingClientRect();
                    const header = document.querySelector(".site-header")?.getBoundingClientRect().bottom ?? 0;
                    if ((rect.top < header && !target.closest("header, nav")) || rect.bottom > window.innerHeight - 72) {
                        target.scrollIntoView({ block: "center", behavior: "instant" });
                    }
                }
            }
            if (!target || !step) { setAnchor(undefined); return; }
            const rect = target.getBoundingClientRect();
            const height = card.current?.offsetHeight ?? 145;
            setAnchor(prev => prev?.step === step && prev.budget === !!budget && prev.height === height && prev.rect.x === rect.x &&
                prev.rect.y === rect.y && prev.rect.width === rect.width && prev.rect.height === rect.height
                ? prev : { step: step!, budget: !!budget, rect, height });
        }
        function schedule() {
            if (!frame) frame = requestAnimationFrame(measure);
        }
        function advance(event: Event) {
            if (!(event.target instanceof Element)) return;
            const element = event.target.closest<HTMLElement>("[data-guide]");
            const step = element?.dataset.guide as Step | undefined;
            if (!step || !order.includes(step)) return;
            const completed = event.type === "focusout"
                ? step === "quantity" && element?.getAttribute("aria-invalid") !== "true"
                : step === "catalog" || step === "invite" ||
                  (step === "price" && event.target.matches("input[type=radio]")) ||
                  (step === "duration" && !!event.target.closest("button"));
            if (completed) {
                for (const earlier of order.slice(0, order.indexOf(step) + 1)) done.add(earlier);
                schedule();
            }
        }
        goToDuration.current = () => {
            if (current?.dataset.guide !== "quantity") return;
            if (current.getAttribute("aria-invalid") === "true") { current.focus(); return; }
            done.add("quantity");
            document.querySelector<HTMLButtonElement>('[data-guide="duration"] button')?.focus({ preventScroll: true });
            schedule();
        };
        const observer = new MutationObserver(schedule);
        observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["data-guide", "open"] });
        const resize = new ResizeObserver(schedule);
        resize.observe(document.body);
        if (card.current) resize.observe(card.current);
        window.addEventListener("resize", schedule);
        window.addEventListener("scroll", schedule, true);
        document.addEventListener("click", advance, true);
        document.addEventListener("focusout", advance, true);
        schedule();
        return () => {
            cancelAnimationFrame(frame);
            observer.disconnect();
            resize.disconnect();
            window.removeEventListener("resize", schedule);
            window.removeEventListener("scroll", schedule, true);
            document.removeEventListener("click", advance, true);
            document.removeEventListener("focusout", advance, true);
        };
    }, [path, hash, visible]);

    if (!visible) return null;

    const rect = anchor?.rect;
    const width = Math.min(340, window.innerWidth - 24);
    const top = rect && anchor && (rect.bottom + anchor.height + 20 <= window.innerHeight ?
        rect.bottom + 10 : Math.max(6, rect.top - anchor.height - 10));
    const left = Math.max(12, Math.min(rect?.left ?? 12, window.innerWidth - width - 12));
    const copy = anchor && (anchor.budget ? budgetTip : tips[anchor.step]);
    return createPortal(<div className="practice-overlay">
        {rect && <div className="practice-spot" aria-hidden="true" style={{
            top: rect.top - 3, left: rect.left - 3, width: rect.width + 6, height: rect.height + 6
        }} />}
        <div ref={card} className="practice-tip" hidden={!anchor} style={{ top, left, width }} aria-label="Подсказка практики" role="region">
            {anchor && copy && <>
                <div className="learning-progress">
                    <div className="learning-progress-labels"><strong>Тур по сайту</strong>
                        <span>Шаг {sequence.indexOf(anchor.step) + 1} из {sequence.length}</span></div>
                    <div className="learning-track"><span style={{ width: ((sequence.indexOf(anchor.step) + 1) / sequence.length * 100) + "%" }} /></div>
                </div>
                <h2 aria-live="polite">{copy[0]}</h2><p>{copy[1]}</p>
                <div className="practice-tip-actions">
                    <button className="text-link" type="button" onClick={leavePractice}>Выйти из практики</button>
                    {anchor.step === "quantity" && !anchor.budget && <button type="button" className="text-link"
                    onPointerDown={event => event.preventDefault()} onClick={() => goToDuration.current?.()}>К выбору срока</button>}
                </div>
            </>}
        </div>
    </div>, document.body);
}

export function PracticeBar() {
    const { pathname, hash } = useLocation();
    const [visible, setVisible] = useState(true);
    return <aside className="practice-bar" aria-label="Учебный режим">
        <div className="frame"><strong>Практика · Анна</strong><div className="flex flex-wrap gap-x-3">
            <button className="text-link" aria-pressed={visible} onClick={() => setVisible(!visible)}>
                {visible ? "Скрыть подсказки" : "Показать подсказки"}
            </button>
            <button data-guide="finish" className="text-link" onClick={leavePractice}>Выйти из практики</button>
        </div></div>
        <Spotlight key={pathname} path={pathname} hash={hash} visible={visible} />
    </aside>;
}
