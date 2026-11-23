export interface User {
    id: string;
    nickname: string;
    email: string;
}

export interface Session {
    user: User;
    accessToken: string;
    expiresAt: string;
}

export interface PriceTier {
    requiredQuantity: number;
    waitingBuyers?: number;
    eligibleQuantity?: number;
    priceKopecks: number;
}

export interface DeliveryAddress {
    city: string;
    street: string;
    house: string;
    building?: string | null;
    apartment?: string | null;
}

export type SaleUnit = "piece" | "kilogram";

export interface ProductCardData {
    id: string;
    name: string;
    imageUrl: string | null;
    regularPriceKopecks: number;
    saleUnit: SaleUnit;
    startingTier: PriceTier | null;
    openGroupsCount: number;
}

export interface ProductBonus {
    quantity: number;
    unit: string;
}

export interface GroupLimit {
    address?: DeliveryAddress | null;
    deliveryAddress?: string;
    quantity?: number;
    limitKopecks: number;
    chargedKopecks: number;
    refundedKopecks: number;
    remainingQuantity?: number | null;
    status: "pending" | "purchased" | "revoked" | "closed";
}

export interface LimitQuote {
    chargeNowKopecks: number;
    otherLimitsPurchased: number;
    reachablePriceKopecks?: number | null;
    remainingQuantity?: number | null;
    refundNowKopecks: number;
    canAfford: boolean;
}

export interface OrganizerProfile {
    id: string;
    nickname: string;
    openGroupsCount: number;
    successfulGroupsCount: number;
    likesCount: number;
    likedByMe: boolean;
}
export type GroupStatus = "open" | "successful" | "cancelled";
export type Role = "organizer" | "participant";

export interface Group {
    id: string;
    productId: string;
    productName: string;
    imageUrl: string | null;
    regularPriceKopecks: number;
    saleUnit: SaleUnit;
    organizerId: string;
    organizerNickname: string;
    deadline: string;
    createdAt: string;
    status: GroupStatus;
    organizerLikesCount: number;
    progressTier: PriceTier | null;
    eligibleQuantity: number;
    settlementPriceKopecks?: number;
    pendingLimitsCount?: number;
    directPaymentsCount: number;
    purchasedLimitsCount?: number;
    myLimit?: GroupLimit;
    myPayment?: { quantity: number; address?: DeliveryAddress | null; deliveryAddress?: string | null; paidKopecks: number; refundedKopecks: number };
    myDirectPaidKopecks?: number;
    myBonus?: ProductBonus;
    participantsCount: number;
    priceTiers: PriceTier[];
    inviteCode: string;
    myRole?: Role | null;
}

export interface Product {
    id: string;
    name: string;
    description: string | null;
    imageUrl: string | null;
    regularPriceKopecks: number;
    saleUnit: SaleUnit;
    priceTiers: PriceTier[];
    organizerBonus: ProductBonus;
    openGroups: Group[];
}

export interface CreateGroupRequest {
    quantity: number;
    productId: string;
    deadline: string;
    organizerLimitKopecks: number;
    address: DeliveryAddress;
}

export interface OrderNotification {
    id: string;
    kind: "purchase" | "refund";
    groupId: string;
    productName: string;
    amountKopecks: number;
    createdAt: string;
    readAt: string | null;
}

export interface GroupParticipant {
    unitPriceKopecks: number | null;
    isLimit: boolean;
    userId: string;
    nickname: string;
}

export interface ChatMessage {
    id: number;
    authorId: string;
    authorNickname: string;
    text: string;
    createdAt: string;
}
