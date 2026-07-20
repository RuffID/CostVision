import { clearElement } from "../../shared/dom.js";
import { formatRuNumber } from "../../shared/formatters.js";
import type { ProductListItem, ProductListState, ProductSortDirection, ProductStorePurchase, ProductStorePurchaseSortBy } from "./types.js";

const MAX_PRODUCT_NAME_LENGTH = 50;

export interface ProductsUi {
    tableBody: HTMLTableSectionElement;
    pageInfoElements: HTMLElement[];
    paginationElements: HTMLElement[];
    nameSortButton: HTMLButtonElement;
    receiptCountSortButton: HTMLButtonElement;
    averagePriceSortButton: HTMLButtonElement;
    storePurchasesQuantitySortButton: HTMLButtonElement;
    storePurchasesPriceSortButton: HTMLButtonElement;
}

export function renderProducts(ui: ProductsUi, state: ProductListState, onPageClick: (page: number) => void): void {
    clearElement(ui.tableBody);
    updateSortButtons(ui, state);

    if (state.products.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = 3;
        cell.className = "text-muted text-center py-4";
        cell.textContent = "Товары не найдены.";
        row.append(cell);
        ui.tableBody.append(row);
    }
    else {
        for (const product of state.products) {
            ui.tableBody.append(createProductRow(product, state.showOriginalNames));
        }
    }

    const shouldShowPagination = state.page > 1 || state.totalCount >= state.pageSize;
    document.getElementById("productsPageSizeContainer")?.classList.toggle("d-none", !shouldShowPagination);
    for (const pageInfo of ui.pageInfoElements) {
        pageInfo.textContent = `Всего товаров: ${state.totalCount}`;
    }

    for (const pagination of ui.paginationElements) {
        renderProductsPagination(pagination, state, onPageClick);
    }
}

function updateSortButtons(ui: ProductsUi, state: ProductListState): void {
    ui.nameSortButton.textContent = buildSortButtonText("Наименование", state.sortBy === "name" ? state.sortDirection : null);
    ui.receiptCountSortButton.textContent = buildSortButtonText("Количество", state.sortBy === "receiptCount" ? state.sortDirection : null);
    ui.averagePriceSortButton.textContent = buildSortButtonText("Средняя цена", state.sortBy === "averagePrice" ? state.sortDirection : null);
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

function createProductRow(product: ProductListItem, showOriginalNames: boolean): HTMLTableRowElement {
    const row = document.createElement("tr");
    row.dataset.productId = product.id;
    row.style.height = "43px";

    const nameCell = document.createElement("td");
    nameCell.className = "text-start";
    const nameText = document.createElement("button");
    nameText.type = "button";
    nameText.className = product.adaptiveName
        ? "btn btn-link d-block w-100 p-0 text-start text-break text-decoration-none text-success"
        : "btn btn-link d-block w-100 p-0 text-start text-break text-decoration-none text-body";
    const displayName = showOriginalNames ? product.name : product.adaptiveName ?? product.name;
    nameText.textContent = truncateProductName(displayName);
    nameText.title = displayName;
    nameText.dataset.productAdaptiveNameButton = "true";
    nameText.addEventListener("mouseenter", () => updateProductNameHover(nameText, Boolean(product.adaptiveName), true));
    nameText.addEventListener("mouseleave", () => updateProductNameHover(nameText, Boolean(product.adaptiveName), false));
    nameCell.append(nameText);
    row.append(nameCell);

    const receiptCountCell = document.createElement("td");
    receiptCountCell.className = "text-center text-nowrap";
    const receiptCountButton = document.createElement("button");
    receiptCountButton.type = "button";
    receiptCountButton.className = "btn btn-sm border border-transparent px-3 py-1 text-body text-decoration-none";
    receiptCountButton.textContent = String(product.receiptCount);
    receiptCountButton.dataset.productStorePurchasesButton = "true";
    receiptCountButton.addEventListener("mouseenter", () => setReceiptCountButtonHover(receiptCountButton, true));
    receiptCountButton.addEventListener("mouseleave", () => setReceiptCountButtonHover(receiptCountButton, false));
    receiptCountButton.addEventListener("mousedown", event => event.preventDefault());
    receiptCountCell.append(receiptCountButton);
    row.append(receiptCountCell);

    const averagePriceCell = document.createElement("td");
    averagePriceCell.className = "text-end text-nowrap";
    averagePriceCell.textContent = product.averagePrice === null || product.averagePriceIsWeighted === null
        ? "—"
        : `${formatAveragePrice(product.averagePrice)}/${product.averagePriceIsWeighted ? "кг" : "шт."}`;
    row.append(averagePriceCell);

    return row;
}

function truncateProductName(name: string): string {
    return name.length > MAX_PRODUCT_NAME_LENGTH
        ? `${name.substring(0, MAX_PRODUCT_NAME_LENGTH - 1)}…`
        : name;
}

function setReceiptCountButtonHover(button: HTMLButtonElement, isHovered: boolean): void {
    button.classList.toggle("border-transparent", !isHovered);
    button.classList.toggle("border-dark", isHovered);
}

export function renderProductStorePurchases(
    container: HTMLTableSectionElement,
    purchases: ProductStorePurchase[],
    sortBy: ProductStorePurchaseSortBy,
    sortDirection: ProductSortDirection,
    quantitySortButton: HTMLButtonElement,
    priceSortButton: HTMLButtonElement): void {
    clearElement(container);
    quantitySortButton.textContent = buildSortButtonText("Количество", sortBy === "quantity" ? sortDirection : null);
    priceSortButton.textContent = buildSortButtonText("Средняя цена", sortBy === "pricePerUnit" ? sortDirection : null);

    if (purchases.length === 0) {
        const row = document.createElement("tr");
        const cell = document.createElement("td");
        cell.colSpan = 3;
        cell.className = "text-muted text-center py-4";
        cell.textContent = "Покупки в магазинах не найдены.";
        row.append(cell);
        container.append(row);
        return;
    }

    for (const purchase of sortStorePurchases(purchases, sortBy, sortDirection)) {
        const row = document.createElement("tr");
        const storeNameCell = document.createElement("td");
        storeNameCell.textContent = purchase.storeName;
        const quantityCell = document.createElement("td");
        quantityCell.className = "text-center text-nowrap";
        quantityCell.textContent = `${formatRuNumber(purchase.quantity)} ${purchase.isWeighted ? "кг" : "шт."}`;
        const priceCell = document.createElement("td");
        priceCell.className = "text-end text-nowrap";
        priceCell.textContent = `${formatAveragePrice(purchase.pricePerUnit)}/${purchase.isWeighted ? "кг" : "шт."}`;
        row.append(storeNameCell, quantityCell, priceCell);
        container.append(row);
    }
}

function sortStorePurchases(
    purchases: ProductStorePurchase[],
    sortBy: ProductStorePurchaseSortBy,
    sortDirection: ProductSortDirection): ProductStorePurchase[] {
    const direction = sortDirection === "asc" ? 1 : -1;

    return [...purchases].sort((left, right) => (left[sortBy] - right[sortBy]) * direction);
}

function formatAveragePrice(value: number): string {
    return `${value.toLocaleString("ru-RU", { maximumFractionDigits: 2 })} ₽`;
}

function updateProductNameHover(button: HTMLButtonElement, hasAdaptiveName: boolean, isHovered: boolean): void {
    if (hasAdaptiveName) {
        button.classList.toggle("text-success", true);
        return;
    }

    button.classList.toggle("text-body", !isHovered);
    button.classList.toggle("text-primary", isHovered);
}

function renderProductsPagination(container: HTMLElement, state: ProductListState, onPageClick: (page: number) => void): void {
    clearElement(container);

    if (state.page === 1 && state.totalCount < state.pageSize) return;

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

