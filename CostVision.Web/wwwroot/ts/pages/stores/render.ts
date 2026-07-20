import { clearElement } from "../../shared/dom.js";
import { formatRuNumber } from "../../shared/formatters.js";
import type { StoreListItem, StoreListState } from "./types.js";

export interface StoresUi {
    tableBody: HTMLTableSectionElement;
    pageInfoElements: HTMLElement[];
    paginationElements: HTMLElement[];
    nameSortButton: HTMLButtonElement;
    receiptCountSortButton: HTMLButtonElement;
    totalSpentSortButton: HTMLButtonElement;
}

export function renderStores(ui: StoresUi, state: StoreListState, onPageClick: (page: number) => void): void {
    clearElement(ui.tableBody);
    updateSortButtons(ui, state);

    if (state.stores.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = 4;
        cell.className = "border-start border-end border-bottom text-muted text-center py-4";
        cell.textContent = "Магазины не найдены.";
        row.append(cell);
        ui.tableBody.append(row);
    }
    else {
        for (const store of state.stores) {
            ui.tableBody.append(createStoreRow(store, state));
        }
    }

    const shouldShowPagination = state.page > 1 || state.totalCount >= state.pageSize;
    document.getElementById("storesPageSizeContainer")?.classList.toggle("d-none", !shouldShowPagination);
    for (const pageInfo of ui.pageInfoElements) {
        pageInfo.textContent = `Всего магазинов: ${state.totalCount}`;
    }

    for (const pagination of ui.paginationElements) {
        renderStoresPagination(pagination, state, onPageClick);
    }
}

function updateSortButtons(ui: StoresUi, state: StoreListState): void {
    ui.nameSortButton.textContent = buildSortButtonText("Наименование", state.sortBy === "name" ? state.sortDirection : null);
    updateReceiptCountSortButton(ui.receiptCountSortButton, state.sortBy === "receiptCount" ? state.sortDirection : null);
    ui.totalSpentSortButton.textContent = buildSortButtonText("Потрачено", state.sortBy === "totalSpent" ? state.sortDirection : null);
}

function buildSortButtonText(text: string, direction: string | null): string {
    if (direction === "asc") {
        return `${text} ↑`;
    }

    if (direction === "desc") {
        return `${text} ↓`;
    }

    return text;
}

function formatStoreTableText(text: string | null, maxLength: number): string {
    if (!text) {
        return "-";
    }

    return text.length > maxLength ? `${text.substring(0, maxLength)}...` : text;
}

export function createStoreRow(store: StoreListItem, state: StoreListState): HTMLTableRowElement {
    const row = document.createElement("tr");
    row.dataset.storeId = store.id;
    row.style.height = "43px";

    const isGroupRow = store.children.length > 0;
    if (isGroupRow) {
        row.dataset.storeGroupKey = store.groupKey;
    }

    const nameCell = document.createElement("td");
    nameCell.className = "border-start border-bottom";
    const nameText = isGroupRow ? document.createElement("div") : document.createElement("button");
    if (nameText instanceof HTMLButtonElement) {
        nameText.type = "button";
        nameText.className = store.adaptiveName ? "btn btn-link p-0 text-decoration-none text-success" : "btn btn-link p-0 text-decoration-none text-body";
        nameText.classList.add("text-start");
        nameText.textContent = formatStoreTableText(state.showOriginalNames ? store.name : store.adaptiveName ?? store.name, 50);
        nameText.dataset.storeAdaptiveNameButton = "true";
        nameText.addEventListener("mouseenter", () => updateStoreNameHover(nameText, Boolean(store.adaptiveName), true));
        nameText.addEventListener("mouseleave", () => updateStoreNameHover(nameText, Boolean(store.adaptiveName), false));
    }
    else {
        nameText.textContent = formatStoreTableText(store.name, 50);
    }
    nameCell.append(nameText);
    row.append(nameCell);

    const addressCell = document.createElement("td");
    addressCell.className = isGroupRow ? "border-start border-bottom fw-bold" : "border-start border-bottom";
    addressCell.textContent = formatStoreTableText(store.address, 100);
    row.append(addressCell);

    const receiptCountCell = document.createElement("td");
    receiptCountCell.className = "border-start border-bottom text-center text-nowrap";
    const receiptCountButton = document.createElement("button");
    receiptCountButton.type = "button";
    receiptCountButton.className = "btn btn-sm border border-transparent px-3 py-1 text-body text-decoration-none";
    receiptCountButton.textContent = String(store.receiptCount);
    receiptCountButton.dataset.storeReceiptsButton = "true";
    receiptCountButton.dataset.storeId = store.id;
    if (isGroupRow) {
        receiptCountButton.dataset.storeGroupKey = store.groupKey;
    }
    receiptCountButton.addEventListener("mouseenter", () => setReceiptCountButtonHover(receiptCountButton, true));
    receiptCountButton.addEventListener("mouseleave", () => setReceiptCountButtonHover(receiptCountButton, false));
    receiptCountButton.addEventListener("mousedown", event => event.preventDefault());
    receiptCountCell.append(receiptCountButton);
    row.append(receiptCountCell);

    const totalSpentCell = document.createElement("td");
    totalSpentCell.className = "border-start border-end border-bottom text-end text-nowrap";
    totalSpentCell.textContent = `${formatRuNumber(Math.floor(store.totalSpent))} ₽`;
    row.append(totalSpentCell);

    return row;
}

function updateStoreNameHover(button: HTMLButtonElement, hasAdaptiveName: boolean, isHovered: boolean): void {
    if (hasAdaptiveName) {
        button.classList.toggle("text-success", true);
        return;
    }

    button.classList.toggle("text-body", !isHovered);
    button.classList.toggle("text-primary", isHovered);
}

function setReceiptCountButtonHover(button: HTMLButtonElement, isHovered: boolean): void {
    button.classList.toggle("border-transparent", !isHovered);
    button.classList.toggle("border-dark", isHovered);
}

function updateReceiptCountSortButton(button: HTMLButtonElement, direction: string | null): void {
    const directionText = direction === "asc" ? " ↑" : direction === "desc" ? " ↓" : "";
    button.replaceChildren(document.createTextNode("Количество"), document.createElement("br"), document.createTextNode(`чеков${directionText}`));
}

function renderStoresPagination(container: HTMLElement, state: StoreListState, onPageClick: (page: number) => void): void {
    clearElement(container);

    if (state.page === 1 && state.totalCount < state.pageSize) return;

    const currentPage = state.page || 1;
    const totalPages = Math.max(1, state.totalPages || 1);

    if (currentPage > 1) {
        container.append(createPageButton("<<", () => onPageClick(1), false));
        container.append(createPageButton("<", () => onPageClick(currentPage - 1), false));
    }

    const range = getStoresPageRange(currentPage, totalPages);
    for (const page of range) {
        container.append(createPageButton(String(page), () => onPageClick(page), page === currentPage));
    }

    if (range.length > 0 && range[range.length - 1] < totalPages) {
        const dots = document.createElement("span");
        dots.className = "px-2";
        dots.textContent = "...";
        container.append(dots);
    }

    if (state.hasNextPage) {
        container.append(createPageButton(">", () => onPageClick(currentPage + 1), false));
        container.append(createPageButton(">>", () => onPageClick(totalPages), false));
    }
}

function createPageButton(text: string, onClick: () => void, isActive: boolean): HTMLButtonElement {
    const button = document.createElement("button");
    button.type = "button";
    button.className = isActive ? "btn btn-primary" : "btn btn-outline-secondary";
    button.textContent = text;
    button.addEventListener("click", onClick);
    return button;
}

function getStoresPageRange(currentPage: number, visibleLastPage: number): number[] {
    let start = 1;

    if (currentPage >= 5) {
        start = currentPage - 2;
    }

    if (start + 4 > visibleLastPage) {
        start = Math.max(1, visibleLastPage - 4);
    }

    const end = Math.min(visibleLastPage, start + 4);
    const pages: number[] = [];

    for (let page = start; page <= end; page += 1) {
        pages.push(page);
    }

    return pages;
}

