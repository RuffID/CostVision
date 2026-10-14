import { getPageFromQuery, updatePageQuery, getCookie, setCookie } from "../../shared/preferences.js";
import { createRequestCoordinator, getWriteErrorMessage, isIntentionalAbort } from "../../shared/requestCoordinator.js";
import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { createBootstrapModal, type BootstrapModal } from "../../shared/bootstrap.js";
import { requireElementById, requireInputById } from "../../shared/dom.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { createTableLoadingIndicator, type TableLoadingIndicator } from "../../shared/tableLoadingIndicator.js";
import { getProductAccounts, getProductStorePurchases, getProducts, updateProductAdaptiveName } from "./api.js";
import { renderProductStorePurchases, renderProducts, type ProductsUi } from "./render.js";
import { productsState } from "./state.js";
import type { ProductListItem, ProductSortBy, ProductSortDirection, ProductStorePurchase, ProductStorePurchaseSortBy } from "./types.js";

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
let storePurchases: ProductStorePurchase[] = [];
let storePurchasesSortBy: ProductStorePurchaseSortBy = "quantity";
let storePurchasesSortDirection: ProductSortDirection = "desc";
let storePurchasesPage = 1;
let storePurchasesPageSizeSelect: HTMLSelectElement;
let storePurchasesPagination: HTMLElement;
let originalNameInput: HTMLInputElement;
let adaptiveNameInput: HTMLInputElement;
let clearAdaptiveNameButton: HTMLButtonElement;
let saveAdaptiveNameButton: HTMLButtonElement;
let editedProductId: string | null = null;
const listRequests = createRequestCoordinator();
const accountRequests = createRequestCoordinator();
const namingContext = createRequestCoordinator();
const namingWrites = new Set<string>();
const purchaseRequests = createRequestCoordinator();
window.addEventListener("pagehide", () => { listRequests.invalidate(); accountRequests.invalidate(); namingContext.invalidate(); purchaseRequests.invalidate(); });
let accountFilterSelect: HTMLSelectElement;
let productsCountElement: HTMLElement;
let productsSumElement: HTMLElement;

const SORT_BY_COOKIE_NAME = "productsSortBy";
const SORT_DIRECTION_COOKIE_NAME = "productsSortDirection";
const PAGE_SIZE_COOKIE_NAME = "productsPageSize";
const STORE_PURCHASES_SORT_BY_COOKIE_NAME = "productStorePurchasesSortBy";
const STORE_PURCHASES_SORT_DIRECTION_COOKIE_NAME = "productStorePurchasesSortDirection";

document.addEventListener("DOMContentLoaded", () => {
    void initProductsPage();
});

async function initProductsPage(): Promise<void> {
    searchInput = requireInputById("productsSearch");
    showOriginalNamesInput = requireInputById("productsShowOriginalNames");
    pageSizeInputs = [
        requireElementById<HTMLSelectElement>("productsTopPageSize")
    ];
    alertElement = requireElementById<HTMLElement>("productsAlert");
    accountFilterSelect = requireElementById<HTMLSelectElement>("productsAccountFilter");
    productsCountElement = requireElementById<HTMLElement>("productsCount");
    productsSumElement = requireElementById<HTMLElement>("productsSum");
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
        receiptCountSortButton: requireElementById<HTMLButtonElement>("productsReceiptCountSortButton"),
        averagePriceSortButton: requireElementById<HTMLButtonElement>("productsAveragePriceSortButton"),
        storePurchasesQuantitySortButton: requireElementById<HTMLButtonElement>("productStorePurchasesQuantitySortButton"),
        storePurchasesPriceSortButton: requireElementById<HTMLButtonElement>("productStorePurchasesPriceSortButton")
    };

    renderHelpTooltip(requireElementById<HTMLElement>("productsReceiptCountHelp"), {
        title: "Количество",
        text: "Сколько чеков текущего пользователя содержат этот товар."
    });
    renderHelpTooltip(requireElementById<HTMLElement>("productsNameHelp"), {
        title: "Наименование",
        text: "Нажмите на наименование товара, чтобы указать общее название для всех пользователей. Изменение влияет на их списки и отчёты. Товары с заданным названием выделены зелёным цветом."
    });
    renderHelpTooltip(requireElementById<HTMLElement>("productsAveragePriceHelp"), {
        title: "Средняя цена",
        text: "Рассчитывается как общая стоимость покупок, делённая на общее количество. Для весового товара цена указана за килограмм. Если товар покупался и в килограммах, и в штуках, средняя цена не отображается."
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
    requireElementById<HTMLElement>("productAdaptiveNameModal").addEventListener("hide.bs.modal", () => {
        namingContext.invalidate();
        editedProductId = null;
    });
    originalNameInput = requireInputById("productOriginalName");
    adaptiveNameInput = requireInputById("productAdaptiveName");
    clearAdaptiveNameButton = requireElementById<HTMLButtonElement>("productClearAdaptiveName");
    saveAdaptiveNameButton = requireElementById<HTMLButtonElement>("productSaveAdaptiveName");
    storePurchasesModalElement = requireElementById<HTMLElement>("productStorePurchasesModal");
    storePurchasesModal = createBootstrapModal(storePurchasesModalElement);
    storePurchasesModalElement.addEventListener("hide.bs.modal", () => purchaseRequests.invalidate());
    storePurchasesBody = requireElementById<HTMLTableSectionElement>("productStorePurchasesBody");
    storePurchasesPageSizeSelect = requireElementById<HTMLSelectElement>("productStorePurchasesPageSize");
    storePurchasesPagination = requireElementById<HTMLElement>("productStorePurchasesPagination");

    requireElementById<HTMLButtonElement>("productsApplyFilter").addEventListener("click", () => reloadFromFirstPage());
    searchInput.addEventListener("keydown", handleSearchKeyDown);
    searchInput.addEventListener("input", () => { listRequests.invalidate(); tableLoadingIndicator.hide(); });
    showOriginalNamesInput.addEventListener("change", handleShowOriginalNamesChange);
    accountFilterSelect.addEventListener("change", () => reloadFromFirstPage());
    ui.nameSortButton.addEventListener("click", () => handleSortClick("name"));
    ui.receiptCountSortButton.addEventListener("click", () => handleSortClick("receiptCount"));
    ui.averagePriceSortButton.addEventListener("click", () => handleSortClick("averagePrice"));
    ui.storePurchasesQuantitySortButton.addEventListener("click", () => handleStorePurchasesSortClick("quantity"));
    ui.storePurchasesPriceSortButton.addEventListener("click", () => handleStorePurchasesSortClick("pricePerUnit"));
    storePurchasesPageSizeSelect.addEventListener("change", () => { storePurchasesPage = 1; renderStorePurchases(); });
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
    if (!await loadAccounts()) { return; }
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

function handleStorePurchasesSortClick(sortBy: ProductStorePurchaseSortBy): void {
    if (storePurchasesSortBy === sortBy) {
        storePurchasesSortDirection = storePurchasesSortDirection === "asc" ? "desc" : "asc";
    } else {
        storePurchasesSortBy = sortBy;
        storePurchasesSortDirection = "asc";
    }

    saveStorePurchasesSortPreferences();
    renderStorePurchases();
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

    const request = listRequests.begin({ search: productsState.search, page, pageSize: productsState.pageSize,
        sortBy: productsState.sortBy, sortDirection: productsState.sortDirection, accountId: accountFilterSelect.value });
    const p = request.parameters;
    updatePageQuery(page);
    tableLoadingIndicator.show();

    try {
        hideAlertMessage(alertElement);

        const result = await getProducts(p.search, p.page, p.pageSize, p.sortBy, p.sortDirection, p.accountId, request.signal);
        if (!request.isCurrent()) {
            return;
        }

        productsState.products = result.items;
        productsState.page = result.page;
        productsState.pageSize = result.pageSize;
        productsState.totalCount = result.totalCount;
        productsState.totalPages = result.totalPages;
        productsState.hasPreviousPage = result.hasPreviousPage;
        productsState.hasNextPage = result.hasNextPage;
        productsCountElement.textContent = `Товаров: ${result.totalCount}`;
        productsSumElement.textContent = `Сумма: ${formatMoney(result.totalSum)}`;
        syncPageSizeInputs(productsState.pageSize);
        updatePageQuery(productsState.page);

        renderProducts(ui, productsState, pageNumber => {
            void loadPage(pageNumber);
        });

        tableLoadingIndicator.hide();
    }
    catch (error) {
        if (!request.isCurrent() || isIntentionalAbort(error)) {
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

    if (sortBy === "name" || sortBy === "receiptCount" || sortBy === "averagePrice") {
        productsState.sortBy = sortBy;
    }

    if (sortDirection === "asc" || sortDirection === "desc") {
        productsState.sortDirection = sortDirection;
    }

    if (pageSize === 20 || pageSize === 50 || pageSize === 100) {
        productsState.pageSize = pageSize;
    }

    const savedStorePurchasesSortBy = getCookie(STORE_PURCHASES_SORT_BY_COOKIE_NAME);
    const savedStorePurchasesSortDirection = getCookie(STORE_PURCHASES_SORT_DIRECTION_COOKIE_NAME);

    if (savedStorePurchasesSortBy === "quantity" || savedStorePurchasesSortBy === "pricePerUnit") {
        storePurchasesSortBy = savedStorePurchasesSortBy;
    }

    if (savedStorePurchasesSortDirection === "asc" || savedStorePurchasesSortDirection === "desc") {
        storePurchasesSortDirection = savedStorePurchasesSortDirection;
    }
}

function savePreferences(): void {
    setCookie(SORT_BY_COOKIE_NAME, productsState.sortBy);
    setCookie(SORT_DIRECTION_COOKIE_NAME, productsState.sortDirection);
    setCookie(PAGE_SIZE_COOKIE_NAME, String(productsState.pageSize));
}

function saveStorePurchasesSortPreferences(): void {
    setCookie(STORE_PURCHASES_SORT_BY_COOKIE_NAME, storePurchasesSortBy);
    setCookie(STORE_PURCHASES_SORT_DIRECTION_COOKIE_NAME, storePurchasesSortDirection);
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
    const request = purchaseRequests.begin(product.id);
    requireElementById<HTMLElement>("productStorePurchasesModalTitle").textContent = `Магазины, в которых был куплен товар - ${product.adaptiveName ?? product.name}`;
    storePurchases = [];
    storePurchasesPage = 1;
    renderStorePurchases();
    storePurchasesModal.show();

    try {
        hideAlertMessage(alertElement);
        const purchases = await getProductStorePurchases(request.parameters, request.signal);
        if (!request.isCurrent()) { return; }
        storePurchases = purchases;
        renderStorePurchases();
    }
    catch (error) {
        if (!request.isCurrent() || isIntentionalAbort(error)) { return; }
        storePurchasesModal.hide();
        showAlertMessage(alertElement, getErrorMessage(error));
    }
}

function renderStorePurchases(): void {
    const pageSize = Number(storePurchasesPageSizeSelect.value);
    const sortedPurchases = [...storePurchases].sort((left, right) => (left[storePurchasesSortBy] - right[storePurchasesSortBy]) * (storePurchasesSortDirection === "asc" ? 1 : -1));
    const totalPages = Math.max(1, Math.ceil(sortedPurchases.length / pageSize));
    storePurchasesPage = Math.min(storePurchasesPage, totalPages);
    requireElementById<HTMLElement>("productStorePurchasesPageSizeContainer").classList.toggle("d-none", storePurchasesPage === 1 && sortedPurchases.length < pageSize);
    renderProductStorePurchases(
        storePurchasesBody,
        sortedPurchases.slice((storePurchasesPage - 1) * pageSize, storePurchasesPage * pageSize),
        storePurchasesSortBy,
        storePurchasesSortDirection,
        ui.storePurchasesQuantitySortButton,
        ui.storePurchasesPriceSortButton);
    storePurchasesPagination.replaceChildren();
    if (totalPages > 1) for (let page = 1; page <= totalPages; page += 1) {
        const button = document.createElement("button"); button.type = "button"; button.className = page === storePurchasesPage ? "btn btn-primary" : "btn btn-outline-secondary"; button.textContent = String(page);
        button.addEventListener("click", () => { storePurchasesPage = page; renderStorePurchases(); }); storePurchasesPagination.append(button);
    }
}

async function loadAccounts(): Promise<boolean> {
    const request = accountRequests.begin(null);
    try {
        const accounts = await getProductAccounts(request.signal);
        if (!request.isCurrent()) { return false; }
        accountFilterSelect.replaceChildren();
        const allAccountsOption = document.createElement("option");
        allAccountsOption.value = "";
        allAccountsOption.textContent = "Все счета";
        accountFilterSelect.append(allAccountsOption);
        for (const account of accounts) {
            const option = document.createElement("option");
            option.value = account.id;
            option.textContent = account.name;
            accountFilterSelect.append(option);
        }
        accountFilterSelect.disabled = false;
        return true;
    } catch (error) {
        if (request.isCurrent() && !isIntentionalAbort(error)) {
            showAlertMessage(alertElement, error instanceof Error ? error.message : "Не удалось загрузить счета.");
        }
        return false;
    }
}

function formatMoney(value: number): string {
    return `${value.toLocaleString("ru-RU", { maximumFractionDigits: 2 })} ₽`;
}

function openAdaptiveNameModal(product: ProductListItem): void {
    namingContext.invalidate();
    editedProductId = product.id;
    saveAdaptiveNameButton.disabled = false;
    requireElementById<HTMLElement>("productAdaptiveNameModalTitle").textContent = `Укажите общее наименование для товара - ${product.name}`;
    originalNameInput.value = product.name;
    adaptiveNameInput.value = product.adaptiveName ?? "";
    clearAdaptiveNameButton.classList.toggle("d-none", !product.adaptiveName);
    adaptiveNameModal.show();
}

async function saveAdaptiveName(): Promise<void> {
    const id = editedProductId;
    if (!id) { throw new Error("Не выбран объект для изменения наименования."); }
    if (namingWrites.has(id)) { return; }
    const adaptiveName = adaptiveNameInput.value;
    const isCurrent = namingContext.capture();
    namingWrites.add(id);
    saveAdaptiveNameButton.disabled = true;
    try {
        hideAlertMessage(alertElement);
        await updateProductAdaptiveName(id, adaptiveName);
        if (!isCurrent()) { return; }
        await loadPage(productsState.page);
        if (isCurrent()) { adaptiveNameModal.hide(); }
    }
    catch (error) {
        if (isCurrent()) { showAlertMessage(alertElement, getWriteErrorMessage(error)); }
    }
    finally {
        namingWrites.delete(id);
        if (isCurrent()) { saveAdaptiveNameButton.disabled = false; }
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
