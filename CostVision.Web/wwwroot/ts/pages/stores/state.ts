import { createRequestCoordinator } from "../../shared/requestCoordinator.js";
import type { AvailableAccountDto, MoveReceiptAccountAction, ReceiptDto } from "../reports/receipts/types.js";
import type { StoreListState } from "./types.js";

export function createStoresState(): StoreListState {
    return {
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
}

export function createStoresSession() {
    return {
        data: createStoresState(),
        editedStoreId: null as string | null,
        selectedStoreReceiptsStoreId: null as string | null,
        selectedStoreReceiptsGroupKey: null as string | null,
        selectedStoreReceiptsPage: 1,
        selectedStoreReceipts: [] as ReceiptDto[],
        availableAccounts: [] as AvailableAccountDto[],
        pendingReceiptAccountAction: null as MoveReceiptAccountAction | null,
        preserveMoveReceiptAccountModalStateOnHide: false,
        shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation: false,
        listRequests: createRequestCoordinator(),
        accountRequests: createRequestCoordinator(),
        namingContext: createRequestCoordinator(),
        namingWrites: new Set<string>(),
        storeReceiptRequests: createRequestCoordinator(),
        storeContext: createRequestCoordinator(),
        detailRequests: createRequestCoordinator(),
        accountActionContext: createRequestCoordinator(),
        receiptWrites: new Set<string>(),
        confirmationContext: createRequestCoordinator(),
        SORT_BY_COOKIE_NAME: "storesSortBy",
        SORT_DIRECTION_COOKIE_NAME: "storesSortDirection",
        PAGE_SIZE_COOKIE_NAME: "storesPageSize",
        SHOW_ORIGINAL_NAMES_COOKIE_NAME: "storesShowOriginalNames",
        GROUP_BY_NAME_COOKIE_NAME: "storesGroupByName",
    };
}

export type StoresSession = ReturnType<typeof createStoresSession>;
