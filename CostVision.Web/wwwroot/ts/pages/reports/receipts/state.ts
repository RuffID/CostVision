import { createRequestCoordinator } from "../../../shared/requestCoordinator.js";
import type { MoveReceiptAccountAction, PendingDeleteAction, ReceiptList } from "./types.js";
import { AvailableAccountDto, ReceiptDto } from "./types.js";

export type ReceiptsPageState = {
    receipts: ReceiptDto[];
    availableAccounts: AvailableAccountDto[];
};

export function createReceiptsPageState(): ReceiptsPageState {
    return {
        receipts: [],
        availableAccounts: []
    };
}

export function removeReceiptFromCache(receipts: ReceiptDto[], receiptId: string): ReceiptDto[] {
    return receipts.filter(function (receipt) {
        return receipt.id !== receiptId;
    });
}

export function createReceiptsSession() {
    return {
        forgeryToken: null as string | null,
        currentReceiptList: null as ReceiptList | null,
        pendingDeleteReceiptId: null as string | null,
        pendingDeleteCardElement: null as HTMLElement | null,
        pendingDeleteAction: null as PendingDeleteAction | null,
        preserveMoveReceiptAccountModalStateOnHide: false,
        shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation: false,
        pageState: createReceiptsPageState(),
        listRequests: createRequestCoordinator(),
        accountRequests: createRequestCoordinator(),
        detailRequests: createRequestCoordinator(),
        receiptWrites: new Set<string>(),
        confirmationContext: createRequestCoordinator(),
        searchDebounceTimerId: null as number | null,
        pendingReceiptAccountAction: null as MoveReceiptAccountAction | null,
        DELETE_ACTION_DELETE_RECEIPT: 'delete-receipt' as const,
        DELETE_ACTION_REMOVE_FROM_ACCOUNT: 'remove-from-account' as const,
    };
}

export type ReceiptsSession = ReturnType<typeof createReceiptsSession>;
