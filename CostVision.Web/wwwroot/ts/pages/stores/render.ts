import { clearElement } from "../../shared/dom.js";
import type { StoreListItem, StoreListState } from "./types.js";

export interface StoresUi {
    tableBody: HTMLTableSectionElement;
    pageInfoElements: HTMLElement[];
    paginationElements: HTMLElement[];
    adaptiveNameHeader: HTMLElement;
    actionsHeader: HTMLElement;
    nameSortButton: HTMLButtonElement;
    receiptCountSortButton: HTMLButtonElement;
}

export function renderStores(ui: StoresUi, state: StoreListState, onPageClick: (page: number) => void): void {
    clearElement(ui.tableBody);
    ui.adaptiveNameHeader.classList.toggle("d-none", !state.useAdaptiveNames);
    ui.actionsHeader.classList.toggle("d-none", !state.useAdaptiveNames);
    updateSortButtons(ui, state);

    if (state.stores.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = state.useAdaptiveNames ? 6 : 4;
        cell.className = "text-muted text-center py-4";
        cell.textContent = "Магазины не найдены.";
        row.append(cell);
        ui.tableBody.append(row);
    }
    else {
        for (const store of state.stores) {
            ui.tableBody.append(createStoreRow(store, state, false));

            if (isStoreGroupExpanded(store, state)) {
                for (const child of store.children) {
                    ui.tableBody.append(createStoreRow(child, state, true));
                }
            }
        }
    }

    for (const pageInfo of ui.pageInfoElements) {
        pageInfo.textContent = `Всего магазинов: ${state.totalCount}`;
    }

    for (const pagination of ui.paginationElements) {
        renderStoresPagination(pagination, state, onPageClick);
    }
}

function updateSortButtons(ui: StoresUi, state: StoreListState): void {
    ui.nameSortButton.textContent = buildSortButtonText("Наименование", state.sortBy === "name" ? state.sortDirection : null);
    ui.receiptCountSortButton.textContent = buildSortButtonText("Количество", state.sortBy === "receiptCount" ? state.sortDirection : null);
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

function createStoreRow(store: StoreListItem, state: StoreListState, isChildRow: boolean): HTMLTableRowElement {
    const row = document.createElement("tr");
    row.dataset.storeId = store.id;
    row.style.height = "43px";

    const isGroupRow = !isChildRow && store.children.length > 0;
    if (isGroupRow) {
        row.dataset.storeGroupKey = store.groupKey;
    }

    const toggleCell = document.createElement("td");
    toggleCell.className = "border-0 bg-transparent p-0 text-center align-middle";
    toggleCell.style.width = "2.5rem";

    if (isGroupRow) {
        const expandButton = document.createElement("button");
        expandButton.type = "button";
        expandButton.className = "btn btn-sm btn-outline-secondary";
        expandButton.style.width = "2rem";
        expandButton.style.height = "2rem";
        expandButton.style.lineHeight = "1";
        expandButton.textContent = state.expandedStoreGroups.has(store.groupKey) ? "⌃" : "⌄";
        expandButton.ariaLabel = state.expandedStoreGroups.has(store.groupKey) ? "Свернуть группу магазинов" : "Развернуть группу магазинов";
        expandButton.dataset.storeGroupToggle = "true";
        toggleCell.append(expandButton);
    }

    row.append(toggleCell);

    const nameCell = document.createElement("td");
    nameCell.className = "border-start-0";
    const nameText = document.createElement("div");
    if (isChildRow) {
        nameText.className = "ps-3";
    }
    nameText.textContent = store.name || "-";
    nameCell.append(nameText);
    row.append(nameCell);

    const addressCell = document.createElement("td");
    addressCell.textContent = store.address || "-";
    row.append(addressCell);

    const receiptCountCell = document.createElement("td");
    receiptCountCell.className = "text-end text-nowrap";
    receiptCountCell.textContent = String(store.receiptCount);
    row.append(receiptCountCell);

    if (!state.useAdaptiveNames) {
        return row;
    }

    if (isGroupRow) {
        const adaptiveNameCell = document.createElement("td");
        adaptiveNameCell.className = "text-muted";
        adaptiveNameCell.textContent = "-";

        const actionsCell = document.createElement("td");
        actionsCell.className = "text-center text-nowrap";
        actionsCell.textContent = "-";

        row.append(adaptiveNameCell, actionsCell);
        return row;
    }

    const adaptiveNameCell = document.createElement("td");
    adaptiveNameCell.className = "p-0";
    adaptiveNameCell.style.minWidth = "22rem";
    const adaptiveNameInput = document.createElement("input");
    adaptiveNameInput.type = "text";
    adaptiveNameInput.id = `storeAdaptiveName_${store.id}`;
    adaptiveNameInput.name = `storeAdaptiveName_${store.id}`;
    adaptiveNameInput.className = "form-control-plaintext border-0 rounded-0 shadow-none bg-transparent w-100 h-100 px-2 py-0";
    adaptiveNameInput.style.height = "100%";
    adaptiveNameInput.style.minHeight = "40px";
    adaptiveNameInput.style.lineHeight = "1.5";
    adaptiveNameInput.style.outline = "none";
    adaptiveNameInput.maxLength = 500;
    adaptiveNameInput.value = state.editedAdaptiveNames.get(store.id) ?? store.adaptiveName ?? "";
    adaptiveNameInput.dataset.storeAdaptiveNameInput = "true";
    adaptiveNameCell.append(adaptiveNameInput);

    const actionsCell = document.createElement("td");
    actionsCell.className = "text-center text-nowrap align-middle p-0";
    actionsCell.style.width = "9rem";
    const saveButton = document.createElement("button");
    saveButton.type = "button";
    saveButton.className = "btn btn-sm btn-primary px-2 py-0 invisible";
    saveButton.style.width = "calc(100% - 4px)";
    saveButton.style.height = "calc(100% - 4px)";
    saveButton.style.minHeight = "39px";
    saveButton.style.lineHeight = "1";
    saveButton.style.margin = "2px";
    saveButton.textContent = "Сохранить";
    saveButton.dataset.storeSaveButton = "true";
    actionsCell.append(saveButton);

    row.append(adaptiveNameCell, actionsCell);
    updateSaveButtonVisibility(row, store);

    return row;
}

function isStoreGroupExpanded(store: StoreListItem, state: StoreListState): boolean {
    return store.children.length > 0 && state.expandedStoreGroups.has(store.groupKey);
}

function renderStoresPagination(container: HTMLElement, state: StoreListState, onPageClick: (page: number) => void): void {
    clearElement(container);

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

export function updateSaveButtonVisibility(row: HTMLTableRowElement, store: StoreListItem): void {
    const input = row.querySelector<HTMLInputElement>("[data-store-adaptive-name-input]");
    const saveButton = row.querySelector<HTMLButtonElement>("[data-store-save-button]");

    if (!input || !saveButton) {
        throw new Error("Не найдены элементы строки магазина.");
    }

    const initialValue = store.adaptiveName ?? "";
    saveButton.classList.toggle("invisible", input.value === initialValue);
}
