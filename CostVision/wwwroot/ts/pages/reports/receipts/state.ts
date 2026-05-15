import { ReceiptDto } from "./types.js";

export function removeReceiptFromCache(receipts: ReceiptDto[], receiptId: string): ReceiptDto[] {
    return receipts.filter(function (receipt) {
        return receipt.id !== receiptId;
    });
}
