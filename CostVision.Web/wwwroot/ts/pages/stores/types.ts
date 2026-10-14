import type { ReceiptDto } from "../reports/receipts/types.js";

export interface StoreListItem {
    id: string;
    name: string;
    address: string;
    adaptiveName: string | null;
    displayName: string;
    receiptCount: number;
    totalSpent: number;
    incomeSum: number;
    groupKey: string;
    children: StoreListItem[];
}

export interface StoreList extends PagedList<StoreListItem> {
    totalSum: number;
    incomeSum: number;
}

export interface StoreReceiptList extends PagedList<ReceiptDto> {
    totalSum: number;
    incomeSum: number;
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
import type { PagedList } from "../../shared/paging.js";

