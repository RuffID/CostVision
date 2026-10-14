import { clearElement } from "../../../shared/dom.js";
import { createPageButton, getPageRange } from "../../../shared/paging.js";
import { buildReceiptCard } from "./ui/receiptCards.js";
import type { ReceiptDto, ReceiptList } from "./types.js";

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

export function renderReceiptCards(container: HTMLElement, receipts: ReceiptDto[], formatCurrency: (value: number) => string): void {
    clearElement(container);
    for (const receipt of receipts) {
        container.appendChild(buildReceiptCard(receipt, formatCurrency));
    }
}

export function createReceiptLoadingIndicator(): HTMLElement {
    const indicator = document.createElement("div");
    indicator.className = "text-center text-muted py-5";
    indicator.textContent = "Идёт загрузка чеков...";
    return indicator;
}

export function renderReceiptPagination(container: HTMLElement, result: ReceiptList, onPage: (page: number) => void): void {
    clearElement(container);
    if (result.page === 1 && result.totalCount < result.pageSize) { return; }
    if (result.hasPreviousPage) {
        container.append(createPageButton("<<", () => onPage(1)), createPageButton("<", () => onPage(result.page - 1)));
    }
    for (const page of getPageRange(result.page, result.totalPages, "centered")) {
        container.append(createPageButton(String(page), () => onPage(page), page === result.page));
    }
    if (result.hasNextPage) {
        container.append(createPageButton(">", () => onPage(result.page + 1)), createPageButton(">>", () => onPage(result.totalPages)));
    }
}
