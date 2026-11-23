import { totalPrice } from "./format";
import { demoSession, practiceDataKey } from "./practice";
import type { ChatMessage, CreateGroupRequest, Group, Product } from "./types";

const products: Product[] = [
    { id: "guide-pears", name: "Груши Конференция", saleUnit: "kilogram", regularPriceKopecks: 26900,
        imageUrl: "/art/pears.webp", description: null, organizerBonus: { quantity: 1, unit: "кг" }, openGroups: [],
        priceTiers: [{ requiredQuantity: 3, priceKopecks: 21900 }, { requiredQuantity: 5, priceKopecks: 18900 }] },
    { id: "guide-honey", name: "Мёд цветочный", saleUnit: "piece", regularPriceKopecks: 59000,
        imageUrl: "/art/honey.webp", description: "Банка мёда", organizerBonus: { quantity: 1, unit: "шт." }, openGroups: [],
        priceTiers: [{ requiredQuantity: 3, priceKopecks: 49000 }, { requiredQuantity: 5, priceKopecks: 45000 }] }
];

interface PracticeState {
    balance: number;
    groups: Group[];
    chats?: Record<string, ChatMessage[]>;
}

function readState(): PracticeState {
    const saved = sessionStorage.getItem(practiceDataKey);
    return saved ? JSON.parse(saved) : { balance: 500_000, groups: [] };
}

function save(state: PracticeState) {
    sessionStorage.setItem(practiceDataKey, JSON.stringify(state));
}

function createGroup(product: Product, order: CreateGroupRequest, state: PracticeState): Group {
    const tiers = product.priceTiers.map(tier => ({
        ...tier, eligibleQuantity: tier.priceKopecks <= order.organizerLimitKopecks ? order.quantity : 0
    }));
    const selected = tiers.find(tier => tier.priceKopecks === order.organizerLimitKopecks);
    const advance = totalPrice(order.organizerLimitKopecks, order.quantity);
    if (!selected || advance > state.balance) throw new Error("Недостаточно учебных средств");
    const achieved = tiers.filter(tier => tier.eligibleQuantity >= tier.requiredQuantity).at(-1);
    const charged = totalPrice(achieved?.priceKopecks ?? order.organizerLimitKopecks, order.quantity);
    const id = crypto.randomUUID().replaceAll("-", "");
    const group: Group = {
        id, productId: product.id, productName: product.name, imageUrl: product.imageUrl, saleUnit: product.saleUnit,
        regularPriceKopecks: product.regularPriceKopecks, organizerId: demoSession.user.id,
        organizerNickname: demoSession.user.nickname, organizerLikesCount: 0, participantsCount: 1,
        createdAt: new Date().toISOString(), deadline: order.deadline, inviteCode: id,
        status: achieved ? "successful" : "open", priceTiers: tiers, progressTier: achieved ?? selected,
        eligibleQuantity: order.quantity, settlementPriceKopecks: achieved?.priceKopecks, directPaymentsCount: 0,
        myRole: "organizer", myLimit: { limitKopecks: selected.priceKopecks, quantity: order.quantity,
            chargedKopecks: charged, refundedKopecks: advance - charged, address: order.address,
            remainingQuantity: Math.max(0, selected.requiredQuantity - order.quantity), status: achieved ? "purchased" : "pending" },
        myPayment: achieved ? { quantity: order.quantity, paidKopecks: advance, refundedKopecks: advance - charged } : undefined
    };
    state.balance -= charged;
    state.groups.unshift(group);
    save(state);
    return group;
}

export function practiceRequest(path: string, method = "GET", body?: unknown): unknown {
    const { pathname, searchParams } = new URL(path, location.origin);
    const parts = pathname.split("/").filter(Boolean);
    const state = readState();
    const user = demoSession.user;
    if (method === "POST" && pathname === "/groups") {
        const order = body as CreateGroupRequest;
        const product = products.find(item => item.id === order.productId);
        if (product) return createGroup(product, order, state);
    }
    const group = parts[0] === "groups" && state.groups.find(g => g.id === parts[1]);
    if (group && parts[2] === "chat") {
        const history = state.chats?.[group.id] ?? [];
        if (method === "GET") return history;
        if (method === "POST") {
            const { text } = body as { text: string };
            const message = { id: history.length + 1, text, authorId: user.id, authorNickname: user.nickname,
                createdAt: new Date().toISOString() };
            state.chats = { ...state.chats, [group.id]: [...history, message] };
            save(state);
            return message;
        }
    }
    if (method !== "GET") throw new Error("Это действие доступно после выхода из практики.");
    const term = (searchParams.get("search") ?? "").toLocaleLowerCase();
    if (pathname === "/catalog/products") return products.filter(p => p.name.toLowerCase().includes(term)).map(p => ({
        ...p, startingTier: p.priceTiers[0], openGroupsCount: state.groups.filter(g => g.productId === p.id && g.status === "open").length
    }));
    if (pathname.startsWith("/catalog/products/")) {
        const product = products.find(p => p.id === parts[2]);
        if (product) return { ...product, openGroups: state.groups.filter(g => g.productId === product.id && g.status === "open") };
    }
    if (pathname === "/auth/me") return user;
    if (pathname === "/me/balance") return { balanceKopecks: state.balance };
    if (pathname === "/me/notifications") return [];
    if (pathname === "/me/groups") return state.groups;
    if (pathname === "/users/" + user.id + "/organizer-profile") return {
        ...user, likedByMe: false, likesCount: 0, openGroupsCount: state.groups.filter(g => g.status === "open").length,
        successfulGroupsCount: state.groups.filter(g => g.status === "successful").length
    };
    if (pathname === "/groups") {
        const status = searchParams.get("status");
        const max = Number(searchParams.get("maxPriceKopecks") || Infinity);
        const end = Number(searchParams.get("endsWithinHours") || Infinity);
        const offset = Number(searchParams.get("offset"));
        return state.groups.filter(g => (!status || g.status === status) &&
            (g.settlementPriceKopecks ?? g.progressTier!.priceKopecks) <= max &&
            Date.parse(g.deadline) - Date.now() <= end * 3600000 &&
            [g.productName, g.organizerNickname, g.inviteCode].some(v => v.toLowerCase().includes(term)))
            .slice(offset, offset + Number(searchParams.get("limit") || 12));
    }
    if (group && parts.length === 2) return group;
    if (group && parts[2] === "participants") return [{ userId: user.id, nickname: user.nickname,
        isLimit: group.status === "open", unitPriceKopecks: group.settlementPriceKopecks ?? group.myLimit?.limitKopecks }];
    throw new Error("В этой практике таких данных нет.");
}
