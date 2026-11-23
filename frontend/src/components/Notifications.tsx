import { useState } from "react";
import { useInfiniteQuery, useMutation, useQuery } from "@tanstack/react-query";
import { Bell, Check } from "lucide-react";
import { Link } from "react-router-dom";
import { api, errorMessage } from "../api";
import { queries } from "../data";
import { deadlineLabel, groupPath, money } from "../format";
import type { OrderNotification, Session } from "../types";
import { LoadError, Loading } from "./States";
import { Dialog } from "./Dialog";

function NotificationList({ session, onOpenGroup }: { session: Session; onOpenGroup: () => void }) {
    const [unreadOnly, setUnreadOnly] = useState(false);
    const limit = 15;
    const key = ["notifications", session.user.id];
    const feed = useInfiniteQuery({
        queryKey: [...key, "list", unreadOnly],
        initialPageParam: 0,
        queryFn: ({ signal, pageParam }) => api<OrderNotification[]>(
            "/me/notifications?unreadOnly=" + unreadOnly + "&limit=" + limit + "&offset=" + pageParam,
            { signal, token: session.accessToken }
        ),
        getNextPageParam: (page, _, offset) => page.length === limit ? offset + limit : undefined,
        refetchInterval: 20_000
    });
    const read = useMutation({
        mutationFn: (id: string) => api<void>("/me/notifications/" + id + "/read", { method: "PUT", token: session.accessToken }),
        onSuccess: () => queries.invalidateQueries({ queryKey: key })
    });
    const items = feed.data?.pages.flat() ?? [];
    return <section className="feature-list" aria-label="Список уведомлений">
        <div className="tabs" role="group" aria-label="Фильтр уведомлений">
            <button type="button" aria-pressed={!unreadOnly} onClick={() => setUnreadOnly(false)}>Все</button>
            <button type="button" aria-pressed={unreadOnly} onClick={() => setUnreadOnly(true)}>Непрочитанные</button>
        </div>
        {feed.isPending ? <Loading /> : feed.isError ? <LoadError error={feed.error} retry={feed.refetch} /> :
            items.length ? <ul className="activity-list">
                {items.map(item => <li key={item.id}>
                    <div>
                        <Link className="text-link" to={groupPath(item.groupId)} onClick={() => { if (!item.readAt) read.mutate(item.id); onOpenGroup(); }}>
                            {item.productName}
                        </Link>
                        <p>{item.kind === "refund" ? "Возврат" : "Заказ доставлен"} · {money(item.amountKopecks)}</p>
                        <time dateTime={item.createdAt}>{deadlineLabel(item.createdAt)}</time>
                    </div>
                    {!item.readAt && <button className="icon-button" disabled={read.isPending} type="button"
                        aria-label={"Отметить прочитанным · " + item.productName} title="Отметить прочитанным" onClick={() => read.mutate(item.id)}>
                        <Check size={17} aria-hidden="true" />
                    </button>}
                </li>)}
            </ul> : <p className="field-hint">{unreadOnly ? "Новых уведомлений нет" : "Уведомлений пока нет"}</p>}
        {feed.hasNextPage && <button className="text-link" disabled={feed.isFetchingNextPage} onClick={() => feed.fetchNextPage()}>Показать ещё</button>}
        {read.isError && <p className="form-error" role="alert">{errorMessage(read.error)}</p>}
    </section>;
}

export function NotificationBell({ session }: { session: Session }) {
    const [open, setOpen] = useState(false);
    const unread = useQuery({
        queryKey: ["notifications", session.user.id, "indicator"],
        queryFn: ({signal}) => api<OrderNotification[]>("/me/notifications?unreadOnly=true&limit=1", { signal, token: session.accessToken }),
        refetchInterval: 20_000
    });
    const hasUnread = !!unread.data?.length;
    return <>
        <button className="notification-bell" type="button" aria-haspopup="dialog" aria-expanded={open}
            aria-label={hasUnread ? "Уведомления, есть непрочитанные" : "Уведомления"} title="Уведомления" onClick={() => setOpen(true)}>
            <Bell size={19} aria-hidden="true" />{hasUnread && <i aria-hidden="true" />}
        </button>
        {open && <Dialog title="Уведомления" placement="notifications" onClose={() => setOpen(false)}>
            <NotificationList session={session} onOpenGroup={() => setOpen(false)} />
        </Dialog>}
    </>;
}
