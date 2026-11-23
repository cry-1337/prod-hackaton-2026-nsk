import { type GroupFilters, useGroups } from "../data";
import type { GroupStatus } from "../types";
import { GroupCard } from "./Purchase";
import { Empty, LoadError, Loading } from "./States";

interface Props {
    status: GroupStatus | "all";
    filters: GroupFilters;
    emptyTitle: string;
    emptyMessage?: string;
}

export function GroupList({ status, filters, emptyTitle, emptyMessage }: Props) {
    const feed = useGroups(status, filters);
    const groups = feed.data?.pages.flat() ?? [];

    if (feed.isPending) return <Loading />;
    if (feed.isError && !groups.length) return <LoadError error={feed.error} retry={feed.refetch} />;
    if (!groups.length) return <Empty title={emptyTitle}>{emptyMessage && <p>{emptyMessage}</p>}</Empty>;

    return (
        <>
            <div className="group-list">
                {groups.map(group => (
                    <GroupCard key={group.id} group={group} showOrganizer={!filters.organizerId} />
                ))}
            </div>
            {feed.isFetchNextPageError ? (
                <LoadError error={feed.error} retry={feed.fetchNextPage} />
            ) : feed.hasNextPage && (
                <button
                    className="button button-light mt-5"
                    onClick={() => feed.fetchNextPage()}
                    disabled={feed.isFetchingNextPage}
                >
                    {feed.isFetchingNextPage ? "Загружаем…" : "Показать ещё"}
                </button>
            )}
        </>
    );
}
