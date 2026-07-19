import type { StoreListState } from "./types.js";

export const storesState: StoreListState = {
    stores: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
    search: "",
    showOriginalNames: false,
    groupByName: false,
    sortBy: "name",
    sortDirection: "asc"
};
