import { ReceiptDto } from "../types.js";

export interface DeleteReceiptModalElements {
    titleElement: HTMLElement;
    messageElement: HTMLElement;
    receiptInfoElement: HTMLElement;
}

export function fillDeleteReceiptModal(elements: DeleteReceiptModalElements, title: string, message: string, receipt: ReceiptDto, formatCurrency: (value: number) => string): void {
    elements.titleElement.textContent = title;
    elements.messageElement.textContent = message;
    elements.receiptInfoElement.textContent = buildDeleteReceiptInfoText(receipt, formatCurrency);
}

function buildDeleteReceiptInfoText(receipt: ReceiptDto, formatCurrency: (value: number) => string): string {
    const parts: string[] = [];
    if (receipt.dateTime) {
        const date = new Date(receipt.dateTime);
        parts.push(formatDeleteReceiptDateTime(date));
    }

    if (typeof receipt.totalSum === "number") {
        parts.push(formatCurrency(receipt.totalSum));
    }

    return parts.join(", ");
}

function formatDeleteReceiptDateTime(date: Date): string {
    return date.toLocaleDateString("ru-RU") + " " + date.toLocaleTimeString("ru-RU", {
        hour: "2-digit",
        minute: "2-digit"
    });
}
