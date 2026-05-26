import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { requireElementById, requireInputById } from "../../shared/dom.js";
import { getProducts, updateProductAdaptiveName } from "./api.js";
import { renderProducts, updateSaveButtonVisibility, type ProductsUi } from "./render.js";
import { productsState } from "./state.js";
import type { ProductListItem } from "./types.js";

let searchInput: HTMLInputElement;
let useAdaptiveNamesInput: HTMLInputElement;
let pageSizeInputs: HTMLSelectElement[];
let alertElement: HTMLElement;
let ui: ProductsUi;

document.addEventListener("DOMContentLoaded", () => {
    initProductsPage();
});

function initProductsPage(): void {
    searchInput = requireInputById("productsSearch");
    useAdaptiveNamesInput = requireInputById("productsUseAdaptiveNames");
    pageSizeInputs = [
        requireElementById<HTMLSelectElement>("productsTopPageSize")
    ];
    alertElement = requireElementById<HTMLElement>("productsAlert");
    ui = {
        tableBody: requireElementById<HTMLTableSectionElement>("productsTableBody"),
        pageInfoElements: Array.from(document.querySelectorAll<HTMLElement>("[data-products-page-info]")),
        paginationElements: Array.from(document.querySelectorAll<HTMLElement>("[data-products-pagination]")),
        adaptiveNameHeader: requireElementById<HTMLElement>("productsAdaptiveNameHeader"),
        actionsHeader: requireElementById<HTMLElement>("productsActionsHeader")
    };

    requireElementById<HTMLButtonElement>("productsApplyFilter").addEventListener("click", () => reloadFromFirstPage());
    searchInput.addEventListener("keydown", handleSearchKeyDown);
    useAdaptiveNamesInput.addEventListener("change", () => reloadFromFirstPage());
    for (const pageSizeInput of pageSizeInputs) {
        pageSizeInput.addEventListener("change", () => reloadFromFirstPage(pageSizeInput));
    }
    ui.tableBody.addEventListener("input", handleTableInput);
    ui.tableBody.addEventListener("click", handleTableClick);

    void loadPage(1);
}

function handleSearchKeyDown(event: KeyboardEvent): void {
    if (event.key === "Enter") {
        event.preventDefault();
        reloadFromFirstPage();
    }
}

function reloadFromFirstPage(changedPageSizeInput: HTMLSelectElement | null = null): void {
    productsState.search = searchInput.value.trim();
    productsState.useAdaptiveNames = useAdaptiveNamesInput.checked;
    productsState.pageSize = Number((changedPageSizeInput ?? pageSizeInputs[0]).value);
    syncPageSizeInputs(productsState.pageSize);
    productsState.editedAdaptiveNames.clear();
    void loadPage(1);
}

async function loadPage(page: number): Promise<void> {
    if (page <= 0) {
        return;
    }

    try {
        hideAlertMessage(alertElement);

        const result = await getProducts(productsState.search, productsState.useAdaptiveNames, page, productsState.pageSize);
        productsState.products = result.items;
        productsState.page = result.page;
        productsState.pageSize = result.pageSize;
        productsState.totalCount = result.totalCount;
        productsState.totalPages = result.totalPages;
        productsState.hasPreviousPage = result.hasPreviousPage;
        productsState.hasNextPage = result.hasNextPage;
        syncPageSizeInputs(productsState.pageSize);

        renderProducts(ui, productsState, pageNumber => {
            void loadPage(pageNumber);
        });
    }
    catch (error) {
        showAlertMessage(alertElement, getErrorMessage(error));
    }
}

function syncPageSizeInputs(pageSize: number): void {
    for (const pageSizeInput of pageSizeInputs) {
        pageSizeInput.value = String(pageSize);
    }
}

function handleTableInput(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement) || target.dataset.productAdaptiveNameInput !== "true") {
        return;
    }

    const row = target.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.productId) {
        throw new Error("Не найдена строка товара.");
    }

    const product = getProductById(row.dataset.productId);
    productsState.editedAdaptiveNames.set(product.id, target.value);
    updateSaveButtonVisibility(row, product);
}

async function handleTableClick(event: MouseEvent): Promise<void> {
    const target = event.target;
    if (!(target instanceof HTMLButtonElement) || target.dataset.productSaveButton !== "true") {
        return;
    }

    const row = target.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.productId) {
        throw new Error("Не найдена строка товара.");
    }

    const input = row.querySelector<HTMLInputElement>("[data-product-adaptive-name-input]");
    if (!input) {
        throw new Error("Не найдено поле адаптивного названия.");
    }

    try {
        target.disabled = true;
        hideAlertMessage(alertElement);
        await updateProductAdaptiveName(row.dataset.productId, input.value);
        productsState.editedAdaptiveNames.delete(row.dataset.productId);
        await loadPage(productsState.page);
    }
    catch (error) {
        target.disabled = false;
        showAlertMessage(alertElement, getErrorMessage(error));
    }
}

function getProductById(productId: string): ProductListItem {
    const product = productsState.products.find(item => item.id === productId);
    if (!product) {
        throw new Error("Товар не найден в состоянии страницы.");
    }

    return product;
}

function getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : "Не удалось выполнить действие.";
}
