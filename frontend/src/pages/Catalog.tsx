import { HelpTip } from "../components/HelpTip";
import { useEffect, useState } from "react";
import { Search, X } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { useCatalog } from "../data";
import { InviteForm } from "../components/InviteForm";
import { ProductCard } from "../components/Purchase";
import { Empty, LoadError, Loading } from "../components/States";
import { usePageTitle } from "../usePageTitle";

function CatalogSection({ choosing = false }: { choosing?: boolean }) {
    const [params, setParams] = useSearchParams();
    const search = params.get("q") ?? "";
    const [term, setTerm] = useState(search.trim());
    useEffect(() => {
        const timer = window.setTimeout(() => setTerm(search.trim()), 220);
        return () => window.clearTimeout(timer);
    }, [search]);
    const catalog = useCatalog(term);

    function updateSearch(next: string) {
        const filters = new URLSearchParams(params);
        if (next) filters.set("q", next);
        else filters.delete("q");
        setParams(filters, { replace: true, preventScrollReset: true });
    }

    return (
        <section className="min-w-0" id="catalog" aria-labelledby="catalog-title">
            <div className="section-heading catalog-heading">
                <h2 id="catalog-title">{choosing ? "Выберите товар" : "Витрина товаров"}</h2>
                <div className="search-box" role="search" aria-label="Поиск товаров">
                    <Search size={17} aria-hidden="true" />
                    <input
                        type="search"
                        aria-label="Поиск товаров"
                        placeholder="Найти товар"
                        maxLength={256}
                        value={search}
                        onChange={event => updateSearch(event.target.value)}
                    />
                    {search && (
                        <button type="button" aria-label="Очистить поиск" onClick={() => updateSearch("")}>
                            <X size={15} />
                        </button>
                    )}
                </div>
            </div>
            {catalog.isPending ? (
                <Loading>Загружаем товары…</Loading>
            ) : catalog.isError ? (
                <LoadError error={catalog.error} retry={catalog.refetch} />
            ) : catalog.data.length ? (
                <div className="product-grid">
                    {catalog.data.map(product => (
                        <ProductCard key={product.id} product={product} choosing={choosing} />
                    ))}
                </div>
            ) : (
                <Empty title={term ? "Такого товара пока нет" : "Витрина пока пуста"}>
                    {term ? (
                        <button className="text-link" onClick={() => updateSearch("")}>
                            Сбросить поиск
                        </button>
                    ) : (
                        <p>Товары появятся здесь, когда их добавят в каталог.</p>
                    )}
                </Empty>
            )}
        </section>
    );
}

export function CatalogPage() {
    usePageTitle("Витрина");
    return (
        <>
            <section className="hero">
                <img className="hero-art" src="/art/basket.webp" alt="" width="1760" height="704" />
                <div className="hero-copy">
                    <h1>
                        Вместе
                        <br />
                        <em>выгоднее.</em>
                    </h1>
                    <p className="hero-summary">
                        Присоединяйтесь к сбору или откройте свой, чтобы покупать по групповой цене.
                    </p>
                    <a className="button button-dark" data-guide="catalog" href="#catalog">
                        Выбрать товар
                    </a>
                </div>
            </section>
            <section className="invite-banner" aria-labelledby="home-invite-title">
                <div className="invite-banner-heading">
                    <img src="/art/tickets.webp" alt="" width="720" height="480" />
                    <div className="with-hint"><h2 id="home-invite-title">Есть код сбора?</h2>
                        <HelpTip label="Где взять код">Участник может прислать код или ссылку через кнопку «Пригласить в сбор».</HelpTip></div>
                </div>
                <InviteForm submitText="Найти сбор" />
            </section>
            <div className="section-block">
                <CatalogSection />
            </div>
        </>
    );
}

export function ChoosePage() {
    usePageTitle("Новый сбор");
    return (
        <>
            <div className="page-heading">
                <h1>Новый сбор</h1>
            </div>
            <CatalogSection choosing />
        </>
    );
}
