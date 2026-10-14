import { clearElement } from "../../shared/dom.js";
import { createPageButton, getPageRange } from "../../shared/paging.js";
import { buildReceiptCard } from "../reports/receipts/ui/receiptCards.js";
import type { ReceiptDto } from "../../shared/receipts/types.js";
import type { StoreReceiptList } from "./types.js";

export function renderStoreReceipts(container: HTMLElement, receipts: ReceiptDto[], formatCurrency: (value: number) => string): void {
    clearElement(container);
    if (receipts.length === 0) {
        const empty = document.createElement("div");
        empty.className = "text-muted py-2";
        empty.textContent = "Чеки магазина не найдены.";
        container.append(empty);
        return;
    }
    for (const receipt of receipts) {
        const card = buildReceiptCard(receipt, formatCurrency);
        card.querySelector('[data-action="delete"]')?.remove();
        container.append(card);
    }
}

export function renderStoreReceiptsPagination(container: HTMLElement, pageSizeContainer: HTMLElement, result: StoreReceiptList, onPage: (page: number) => void): void {
    clearElement(container);
    pageSizeContainer.classList.toggle("d-none", result.page === 1 && result.totalCount < result.pageSize);
    if (result.totalPages <= 1) { return; }
    if (result.hasPreviousPage) {
        container.append(createPageButton("<<", () => onPage(1)), createPageButton("<", () => onPage(result.page - 1)));
    }
    for (const page of getPageRange(result.page, result.totalPages)) {
        container.append(createPageButton(String(page), () => onPage(page), page === result.page));
    }
    if (result.hasNextPage) {
        container.append(createPageButton(">", () => onPage(result.page + 1)), createPageButton(">>", () => onPage(result.totalPages)));
    }
}
