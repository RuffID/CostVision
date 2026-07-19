import type { ProductListState } from "./types.js";

export const productsState: ProductListState = {
    products: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    search: "",
    showOriginalNames: false,
    sortBy: "receiptCount",
    sortDirection: "desc"
};
