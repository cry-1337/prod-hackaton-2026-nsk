import { useLocation, useSearchParams } from "react-router-dom";

export type GroupSortOrder = "likes" | "newest";

export function useGroupSort() {
    const { state } = useLocation();
    const [params, setParams] = useSearchParams();
    const order: GroupSortOrder = params.get("sort") === "newest" ? "newest" : "likes";

    function setOrder(value: GroupSortOrder) {
        setParams(current => {
            const next = new URLSearchParams(current);
            if (value === "newest") next.set("sort", value);
            else next.delete("sort");
            return next;
        }, { replace: true, preventScrollReset: true, state });
    }

    return { order, setOrder };
}
