import type { ReceiptDto } from "../../../shared/receipts/types.js";
import type { PagedList } from "../../../shared/paging.js";
export type { ReceiptDto, ReceiptItemDto, ReceiptAccountDto, AvailableAccountDto } from "../../../shared/receipts/types.js";

export interface ReceiptList extends PagedList<ReceiptDto> {
    totalSum: number;
    incomeSum: number;
}

export interface ReceiptMoneyMovementDto {
    moneyMovementId: string;
    occurredAt: string;
    amount: number;
    type: number;
    comment: string;
    importComment: string;
    accountName: string;
    isLinkedToOtherReceipt: boolean;
}

export interface GetReceiptMoneyMovementCandidatesRequest {
    page?: number;
    pageSize?: number;
    receiptId: string;
    useTimeWindow: boolean;
    timeWindowHours: number | null;
    dateFrom: string | null;
    dateTo: string | null;
    useAmountFilter: boolean;
    amountTolerance: number | null;
    excludeLinkedMoneyMovements: boolean;
}

export interface LinkReceiptMoneyMovementRequest {
    moneyMovementId: string;
    receiptId: string;
}


export interface MoveReceiptAccountAction {
    receiptId: string;
    sourceAccountId: string;
}

export type DeleteReceiptActionType = "delete-receipt" | "remove-from-account";

export type PendingDeleteAction = {
    type: "delete-receipt";
    receiptId: string;
    cardElement: HTMLElement | null;
} | {
    type: "remove-from-account";
    receiptId: string;
    cardElement: HTMLElement | null;
    accountId: string;
};

export interface ReceiptDateRange {
    dateFrom: Date;
    dateTo: Date;
}

export interface MoveReceiptTargetAccount {
    id: string;
    name: string;
    isDisabled: boolean;
    disabledReason: string;
}
