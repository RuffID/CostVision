import type { ProductListState } from "./types.js";

export const productsState: ProductListState = {
    products: [],
    page: 1,
    pageSize: 50,
    totalCount: 0,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    search: "",
    useAdaptiveNames: false,
    editedAdaptiveNames: new Map<string, string>()
};
