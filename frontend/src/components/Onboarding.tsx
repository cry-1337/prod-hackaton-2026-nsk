import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { startPractice } from "../practice";
import { finishOnboarding, needsOnboarding, onboardingKey } from "../onboarding";
import { ArrowLeft, ArrowRight, BookOpen, ShoppingBasket } from "lucide-react";

const slides = [
    { title: "", art: "welcome", text: "Здесь можно заказывать продукты вместе. За три коротких шага покажем, как устроена покупка в сборе." },
    { title: "Один товар, общая скидка", art: "tiers", text: "Выберите товар на витрине. В его карточке видно, сколько нужно заказать всем вместе, чтобы цена за штуку или килограмм стала ниже." },
    { title: "Присоединяйтесь к сбору", art: "join", text: "Найдите открытый сбор или введите код от друга. Посмотрите цену и срок, затем выберите, сколько товара хотите заказать." },
    { title: "Откройте свой сбор", art: "create", text: "Выберите товар, укажите количество, максимальную цену и срок. После подтверждения оплаты отправьте друзьям ссылку или код приглашения." },
    { title: "Создадим учебный сбор?", art: "tour", text: "Вы сами выберете товар, оформите заказ и увидите свой сбор от лица организатора. Учебный кошелёк и адрес уже готовы." }
];
const introSteps = slides.length - 2; // The welcome and tour invitation frame the three lessons.

function ProgressStrip({ label, done, total }: { label: string; done: number; total: number }) {
    return <div className="learning-progress">
        <div className="learning-progress-labels"><strong>{label}</strong><span>Пройдено {done} из {total} · Осталось {total - done}</span></div>
        <div className="learning-track" role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={total} aria-valuenow={done}>
            <span style={{ width: (done / total * 100) + "%" }} />
        </div>
    </div>;
}

function Presentation({ startTour, finish }: { startTour: () => void; finish: () => void }) {
    const [page, setPage] = useState(0);
    const dialog = useRef<HTMLDialogElement>(null);
    const heading = useRef<HTMLHeadingElement>(null);
    const slide = slides[page];
    const last = page === slides.length - 1;
    useLayoutEffect(() => {
        const modal = dialog.current!;
        modal.showModal();
        return () => modal.close();
    }, []);
    useLayoutEffect(() => { heading.current?.focus({ preventScroll: true }); }, [page]);

    return <dialog ref={dialog} className="learning-dialog" aria-labelledby="learning-title" aria-describedby="learning-copy"
        onCancel={event => { event.preventDefault(); finish(); }}>
        <ProgressStrip label="Знакомство" done={Math.max(0, page - 1)} total={introSteps} />
        <div key={page} className="learning-slide">
            <div className="learning-art">
                <img src={"/art/guide/" + slide.art + ".webp"} width="1080" height="432" alt="" />
                <span className="learning-wordmark" aria-hidden="true"><ShoppingBasket size={16} /> вместе.</span>
            </div>
            <div className="learning-copy">
                <h2 ref={heading} tabIndex={-1} id="learning-title">{page === 0 ? <>Добро пожаловать<br /><em>во «Вместе»!</em></> : slide.title}</h2>
                <p id="learning-copy">{slide.text}</p>
            </div>
        </div>
        <div className="learning-actions">
            <button className="text-link" onClick={finish}>Пропустить</button>
            <div className="flex items-center gap-2">
                {page > 0 && <button className="icon-button" aria-label="Назад" onClick={() => setPage(page - 1)}><ArrowLeft size={17} /></button>}
                <button className="button button-dark" onClick={() => last ? startTour() : setPage(page + 1)}>
                    {page === 0 ? "Пройти обучение" : last ? "Начать практику" : page === introSteps ? "Понятно" : "Далее"}<ArrowRight size={16} />
                </button>
            </div>
        </div>
    </dialog>;
}

type Phase = "welcome" | "closed";

export function Onboarding() {
    const [phase, setPhase] = useState<Phase>(() => needsOnboarding() ? "welcome" : "closed");
    useEffect(() => {
        const sync = (event: StorageEvent) => {
            if (event.key === onboardingKey && event.newValue === "finished") setPhase("closed");
        };
        window.addEventListener("storage", sync);
        return () => window.removeEventListener("storage", sync);
    }, []);

    function finish() {
        finishOnboarding();
        setPhase("closed");
    }

    if (phase === "closed") return <button className="text-link" onClick={() => setPhase("welcome")}><BookOpen size={16} />Как покупать вместе</button>;
    return <Presentation finish={finish} startTour={() => { finish(); startPractice(); }} />;
}
