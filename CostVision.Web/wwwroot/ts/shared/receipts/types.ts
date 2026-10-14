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
    operationType: number;
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

export interface AvailableAccountDto {
    id: string;
    name: string;
    colorHex: string;
    accessRole: number;
    canManage: boolean;
}
