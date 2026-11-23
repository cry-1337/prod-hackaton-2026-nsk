import { HelpTip } from "./HelpTip";
import { useInfiniteQuery } from "@tanstack/react-query";
import { UsersRound } from "lucide-react";
import { Link } from "react-router-dom";
import { api } from "../api";
import { groupPath, money, unitLabel } from "../format";
import type { Group, GroupParticipant } from "../types";
import { LoadError, Loading } from "./States";

export function Participants({ group }: { group: Group }) {
    const limit = 12;
    const roster = useInfiniteQuery({
        queryKey: ["participants", group.id],
        initialPageParam: 0,
        queryFn: ({ pageParam, signal }) => api<GroupParticipant[]>(
            groupPath(group.id) + "/participants?offset=" + pageParam + "&limit=" + limit, { signal }
        ),
        getNextPageParam: (last, _pages, offset) => last.length === limit ? offset + last.length : undefined,
        refetchInterval: group.status === "open" ? 10_000 : false
    });
    const members = roster.data?.pages.flat() ?? [];

    return <section id="participants" className="panel roster" aria-labelledby="participants-heading">
        <header className="group-section-heading">
            <UsersRound size={20} aria-hidden="true" />
            <h2 id="participants-heading">Участники</h2>
            <HelpTip label="Кто входит в список">Здесь все участники с действующей покупкой или заявкой, включая организатора. «До» означает личный максимум за единицу товара.</HelpTip>
            <span className="roster-count">{group.participantsCount}</span>
        </header>
        {roster.isPending ? <Loading /> : roster.isError ? <LoadError error={roster.error} retry={roster.refetch} /> :
            members.length === 0 ? <p className="field-hint">Действующих заявок пока нет.</p> :
                <ul className="member-list">{members.map(member => (
                    <li key={member.userId}>
                        <Link to={(group.organizerId === member.userId ? "/organizers/" : "/users/") + member.userId + "?group=" + encodeURIComponent(group.id)}>
                            <span className="member-avatar" aria-hidden="true">{Array.from(member.nickname)[0]}</span>
                            <span className="member-name">{member.nickname}
                                {group.organizerId === member.userId && <small>Организатор</small>}
                            </span>
                            {member.unitPriceKopecks != null && <span className="member-price">
                                {member.isLimit && "до "}{money(member.unitPriceKopecks)}<small> / {unitLabel(group.saleUnit)}</small>
                            </span>}
                        </Link>
                    </li>
                ))}</ul>}
        {roster.hasNextPage && members.length < group.participantsCount &&
            <button className="text-link" disabled={roster.isFetchingNextPage} onClick={() => roster.fetchNextPage()}>Показать ещё</button>}
    </section>;
}
