import { ReceiptDto } from "./receiptsTypes.js";

export function updateReceiptsSummary(countElement: HTMLElement, sumElement: HTMLElement, receipts: ReceiptDto[], formatCurrency: (value: number) => string): void {
    countElement.textContent = "Чеков: " + receipts.length;

    const totalSum = receipts.reduce(function (sum, receipt) {
        return sum + (typeof receipt.totalSum === "number" ? receipt.totalSum : 0);
    }, 0);

    sumElement.textContent = "Сумма: " + formatCurrency(totalSum);
}
