import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link2, Check, Heart, Plus } from "lucide-react";
import { Link, useHref, useSearchParams } from "react-router-dom";
import { api } from "../api";
import { Wallet } from "../components/Wallet";
import { GroupCard } from "../components/Purchase";
import { Empty, LoadError, Loading } from "../components/States";
import { usePageTitle } from "../usePageTitle";
import { countLabel } from "../format";
import { organizerProfileQuery, useMyGroups } from "../data";
import type { Session, User } from "../types";

export function AccountPage({ session }: { session: Session }) {
    usePageTitle("Мои сборы");
    const [params, setParams] = useSearchParams();
    const role = params.get("role") === "organizer" ? "organizer" : "participant";
    const mine = useMyGroups(session);
    const reputation = useQuery(organizerProfileQuery(session.user.id, session));
    const profile = useQuery({
        queryKey: ["profile", session.user.id],
        queryFn: ({ signal }) => api<User>("/auth/me", { token: session.accessToken, signal })
    });
    const user = profile.data ?? session.user;
    const [profileLink, setProfileLink] = useState<"copied" | "manual" | null>(null);
    const publicProfile = new URL(useHref("/users/" + user.id), window.location.origin).href;
    const purchases = mine.data ?? [];
    const groups = role === "organizer" ? purchases.filter(group => group.myRole === "organizer") : purchases;

    return (
        <>
            <header className="account-cover">
                <img src="/art/crates.webp" alt="" width="1080" height="810" />
                <div className="relative min-w-0">
                    <h1>Мои сборы</h1>
                    <p className="mt-3 break-words">
                        <strong>{user.nickname}</strong> · {user.email}
                    </p>
                    {reputation.data ? (
                        <p className="profile-likes mt-2">
                            <Heart size={15} aria-hidden="true" />
                            {countLabel(reputation.data.likesCount, "лайк", "лайка", "лайков")}
                        </p>
                    ) : reputation.isError && (
                        <button className="text-link mt-2" onClick={() => void reputation.refetch()}>Загрузить лайки повторно</button>
                    )}
                    <div className="profile-actions">
                        <button className="button button-light" aria-label="Скопировать ссылку на профиль" onClick={async () => {
                            try { await navigator.clipboard.writeText(publicProfile); setProfileLink("copied"); }
                            catch { setProfileLink("manual"); }
                        }}>
                            {profileLink === "copied" ? <Check size={16} /> : <Link2 size={16} />}
                            <span aria-live="polite">{profileLink === "copied" ? "Скопировано" : "Ссылка на профиль"}</span>
                        </button>
                    </div>
                    {profileLink === "manual" && <input className="profile-url" aria-label="Ссылка на мой профиль" readOnly value={publicProfile}
                        onFocus={event => event.target.select()} />}
                </div>
            </header>
            <Wallet session={session} />
            <div className="section-block">
                <div className="section-heading account-controls">
                    <div className="tabs">
                        <button
                            aria-pressed={role === "participant"}
                            onClick={() => setParams({ role: "participant" })}
                        >
                            Участвую
                        </button>
                        <button
                            aria-pressed={role === "organizer"}
                            onClick={() => setParams({ role: "organizer" })}
                        >
                            Организую
                        </button>
                    </div>
                    <Link className="button button-dark" to="/new">
                        <Plus size={16} />
                        Создать сбор
                    </Link>
                </div>
                {mine.isPending ? (
                    <Loading />
                ) : mine.isError ? (
                    <LoadError error={mine.error} retry={mine.refetch} />
                ) : groups.length ? (
                    <div className={role === "organizer" ? "group-list" : "group-grid"}>
                        {groups.map(group => (
                            <GroupCard key={group.id} group={group} showOrganizer={role !== "organizer"} />
                        ))}
                    </div>
                ) : (
                    <Empty
                        title={
                            role === "organizer"
                                ? "Вы пока не открывали сборы"
                                : "Вы пока не участвуете в сборах"
                        }
                    >
                        <Link className="text-link" to={role === "organizer" ? "/new" : "/groups"}>
                            {role === "organizer" ? "Выбрать товар" : "Найти сбор"}
                        </Link>
                    </Empty>
                )}
            </div>
        </>
    );
}
