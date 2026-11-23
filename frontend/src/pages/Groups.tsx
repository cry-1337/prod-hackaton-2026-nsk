import { HelpTip } from "../components/HelpTip";
import { useDeferredValue, useState } from "react";
import { Plus, Search, SlidersHorizontal } from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import { GroupList } from "../components/GroupList";
import { GroupSort } from "../components/GroupSort";
import { parseRubles } from "../format";
import { useGroupSort } from "../useGroupSort";
import { usePageTitle } from "../usePageTitle";

const filters = [
    { value: "open", label: "Открытые" },
    { value: "successful", label: "Доставленные" },
    { value: "cancelled", label: "Отменённые" },
    { value: "all", label: "Все" }
] as const;

export function GroupsPage() {
    usePageTitle("Сборы");
    const [params, setParams] = useSearchParams();
    const selected = filters.find(filter => filter.value === params.get("status"))?.value ?? "open";
    const search = params.get("q") ?? "";
    const price = params.get("price") ?? "";
    const hours = params.get("end") ?? "";
    const sort = useGroupSort();
    const [filtersOpen, setFiltersOpen] = useState(false);
    const activeCount = [selected !== "open", !!price, !!hours, sort.order !== "likes"].filter(Boolean).length;
    const term = useDeferredValue(search.trim());
    const amount = parseRubles(price);
    const invalidPrice = !!price && !amount;

    function update(key: string, value: string) {
        const next = new URLSearchParams(params);
        if (value) next.set(key, value);
        else next.delete(key);
        if (key === "status" && value !== "open" && value !== "all" && value !== "") next.delete("end");
        setParams(next, { replace: true, preventScrollReset: true });
    }

    return (
        <>
            <div className="page-heading heading-actions">
                <div className="with-hint"><h1>Сборы</h1><HelpTip label="Как выбрать сбор">
                    Ищите по товару, организатору или коду. Число участников включает все действующие заявки. Цены и сроки есть внутри сбора.
                </HelpTip></div>
                <Link className="button button-dark" data-guide="create" to="/new">
                    <Plus size={17} />
                    Создать сбор
                </Link>
            </div>
            <section aria-label="Лента сборов">
                <div className="feed-controls">
                    <div className="feed-searches">
                        <div role="search" aria-label="Поиск сборов" className="search-box feed-search">
                            <Search size={18} aria-hidden="true" />
                            <input
                                type="search"
                                aria-label="Поиск сборов"
                                maxLength={256}
                                placeholder="Найти сбор"
                                title="По товару, организатору или коду"
                                value={search}
                                onChange={event => update("q", event.target.value)}
                            />
                        </div>
                        <button type="button" className="button button-light filter-toggle" aria-controls="group-filters"
                            aria-expanded={filtersOpen} onClick={() => setFiltersOpen(!filtersOpen)}>
                            <SlidersHorizontal size={17} aria-hidden="true" />
                            Фильтры{activeCount > 0 && <span>{activeCount}</span>}
                        </button>
                    </div>
                    <div id="group-filters" className="feed-options" data-open={filtersOpen}>
                        <div className="tabs" role="group" aria-label="Статус сбора">
                            {filters.map(filter => (
                                <button
                                    key={filter.value}
                                    aria-pressed={selected === filter.value}
                                    onClick={() => update("status", filter.value === "open" ? "" : filter.value)}
                                >
                                    {filter.label}
                                </button>
                            ))}
                        </div>
                        <div className="filter-row">
                            <label>
                                Цена до, ₽
                                <input
                                    inputMode="decimal"
                                    maxLength={13}
                                    placeholder="Любая"
                                    value={price}
                                    aria-invalid={invalidPrice}
                                    onChange={event => update("price", event.target.value)}
                                />
                            </label>
                            {(selected === "open" || selected === "all") && (
                                <label>
                                    Завершение
                                    <select value={hours} onChange={event => update("end", event.target.value)}>
                                        <option value="">Любой срок</option>
                                        <option value="24">За 24 часа</option>
                                        <option value="72">За 3 дня</option>
                                    </select>
                                </label>
                            )}
                            <GroupSort {...sort} />
                        </div>
                    </div>
                    {invalidPrice && <p role="alert" className="form-error">Введите положительную цену в рублях.</p>}
                </div>
                {!invalidPrice && (
                    <GroupList
                        status={selected}
                        filters={{ search: term, maxPriceKopecks: amount, endsWithinHours: hours, byLikes: sort.order === "likes" }}
                        emptyTitle={params.size ? "Сборы не найдены" : "Здесь пока нет сборов"}
                        emptyMessage="Попробуйте другой фильтр или создайте сбор из каталога."
                    />
                )}
            </section>
        </>
    );
}
