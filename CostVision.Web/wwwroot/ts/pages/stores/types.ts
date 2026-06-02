export interface StoreListItem {
    id: string;
    name: string;
    address: string;
    adaptiveName: string | null;
    displayName: string;
    receiptCount: number;
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
    useAdaptiveNames: boolean;
    editedAdaptiveNames: Map<string, string>;
    expandedStoreGroups: Set<string>;
    sortBy: StoreSortBy;
    sortDirection: StoreSortDirection;
}

export type StoreSortBy = "name" | "receiptCount";
export type StoreSortDirection = "asc" | "desc";
