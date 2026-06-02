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
    useAdaptiveNames: false,
    editedAdaptiveNames: new Map<string, string>(),
    expandedStoreGroups: new Set<string>(),
    sortBy: "name",
    sortDirection: "asc"
};
