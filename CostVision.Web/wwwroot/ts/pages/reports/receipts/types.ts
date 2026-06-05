export interface ReceiptItemDto {
    name: string;
    quantity: number;
    price: number;
    sum: number;
}

export interface ReceiptAccountDto {
    id: string;
    receiptId: string;
    name: string;
    colorHex: string;
    canEditReceipt: boolean;
    accessRole?: number | null;
}

export interface ReceiptDto {
    id: string;
    dateTime: string;
    retailPlace: string;
    retailPlaceAddress: string;
    totalSum: number;
    fiscalDriveNumber: string;
    fiscalDocumentNumber: string;
    fiscalSign: string;
    accountId: string | null;
    accountName: string;
    createdByUserName: string;
    createdAtUtc: string;
    moneyMovementCount: number;
    availableMoneyMovementCount: number;
    moneyMovementsTotalSum: number;
    accounts: ReceiptAccountDto[];
    items: ReceiptItemDto[];
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

export interface AvailableAccountDto {
    id: string;
    name: string;
    colorHex: string;
    accessRole: number;
    canManage: boolean;
}

export interface MoveReceiptAccountAction {
    receiptId: string;
    sourceAccountId: string;
}

export type DeleteReceiptActionType = "delete-receipt" | "remove-from-account";

export interface PendingDeleteAction {
    type: DeleteReceiptActionType;
    receiptId: string;
    cardElement: HTMLElement | null;
    accountId?: string;
}

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
