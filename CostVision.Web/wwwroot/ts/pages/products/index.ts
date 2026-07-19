import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { createBootstrapModal, type BootstrapModal } from "../../shared/bootstrap.js";
import { requireElementById, requireInputById } from "../../shared/dom.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { createTableLoadingIndicator, type TableLoadingIndicator } from "../../shared/tableLoadingIndicator.js";
import { getProductStorePurchases, getProducts, updateProductAdaptiveName } from "./api.js";
import { renderProductStorePurchases, renderProducts, type ProductsUi } from "./render.js";
import { productsState } from "./state.js";
import type { ProductListItem, ProductSortBy } from "./types.js";

let searchInput: HTMLInputElement;
let showOriginalNamesInput: HTMLInputElement;
let pageSizeInputs: HTMLSelectElement[];
let alertElement: HTMLElement;
let ui: ProductsUi;
let tableLoadingIndicator: TableLoadingIndicator;
let adaptiveNameModal: BootstrapModal;
let adaptiveNameModalElement: HTMLElement;
let storePurchasesModal: BootstrapModal;
let storePurchasesModalElement: HTMLElement;
let storePurchasesBody: HTMLTableSectionElement;
let originalNameInput: HTMLInputElement;
let adaptiveNameInput: HTMLInputElement;
let clearAdaptiveNameButton: HTMLButtonElement;
let saveAdaptiveNameButton: HTMLButtonElement;
let editedProductId: string | null = null;
let latestLoadRequestId = 0;

const SORT_BY_COOKIE_NAME = "productsSortBy";
const SORT_DIRECTION_COOKIE_NAME = "productsSortDirection";
const PAGE_SIZE_COOKIE_NAME = "productsPageSize";

document.addEventListener("DOMContentLoaded", () => {
    initProductsPage();
});

function initProductsPage(): void {
    searchInput = requireInputById("productsSearch");
    showOriginalNamesInput = requireInputById("productsShowOriginalNames");
    pageSizeInputs = [
        requireElementById<HTMLSelectElement>("productsTopPageSize")
    ];
    alertElement = requireElementById<HTMLElement>("productsAlert");
    tableLoadingIndicator = createTableLoadingIndicator(
        requireElementById<HTMLElement>("productsTableContainer"),
        "Идёт загрузка продуктов...",
        480
    );
    ui = {
        tableBody: requireElementById<HTMLTableSectionElement>("productsTableBody"),
        pageInfoElements: Array.from(document.querySelectorAll<HTMLElement>("[data-products-page-info]")),
        paginationElements: Array.from(document.querySelectorAll<HTMLElement>("[data-products-pagination]")),
        nameSortButton: requireElementById<HTMLButtonElement>("productsNameSortButton"),
        receiptCountSortButton: requireElementById<HTMLButtonElement>("productsReceiptCountSortButton")
    };

    renderHelpTooltip(requireElementById<HTMLElement>("productsReceiptCountHelp"), {
        title: "Количество",
        text: "Сколько чеков текущего пользователя содержат этот товар."
    });
    renderHelpTooltip(requireElementById<HTMLElement>("productsNameHelp"), {
        title: "Наименование",
        text: "Нажмите на наименование товара, чтобы указать своё название. Товары с заданным названием выделены зелёным цветом."
    });
    renderHelpTooltip(requireElementById<HTMLElement>("productsShowOriginalNamesHelp"), {
        title: "Отображать оригинальные названия",
        text: "Показывает исходные названия товаров вместо указанных Вами названий."
    });
    renderHelpTooltip(requireElementById<HTMLElement>("productStorePurchasesPriceHelp"), {
        title: "Средняя цена",
        text: "Рассчитывается как общая стоимость всех покупок товара в магазине, делённая на общее количество. Для весового товара цена указана за килограмм."
    });

    adaptiveNameModalElement = requireElementById<HTMLElement>("productAdaptiveNameModal");
    adaptiveNameModal = createBootstrapModal(adaptiveNameModalElement);
    originalNameInput = requireInputById("productOriginalName");
    adaptiveNameInput = requireInputById("productAdaptiveName");
    clearAdaptiveNameButton = requireElementById<HTMLButtonElement>("productClearAdaptiveName");
    saveAdaptiveNameButton = requireElementById<HTMLButtonElement>("productSaveAdaptiveName");
    storePurchasesModalElement = requireElementById<HTMLElement>("productStorePurchasesModal");
    storePurchasesModal = createBootstrapModal(storePurchasesModalElement);
    storePurchasesBody = requireElementById<HTMLTableSectionElement>("productStorePurchasesBody");

    requireElementById<HTMLButtonElement>("productsApplyFilter").addEventListener("click", () => reloadFromFirstPage());
    searchInput.addEventListener("keydown", handleSearchKeyDown);
    showOriginalNamesInput.addEventListener("change", handleShowOriginalNamesChange);
    ui.nameSortButton.addEventListener("click", () => handleSortClick("name"));
    ui.receiptCountSortButton.addEventListener("click", () => handleSortClick("receiptCount"));
    for (const pageSizeInput of pageSizeInputs) {
        pageSizeInput.addEventListener("change", () => reloadFromFirstPage(pageSizeInput));
    }
    ui.tableBody.addEventListener("click", handleTableClick);
    requireElementById<HTMLFormElement>("productAdaptiveNameForm").addEventListener("submit", event => {
        event.preventDefault();
        void saveAdaptiveName();
    });
    clearAdaptiveNameButton.addEventListener("click", () => {
        adaptiveNameInput.value = "";
        adaptiveNameInput.focus();
    });

    restorePreferences();
    syncPageSizeInputs(productsState.pageSize);
    void loadPage(getPageFromQuery());
}

function handleSortClick(sortBy: ProductSortBy): void {
    if (productsState.sortBy === sortBy) {
        productsState.sortDirection = productsState.sortDirection === "asc" ? "desc" : "asc";
    } else {
        productsState.sortBy = sortBy;
        productsState.sortDirection = "asc";
    }

    savePreferences();
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
    productsState.pageSize = Number((changedPageSizeInput ?? pageSizeInputs[0]).value);
    syncPageSizeInputs(productsState.pageSize);
    savePreferences();
    void loadPage(1);
}

function handleShowOriginalNamesChange(): void {
    productsState.showOriginalNames = showOriginalNamesInput.checked;
    renderProducts(ui, productsState, pageNumber => {
        void loadPage(pageNumber);
    });
}

async function loadPage(page: number): Promise<void> {
    if (page <= 0) {
        return;
    }

    const requestId = ++latestLoadRequestId;
    updatePageQuery(page);
    tableLoadingIndicator.show();

    try {
        hideAlertMessage(alertElement);

        const result = await getProducts(productsState.search, page, productsState.pageSize, productsState.sortBy, productsState.sortDirection);
        if (requestId !== latestLoadRequestId) {
            return;
        }

        productsState.products = result.items;
        productsState.page = result.page;
        productsState.pageSize = result.pageSize;
        productsState.totalCount = result.totalCount;
        productsState.totalPages = result.totalPages;
        productsState.hasPreviousPage = result.hasPreviousPage;
        productsState.hasNextPage = result.hasNextPage;
        syncPageSizeInputs(productsState.pageSize);
        updatePageQuery(productsState.page);

        renderProducts(ui, productsState, pageNumber => {
            void loadPage(pageNumber);
        });

        tableLoadingIndicator.hide();
    }
    catch (error) {
        if (requestId !== latestLoadRequestId) {
            return;
        }

        tableLoadingIndicator.hide();
        showAlertMessage(alertElement, getErrorMessage(error));
    }
}

function restorePreferences(): void {
    const sortBy = getCookie(SORT_BY_COOKIE_NAME);
    const sortDirection = getCookie(SORT_DIRECTION_COOKIE_NAME);
    const pageSize = Number(getCookie(PAGE_SIZE_COOKIE_NAME));

    if (sortBy === "name" || sortBy === "receiptCount") {
        productsState.sortBy = sortBy;
    }

    if (sortDirection === "asc" || sortDirection === "desc") {
        productsState.sortDirection = sortDirection;
    }

    if (pageSize === 20 || pageSize === 50 || pageSize === 100) {
        productsState.pageSize = pageSize;
    }
}

function savePreferences(): void {
    setCookie(SORT_BY_COOKIE_NAME, productsState.sortBy);
    setCookie(SORT_DIRECTION_COOKIE_NAME, productsState.sortDirection);
    setCookie(PAGE_SIZE_COOKIE_NAME, String(productsState.pageSize));
}

function getPageFromQuery(): number {
    const page = Number(new URLSearchParams(window.location.search).get("page"));
    return Number.isInteger(page) && page > 0 ? page : 1;
}

function updatePageQuery(page: number): void {
    const url = new URL(window.location.href);
    url.searchParams.set("page", String(page));
    window.history.replaceState(null, "", url);
}

function getCookie(name: string): string | null {
    const prefix = `${name}=`;
    const cookie = document.cookie.split("; ").find(item => item.startsWith(prefix));
    return cookie ? decodeURIComponent(cookie.substring(prefix.length)) : null;
}

function setCookie(name: string, value: string): void {
    document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=31536000; samesite=lax`;
}

function syncPageSizeInputs(pageSize: number): void {
    for (const pageSizeInput of pageSizeInputs) {
        pageSizeInput.value = String(pageSize);
    }
}

function handleTableClick(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof HTMLButtonElement)) {
        return;
    }

    const row = target.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.productId) {
        throw new Error("Не найдена строка товара.");
    }

    const product = getProductById(row.dataset.productId);
    if (target.dataset.productAdaptiveNameButton === "true") {
        openAdaptiveNameModal(product);
        return;
    }

    if (target.dataset.productStorePurchasesButton === "true") {
        void openStorePurchasesModal(product);
    }
}

async function openStorePurchasesModal(product: ProductListItem): Promise<void> {
    requireElementById<HTMLElement>("productStorePurchasesModalTitle").textContent = `Магазины, в которых был куплен товар - ${product.adaptiveName ?? product.name}`;
    renderProductStorePurchases(storePurchasesBody, []);
    storePurchasesModal.show();

    try {
        hideAlertMessage(alertElement);
        renderProductStorePurchases(storePurchasesBody, await getProductStorePurchases(product.id));
    }
    catch (error) {
        storePurchasesModal.hide();
        showAlertMessage(alertElement, getErrorMessage(error));
    }
}

function openAdaptiveNameModal(product: ProductListItem): void {
    editedProductId = product.id;
    requireElementById<HTMLElement>("productAdaptiveNameModalTitle").textContent = `Укажите Ваше наименование для товара - ${product.name}`;
    originalNameInput.value = product.name;
    adaptiveNameInput.value = product.adaptiveName ?? "";
    clearAdaptiveNameButton.classList.toggle("d-none", !product.adaptiveName);
    adaptiveNameModal.show();
}

async function saveAdaptiveName(): Promise<void> {
    if (!editedProductId) {
        throw new Error("Не выбран товар для изменения наименования.");
    }

    try {
        saveAdaptiveNameButton.disabled = true;
        hideAlertMessage(alertElement);
        await updateProductAdaptiveName(editedProductId, adaptiveNameInput.value);
        adaptiveNameModal.hide();
        await loadPage(productsState.page);
    }
    catch (error) {
        showAlertMessage(alertElement, getErrorMessage(error));
    }
    finally {
        saveAdaptiveNameButton.disabled = false;
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
