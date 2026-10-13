import type { ReceiptList } from "./types.js";

export function updateReceiptsSummary(countElement: HTMLElement, sumElement: HTMLElement,
    summary: Pick<ReceiptList, "totalCount" | "totalSum" | "incomeSum"> | null,
    formatCurrency: (value: number) => string): void {
    if (!summary) {
        countElement.textContent = "Чеков: —";
        sumElement.textContent = "Итоги ещё не загружены";
        return;
    }

    countElement.textContent = "Чеков: " + summary.totalCount;
    sumElement.textContent = formatReportTotals(summary.totalSum, summary.incomeSum, formatCurrency);
}

export function formatReportTotals(expense: number, income: number, formatCurrency: (value: number) => string): string {
    return "Расход: " + formatCurrency(expense) + " · Доход: " + formatCurrency(income);
}
