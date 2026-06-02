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
