import { Link, Navigate, Route, Routes, useParams } from "react-router-dom";
import { useSession } from "./session";
import { Layout } from "./components/Layout";
import { Protected } from "./components/Protected";
import { AccountPage } from "./pages/Account";
import { AuthPage } from "./pages/Auth";
import { CatalogPage, ChoosePage } from "./pages/Catalog";
import { CreateGroupPage } from "./pages/CreateGroup";
import { GroupPage } from "./pages/Group";
import { GroupsPage } from "./pages/Groups";
import { OrganizerPage } from "./components/Organizer";
import { ProductPage } from "./pages/Product";

function ProductRoute({ create = false }: { create?: boolean }) {
    const { id = "" } = useParams();
    return create ? (
        <Protected>
            {session => <CreateGroupPage key={id + session.user.id} id={id} session={session} />}
        </Protected>
    ) : (
        <ProductPage key={id} id={id} />
    );
}

function ProfileRoute({ organizer = false }: { organizer?: boolean }) {
    const { id = "" } = useParams();
    const session = useSession();
    if (session?.user.id.toLowerCase() === id.toLowerCase()) {
        return <Navigate to={organizer ? "/me?role=organizer" : "/me"} replace />;
    }

    return <OrganizerPage key={id} id={id} organizer={organizer} />;
}

function GroupRoute() {
    const { code = "" } = useParams();
    return <GroupPage key={code} id={code} />;
}

export default function App() {
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route index element={<CatalogPage />} />
                <Route path="groups" element={<GroupsPage />} />
                <Route path="groups/:code" element={<GroupRoute />} />
                <Route path="invite/:code" element={<GroupRoute />} />
                <Route path="products/:id" element={<ProductRoute />} />
                <Route path="products/:id/create" element={<ProductRoute create />} />
                <Route path="organizers/:id" element={<ProfileRoute organizer />} />
                <Route path="users/:id" element={<ProfileRoute />} />
                <Route path="new" element={<ChoosePage />} />
                <Route path="login" element={<AuthPage key="login" register={false} />} />
                <Route path="register" element={<AuthPage key="register" register />} />
                <Route
                    path="me"
                    element={
                        <Protected>
                            {session => <AccountPage key={session.user.id} session={session} />}
                        </Protected>
                    }
                />
                <Route
                    path="*"
                    element={
                        <section className="empty">
                            <h1>Страница не найдена</h1>
                            <Link to="/" className="text-link">
                                На витрину
                            </Link>
                        </section>
                    }
                />
            </Route>
        </Routes>
    );
}
