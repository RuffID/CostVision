import type { ReceiptDto } from "../reports/receipts/types.js";

export interface StoreListItem {
    id: string;
    name: string;
    address: string;
    adaptiveName: string | null;
    displayName: string;
    receiptCount: number;
    totalSpent: number;
    groupKey: string;
    children: StoreListItem[];
}

export interface StoreList {
    items: StoreListItem[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    totalSum: number;
}

export interface StoreReceiptList {
    items: ReceiptDto[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

export interface StoreListState {
    stores: StoreListItem[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    search: string;
    showOriginalNames: boolean;
    groupByName: boolean;
    sortBy: StoreSortBy;
    sortDirection: StoreSortDirection;
}

export type StoreSortBy = "name" | "receiptCount" | "totalSpent";
export type StoreSortDirection = "asc" | "desc";
