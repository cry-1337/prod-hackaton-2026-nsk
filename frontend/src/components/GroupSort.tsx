import type { GroupSortOrder } from "../useGroupSort";

interface Props {
    order: GroupSortOrder;
    setOrder: (order: GroupSortOrder) => void;
}

export function GroupSort({ order, setOrder }: Props) {
    return (
        <label className="group-sort">
            Сортировка
            <select
                aria-label="Сортировка сборов"
                value={order}
                onChange={event => setOrder(event.target.value === "newest" ? "newest" : "likes")}
            >
                <option value="likes">По лайкам организатора</option>
                <option value="newest">Сначала новые</option>
            </select>
        </label>
    );
}
