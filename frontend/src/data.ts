import { QueryClient, queryOptions, useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { api, ApiError } from "./api";
import { groupPath } from "./format";
import type { Group, GroupStatus, OrganizerProfile, Product, ProductCardData, Session } from "./types";

export const queries = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 10_000,
            retry: (count, error) => count < 1 && !(error instanceof ApiError && error.status < 500)
        },
        mutations: { retry: false }
    }
});

export function useCatalog(search: string) {
    return useQuery({
        queryKey: ["catalog", search],
        queryFn: ({ signal }) =>
            api<ProductCardData[]>("/catalog/products?search=" + encodeURIComponent(search), { signal })
    });
}

export function useProduct(id: string) {
    return useQuery({
        queryKey: ["product", id],
        queryFn: ({ signal }) => api<Product>("/catalog/products/" + encodeURIComponent(id), { signal }),
        refetchInterval: 30_000
    });
}

export function organizerProfileQuery(id: string, session: Session | null) {
    return queryOptions({
        queryKey: ["organizer", id, session?.user.id ?? null],
        queryFn: ({ signal }) => api<OrganizerProfile>("/users/" + encodeURIComponent(id) + "/organizer-profile", {
            signal, token: session?.accessToken
        }),
        refetchInterval: 30_000
    });
}

export function groupQuery(id: string) {
    return queryOptions({
        queryKey: ["group", id],
        queryFn: ({ signal }) => api<Group>(groupPath(id), { signal })
    });
}

export function useGroup(id: string) {
    return useQuery({
        ...groupQuery(id),
        refetchInterval: query => (query.state.data?.status === "open" ? 10_000 : false)
    });
}

export interface GroupFilters {
    organizerId?: string;
    search?: string;
    maxPriceKopecks?: number;
    endsWithinHours?: string;
    byLikes?: boolean;
}

export function useGroups(status: GroupStatus | "all", filters: GroupFilters = {}) {
    const limit = 12;
    return useInfiniteQuery({
        queryKey: ["groups", status, filters],
        initialPageParam: 0,
        queryFn: ({ pageParam, signal }) => {
            const params = new URLSearchParams({ offset: String(pageParam), limit: String(limit) });
            if (status !== "all") params.set("status", status);
            if (filters.organizerId) params.set("organizerId", filters.organizerId);
            if (filters.search) params.set("search", filters.search);
            if (filters.maxPriceKopecks) params.set("maxPriceKopecks", String(filters.maxPriceKopecks));
            if (filters.endsWithinHours) params.set("endsWithinHours", filters.endsWithinHours);
            params.set("sortByOrganizerLikes", String(filters.byLikes ?? true));
            return api<Group[]>("/groups?" + params, { signal });
        },
        getNextPageParam: (page, _pages, offset) => (page.length === limit ? offset + limit : undefined),
        refetchInterval: 30_000
    });
}

export function useBalance(session: Session) {
    return useQuery({
        queryKey: ["balance", session.user.id],
        queryFn: ({ signal }) => api<{ balanceKopecks: number }>("/me/balance", { token: session.accessToken, signal }),
        refetchOnWindowFocus: "always",
        refetchInterval: 15000
    });
}

export function useMyGroups(session: Session | null) {
    return useQuery({
        queryKey: ["mine", session?.user.id],
        queryFn: ({ signal }) => api<Group[]>("/me/groups", { signal, token: session!.accessToken }),
        enabled: !!session,
        refetchInterval: 15_000
    });
}

export function refreshGroup(group: Group, identifier = group.id) {
    queries.setQueryData(["group", identifier], group);
    for (const key of [["group"], ["participants", group.id], ["product", group.productId], ["catalog"], ["groups"], ["mine"], ["balance"], ["notifications"]]) {
        void queries.invalidateQueries({ queryKey: key });
    }
}
