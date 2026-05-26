import { clearElement } from "../../shared/dom.js";
import type { ProductListItem, ProductListState } from "./types.js";

export interface ProductsUi {
    tableBody: HTMLTableSectionElement;
    pageInfoElements: HTMLElement[];
    paginationElements: HTMLElement[];
    adaptiveNameHeader: HTMLElement;
    actionsHeader: HTMLElement;
}

export function renderProducts(ui: ProductsUi, state: ProductListState, onPageClick: (page: number) => void): void {
    clearElement(ui.tableBody);
    ui.adaptiveNameHeader.classList.toggle("d-none", !state.useAdaptiveNames);
    ui.actionsHeader.classList.toggle("d-none", !state.useAdaptiveNames);

    if (state.products.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = state.useAdaptiveNames ? 3 : 1;
        cell.className = "text-muted text-center py-4";
        cell.textContent = "Товары не найдены.";
        row.append(cell);
        ui.tableBody.append(row);
    }
    else {
        for (const product of state.products) {
            ui.tableBody.append(createProductRow(product, state));
        }
    }

    for (const pageInfo of ui.pageInfoElements) {
        pageInfo.textContent = `Всего товаров: ${state.totalCount}`;
    }

    for (const pagination of ui.paginationElements) {
        renderProductsPagination(pagination, state, onPageClick);
    }
}

function createProductRow(product: ProductListItem, state: ProductListState): HTMLTableRowElement {
    const row = document.createElement("tr");
    row.dataset.productId = product.id;
    row.style.height = "43px";

    const nameCell = document.createElement("td");
    const nameText = document.createElement("div");
    nameText.textContent = product.name;
    nameCell.append(nameText);
    row.append(nameCell);

    if (!state.useAdaptiveNames) {
        return row;
    }

    const adaptiveNameCell = document.createElement("td");
    adaptiveNameCell.className = "p-0";
    adaptiveNameCell.style.minWidth = "22rem";
    const adaptiveNameInput = document.createElement("input");
    adaptiveNameInput.type = "text";
    adaptiveNameInput.id = `productAdaptiveName_${product.id}`;
    adaptiveNameInput.name = `productAdaptiveName_${product.id}`;
    adaptiveNameInput.className = "form-control-plaintext border-0 rounded-0 shadow-none bg-transparent w-100 h-100 px-2 py-0";
    adaptiveNameInput.style.height = "100%";
    adaptiveNameInput.style.minHeight = "40px";
    adaptiveNameInput.style.lineHeight = "1.5";
    adaptiveNameInput.style.outline = "none";
    adaptiveNameInput.maxLength = 500;
    adaptiveNameInput.value = state.editedAdaptiveNames.get(product.id) ?? product.adaptiveName ?? "";
    adaptiveNameInput.dataset.productAdaptiveNameInput = "true";
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
    saveButton.dataset.productSaveButton = "true";
    actionsCell.append(saveButton);

    row.append(adaptiveNameCell, actionsCell);
    updateSaveButtonVisibility(row, product);

    return row;
}

function renderProductsPagination(container: HTMLElement, state: ProductListState, onPageClick: (page: number) => void): void {
    clearElement(container);

    const currentPage = state.page || 1;
    const totalPages = Math.max(1, state.totalPages || 1);

    if (currentPage > 1) {
        container.append(createPageButton("<<", () => onPageClick(1), false));
        container.append(createPageButton("<", () => onPageClick(currentPage - 1), false));
    }

    const range = getProductsPageRange(currentPage, totalPages);
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

function getProductsPageRange(currentPage: number, visibleLastPage: number): number[] {
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

export function updateSaveButtonVisibility(row: HTMLTableRowElement, product: ProductListItem): void {
    const input = row.querySelector<HTMLInputElement>("[data-product-adaptive-name-input]");
    const saveButton = row.querySelector<HTMLButtonElement>("[data-product-save-button]");

    if (!input || !saveButton) {
        throw new Error("Не найдены элементы строки товара.");
    }

    const initialValue = product.adaptiveName ?? "";
    saveButton.classList.toggle("invisible", input.value === initialValue);
}
