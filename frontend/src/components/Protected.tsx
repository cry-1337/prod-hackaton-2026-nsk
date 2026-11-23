import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useSession } from "../session";
import type { Session } from "../types";

export function Protected({ children }: { children: (session: Session) => ReactNode }) {
    const session = useSession();
    const { pathname, search } = useLocation();
    return session ? (
        children(session)
    ) : (
        <Navigate to={"/login?next=" + encodeURIComponent(pathname + search)} replace />
    );
}
