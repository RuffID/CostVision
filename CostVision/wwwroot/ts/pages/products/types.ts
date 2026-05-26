export interface ProductListItem {
    id: string;
    name: string;
    adaptiveName: string | null;
    displayName: string;
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

export interface ProductListState {
    products: ProductListItem[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    search: string;
    useAdaptiveNames: boolean;
    editedAdaptiveNames: Map<string, string>;
}
