export interface ProductListItem {
    id: string;
    name: string;
    adaptiveName: string | null;
    displayName: string;
    receiptCount: number;
    averagePrice: number | null;
    averagePriceIsWeighted: boolean | null;
}

export interface ProductList {
    items: ProductListItem[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

export interface ProductStorePurchase {
    storeName: string;
    quantity: number;
    pricePerUnit: number;
    isWeighted: boolean;
}

export type ProductStorePurchaseSortBy = "quantity" | "pricePerUnit";

export interface ProductListState {
    products: ProductListItem[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    search: string;
    showOriginalNames: boolean;
    sortBy: ProductSortBy;
    sortDirection: ProductSortDirection;
}

export type ProductSortBy = "name" | "receiptCount" | "averagePrice";
export type ProductSortDirection = "asc" | "desc";
