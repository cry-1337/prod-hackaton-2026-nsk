import { useMutation, useQuery } from "@tanstack/react-query";
import { Heart } from "lucide-react";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import { api, errorMessage } from "../api";
import { organizerProfileQuery, queries } from "../data";
import { countLabel, groupPath } from "../format";
import { useSession } from "../session";
import type { OrganizerProfile } from "../types";
import { usePageTitle } from "../usePageTitle";
import { GroupList } from "./GroupList";
import { Back, LoadError, Loading } from "./States";

export function OrganizerPage({ id, organizer }: { id: string; organizer: boolean }) {
    const session = useSession();
    const location = useLocation();
    const [params, setParams] = useSearchParams();
    const groupId = params.get("group");
    const back = groupId ? groupPath(groupId) : location.state?.groupPath;
    const path = "/users/" + encodeURIComponent(id) + "/organizer-profile";
    const options = organizerProfileQuery(id, session);
    const profile = useQuery(options);
    usePageTitle(profile.data?.nickname ?? "Организатор");
    const like = useMutation({
        mutationFn: (liked: boolean) => api<OrganizerProfile>(path + "/like", {
            method: liked ? "DELETE" : "PUT", token: session!.accessToken
        }),
        onSuccess: data => {
            queries.setQueryData(options.queryKey, data);
            for (const key of ["organizer", "groups", "group", "catalog", "product", "mine"]) {
                void queries.invalidateQueries({ queryKey: [key] });
            }
        }
    });

    const backLink = back ? (
        <Link className="back-link" to={back} state={location.state?.groupOrigin}>К сбору</Link>
    ) : <Back to="/groups">К списку сборов</Back>;

    const data = profile.data;
    if (!data) {
        return <>{backLink}{profile.isError ? <LoadError error={profile.error} retry={profile.refetch} /> : <Loading />}</>;
    }

    const categories = [
        { value: "open", label: "Открытые", count: data.openGroupsCount },
        { value: "successful", label: "Доставленные", count: data.successfulGroupsCount }
    ] as const;
    const selected = categories.find(item => item.value === params.get("status"))?.value
        ?? (data.openGroupsCount === 0 && data.successfulGroupsCount > 0 ? "successful" : "open");

    function showStatus(status: typeof selected) {
        const next = new URLSearchParams(params);
        next.set("status", status);
        setParams(next, { replace: true, preventScrollReset: true, state: location.state });
    }

    return (
        <>
            {backLink}
            <header className="organizer-profile">
                <div className="profile-identity">
                    <span className="profile-initial" aria-hidden="true">{data.nickname.slice(0, 1)}</span>
                    <div className="min-w-0">
                        <p className="profile-role">{organizer ? "Организатор" : "Профиль покупателя"}</p>
                        <h1>{data.nickname}</h1>
                        <p className="profile-likes">
                            <Heart size={15} aria-hidden="true" />
                            {countLabel(data.likesCount, "лайк", "лайка", "лайков")}
                        </p>
                    </div>
                </div>
                <div className="profile-action">
                    {session ? (
                        <button
                            className="button button-light"
                            aria-pressed={data.likedByMe}
                            disabled={like.isPending}
                            onClick={() => like.mutate(data.likedByMe)}
                        >
                            <Heart size={16} fill={data.likedByMe ? "currentColor" : "none"} />
                            {data.likedByMe ? "Снять лайк" : "Нравится"}
                        </button>
                    ) : (
                        <Link className="button button-light" to={"/login?next=" + encodeURIComponent(location.pathname + location.search)}>
                            Войти и поставить лайк
                        </Link>
                    )}
                    {like.isError && <p role="alert" className="form-error">{errorMessage(like.error)}</p>}
                </div>
            </header>
            <section className="section-block" aria-labelledby="organizer-groups">
                <div className="section-heading profile-groups-heading">
                    <h2 id="organizer-groups">Организованные сборы</h2>
                    <div className="tabs" role="group" aria-label="Статус сборов организатора">
                        {categories.map(item => (
                            <button
                                key={item.value}
                                aria-pressed={selected === item.value}
                                onClick={() => showStatus(item.value)}
                            >
                                {item.label} <span className="tab-count">{item.count}</span>
                            </button>
                        ))}
                    </div>
                </div>
                <GroupList
                    status={selected}
                    filters={{ organizerId: data.id, byLikes: selected === "open" }}
                    emptyTitle={selected === "open" ? "Открытых сборов пока нет" : "Успешных сборов пока нет"}
                />
            </section>
        </>
    );
}
