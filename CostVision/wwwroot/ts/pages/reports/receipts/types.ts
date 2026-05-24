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
    moneyMovementCount: number;
    moneyMovementsTotalSum: number;
    accounts: ReceiptAccountDto[];
    items: ReceiptItemDto[];
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
