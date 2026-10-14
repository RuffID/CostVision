import { createRequestCoordinator, getWriteErrorMessage, isIntentionalAbort } from "../../shared/requestCoordinator.js";
import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { createBootstrapModal, type BootstrapModal } from "../../shared/bootstrap.js";
import { clearElement, requireElementById, requireInputById } from "../../shared/dom.js";
import { formatMoneyRub, formatRuNumber } from "../../shared/formatters.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { createTableLoadingIndicator, type TableLoadingIndicator } from "../../shared/tableLoadingIndicator.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { fillDeleteReceiptModal as fillDeleteReceiptModalUi } from "../reports/receipts/modals/deleteReceiptModal.js";
import { initReceiptMoneyMovementsModal, type ReceiptMoneyMovementsModalController } from "../reports/receipts/modals/receiptMoneyMovementsModal.js";
import { findReceiptAccount, findReceiptByAccountReceiptId, findReceiptById, getAvailableTargetAccounts, normalizeAvailableAccount, removeReceiptAccountLink, replaceReceiptAccountLink } from "../reports/receipts/state/accountModel.js";
import { renderReceiptDetails as renderReceiptDetailsModal } from "../reports/receipts/ui/receiptDetailsModal.js";
import { buildReceiptCard, updateCardFromDto } from "../reports/receipts/ui/receiptCards.js";
import type { AvailableAccountDto, MoveReceiptAccountAction, ReceiptDto } from "../reports/receipts/types.js";
import { getStores, getStoreReceipts, loadStoreAvailableAccounts, moveStoreReceiptToAccount, openStoreReceipt, refreshStoreReceipt, removeStoreReceiptFromAccount, updateStoreAdaptiveName } from "./api.js";
import { renderStores, type StoresUi } from "./render.js";
import { formatReportTotals } from "../reports/receipts/render.js";
import { storesState } from "./state.js";
import type { StoreListItem, StoreSortBy } from "./types.js";

let searchInput: HTMLInputElement;
let showOriginalNamesInput: HTMLInputElement;
let groupByNameInput: HTMLInputElement;
let pageSizeInputs: HTMLSelectElement[];
let alertElement: HTMLElement;
let ui: StoresUi;
let tableLoadingIndicator: TableLoadingIndicator;
let adaptiveNameModal: BootstrapModal;
let originalNameInput: HTMLInputElement;
let adaptiveNameInput: HTMLInputElement;
let clearAdaptiveNameButton: HTMLButtonElement;
let saveAdaptiveNameButton: HTMLButtonElement;
let editedStoreId: string | null = null;
let storeReceiptsModalElement: HTMLElement;
let storeReceiptsModal: BootstrapModal;
let storeReceiptsTitleElement: HTMLElement;
let storeReceiptsAlertElement: HTMLElement;
let storeReceiptsListElement: HTMLElement;
let storeReceiptsPaginationElement: HTMLElement;
let storeReceiptsPageSizeSelect: HTMLSelectElement;
let receiptDetailsModal: BootstrapModal;
let receiptDetailsListElement: HTMLElement;
let receiptDetailsHeaderElement: HTMLElement;
let receiptDetailsTotalElement: HTMLElement;
let receiptMoneyMovementsModal: ReceiptMoneyMovementsModalController;
let moveReceiptAccountModalElement: HTMLElement;
let moveReceiptAccountModal: BootstrapModal;
let moveReceiptSourceAccountElement: HTMLElement;
let moveReceiptTargetAccountSelect: HTMLSelectElement;
let moveReceiptAccountAlertElement: HTMLElement;
let confirmMoveReceiptAccountButton: HTMLButtonElement;
let removeReceiptFromAccountButton: HTMLButtonElement;
let removeReceiptAccountModal: BootstrapModal;
let removeReceiptAccountModalTitleElement: HTMLElement;
let removeReceiptAccountModalMessageElement: HTMLElement;
let removeReceiptAccountModalReceiptInfoElement: HTMLElement;
let confirmRemoveReceiptAccountButton: HTMLButtonElement;
let selectedStoreReceiptsStoreId: string | null = null;
let selectedStoreReceiptsGroupKey: string | null = null;
let selectedStoreReceiptsPage = 1;
let selectedStoreReceipts: ReceiptDto[] = [];
let availableAccounts: AvailableAccountDto[] = [];
let pendingReceiptAccountAction: MoveReceiptAccountAction | null = null;
let preserveMoveReceiptAccountModalStateOnHide = false;
let shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = false;
const listRequests = createRequestCoordinator();
const accountRequests = createRequestCoordinator();
const namingContext = createRequestCoordinator();
const namingWrites = new Set<string>();
const storeReceiptRequests = createRequestCoordinator();
const storeContext = createRequestCoordinator();
const detailRequests = createRequestCoordinator();
const accountActionContext = createRequestCoordinator();
const receiptWrites = new Set<string>();
const confirmationContext = createRequestCoordinator();
window.addEventListener("pagehide", () => { listRequests.invalidate(); accountRequests.invalidate(); namingContext.invalidate(); storeReceiptRequests.invalidate(); storeContext.invalidate(); detailRequests.invalidate(); accountActionContext.invalidate(); confirmationContext.invalidate(); });
let accountFilterSelect: HTMLSelectElement;
let storesCountElement: HTMLElement;
let storesSumElement: HTMLElement;
const SORT_BY_COOKIE_NAME = "storesSortBy";
const SORT_DIRECTION_COOKIE_NAME = "storesSortDirection";
const PAGE_SIZE_COOKIE_NAME = "storesPageSize";
const SHOW_ORIGINAL_NAMES_COOKIE_NAME = "storesShowOriginalNames";
const GROUP_BY_NAME_COOKIE_NAME = "storesGroupByName";

document.addEventListener("DOMContentLoaded", () => {
    initStoresPage().catch(error => {
        console.error(error);
        if (alertElement) {
            showAlertMessage(alertElement, getErrorMessage(error));
        }
    });
});

async function initStoresPage(): Promise<void> {
    searchInput = requireInputById("storesSearch");
    showOriginalNamesInput = requireInputById("storesShowOriginalNames");
    groupByNameInput = requireInputById("storesGroupByName");
    pageSizeInputs = [
        requireElementById<HTMLSelectElement>("storesTopPageSize")
    ];
    alertElement = requireElementById<HTMLElement>("storesAlert");
    accountFilterSelect = requireElementById<HTMLSelectElement>("storesAccountFilter");
    storesCountElement = requireElementById<HTMLElement>("storesCount");
    storesSumElement = requireElementById<HTMLElement>("storesSum");
    tableLoadingIndicator = createTableLoadingIndicator(
        requireElementById<HTMLElement>("storesTableContainer"),
        "Идёт загрузка магазинов...",
        480
    );
    storeReceiptsModalElement = requireElementById<HTMLElement>("storeReceiptsModal");
    storeReceiptsModal = createBootstrapModal(storeReceiptsModalElement);
    storeReceiptsTitleElement = requireElementById<HTMLElement>("storeReceiptsModalLabel");
    storeReceiptsAlertElement = requireElementById<HTMLElement>("storeReceiptsAlert");
    storeReceiptsListElement = requireElementById<HTMLElement>("storeReceiptsList");
    storeReceiptsPaginationElement = requireElementById<HTMLElement>("storeReceiptsPagination");
    storeReceiptsPageSizeSelect = requireElementById<HTMLSelectElement>("storeReceiptsPageSize");
    storeReceiptsPageSizeSelect.addEventListener("change", () => void loadSelectedStoreReceiptsPage(1));
    receiptDetailsModal = createBootstrapModal(requireElementById<HTMLElement>("receiptDetailsModal"));
    requireElementById<HTMLElement>("receiptDetailsModal").addEventListener("hide.bs.modal", () => detailRequests.invalidate());
    receiptDetailsListElement = requireElementById<HTMLElement>("receipt-details-list");
    receiptDetailsHeaderElement = requireElementById<HTMLElement>("receipt-details-header");
    receiptDetailsTotalElement = requireElementById<HTMLElement>("receipt-details-total");
    moveReceiptAccountModalElement = requireElementById<HTMLElement>("moveReceiptAccountModal");
    moveReceiptAccountModal = createBootstrapModal(moveReceiptAccountModalElement);
    moveReceiptSourceAccountElement = requireElementById<HTMLElement>("moveReceiptSourceAccount");
    moveReceiptTargetAccountSelect = requireElementById<HTMLSelectElement>("moveReceiptTargetAccount");
    moveReceiptAccountAlertElement = requireElementById<HTMLElement>("moveReceiptAccountAlert");
    confirmMoveReceiptAccountButton = requireElementById<HTMLButtonElement>("confirmMoveReceiptAccountBtn");
    removeReceiptFromAccountButton = requireElementById<HTMLButtonElement>("removeReceiptFromAccountBtn");
    removeReceiptAccountModal = createBootstrapModal(requireElementById<HTMLElement>("removeReceiptAccountModal"));
    removeReceiptAccountModalTitleElement = requireElementById<HTMLElement>("removeReceiptAccountModalTitle");
    removeReceiptAccountModalMessageElement = requireElementById<HTMLElement>("removeReceiptAccountModalMessage");
    removeReceiptAccountModalReceiptInfoElement = requireElementById<HTMLElement>("removeReceiptAccountModalReceiptInfo");
    confirmRemoveReceiptAccountButton = requireElementById<HTMLButtonElement>("confirmRemoveReceiptAccountBtn");
    ui = {
        tableBody: requireElementById<HTMLTableSectionElement>("storesTableBody"),
        pageInfoElements: Array.from(document.querySelectorAll<HTMLElement>("[data-stores-page-info]")),
        paginationElements: Array.from(document.querySelectorAll<HTMLElement>("[data-stores-pagination]")),
        nameSortButton: requireElementById<HTMLButtonElement>("storesNameSortButton"),
        receiptCountSortButton: requireElementById<HTMLButtonElement>("storesReceiptCountSortButton"),
        totalSpentSortButton: requireElementById<HTMLButtonElement>("storesTotalSpentSortButton")
    };

    renderHelpTooltip(requireElementById<HTMLElement>("storesNameHelp"), {
        title: "Наименование",
        text: "Нажмите на наименование магазина, чтобы указать общее название для всех пользователей. Изменение влияет на их списки и отчёты. Магазины с заданным названием выделены зелёным цветом."
    });
    renderHelpTooltip(requireElementById<HTMLElement>("storesShowOriginalNamesHelp"), {
        title: "Отображать оригинальные названия",
        text: "Показывает исходные названия магазинов вместо указанных Вами названий."
    });
    renderHelpTooltip(requireElementById<HTMLElement>("storesGroupByNameHelp"), {
        title: "Группировать магазины по названию",
        text: "Объединяет магазины с одинаковым названием в одну строку."
    });

    adaptiveNameModal = createBootstrapModal(requireElementById<HTMLElement>("storeAdaptiveNameModal"));
    requireElementById<HTMLElement>("storeAdaptiveNameModal").addEventListener("hide.bs.modal", () => {
        namingContext.invalidate();
        editedStoreId = null;
    });
    originalNameInput = requireInputById("storeOriginalName");
    adaptiveNameInput = requireInputById("storeAdaptiveName");
    clearAdaptiveNameButton = requireElementById<HTMLButtonElement>("storeClearAdaptiveName");
    saveAdaptiveNameButton = requireElementById<HTMLButtonElement>("storeSaveAdaptiveName");

    requireElementById<HTMLButtonElement>("storesApplyFilter").addEventListener("click", () => reloadFromFirstPage());
    searchInput.addEventListener("keydown", handleSearchKeyDown);
    searchInput.addEventListener("input", () => { listRequests.invalidate(); tableLoadingIndicator.hide(); });
    showOriginalNamesInput.addEventListener("change", handleShowOriginalNamesChange);
    groupByNameInput.addEventListener("change", () => reloadFromFirstPage());
    accountFilterSelect.addEventListener("change", () => reloadFromFirstPage());
    ui.nameSortButton.addEventListener("click", () => handleSortClick("name"));
    ui.receiptCountSortButton.addEventListener("click", () => handleSortClick("receiptCount"));
    ui.totalSpentSortButton.addEventListener("click", () => handleSortClick("totalSpent"));
    for (const pageSizeInput of pageSizeInputs) {
        pageSizeInput.addEventListener("change", () => reloadFromFirstPage(pageSizeInput));
    }
    ui.tableBody.addEventListener("click", event => {
        void handleTableClick(event);
    });
    requireElementById<HTMLFormElement>("storeAdaptiveNameForm").addEventListener("submit", event => {
        event.preventDefault();
        void saveAdaptiveName();
    });
    clearAdaptiveNameButton.addEventListener("click", () => {
        adaptiveNameInput.value = "";
        adaptiveNameInput.focus();
    });
    storeReceiptsListElement.addEventListener("click", event => {
        void handleStoreReceiptClick(event);
    });
    storeReceiptsModalElement.addEventListener("hide.bs.modal", clearSelectedStoreReceipts);
    moveReceiptAccountModalElement.addEventListener("hide.bs.modal", () => {
        if (preserveMoveReceiptAccountModalStateOnHide) {
            return;
        }

        clearMoveReceiptAccountState();
    });
    const removeModalElement = requireElementById<HTMLElement>("removeReceiptAccountModal");
    removeModalElement.addEventListener("hide.bs.modal", () => {
        const isCurrent = confirmationContext.capture();
        const matchesStore = storeContext.capture();
        const action = pendingReceiptAccountAction;
        const restore = shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation;
        if (!restore) { pendingReceiptAccountAction = null; }
        removeModalElement.addEventListener("hidden.bs.modal", () => {
            if (!isCurrent() || !matchesStore() || !restore || pendingReceiptAccountAction !== action) { return; }
            shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = false;
            preserveMoveReceiptAccountModalStateOnHide = false;
            moveReceiptAccountModal.show();
        }, { once: true });
    });
    confirmMoveReceiptAccountButton.addEventListener("click", () => {
        void onConfirmMoveReceiptToAccount();
    });
    removeReceiptFromAccountButton.addEventListener("click", openRemoveReceiptFromAccountConfirmation);
    confirmRemoveReceiptAccountButton.addEventListener("click", () => {
        void onConfirmRemoveReceiptFromAccount();
    });
    receiptMoneyMovementsModal = initReceiptMoneyMovementsModal({
        getReceipts: () => selectedStoreReceipts,
        getForgeryToken: () => getRequestVerificationToken(),
        reloadReceiptList: () => loadSelectedStoreReceiptsPage(selectedStoreReceiptsPage),
        formatCurrency: formatCurrency
    });

    restorePreferences();
    syncPageSizeInputs(storesState.pageSize);
    if (!await loadAvailableAccounts()) { return; }
    await loadPage(getPageFromQuery());
}

function handleSortClick(sortBy: StoreSortBy): void {
    if (storesState.sortBy === sortBy) {
        storesState.sortDirection = storesState.sortDirection === "asc" ? "desc" : "asc";
    } else {
        storesState.sortBy = sortBy;
        storesState.sortDirection = "asc";
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
    storesState.search = searchInput.value.trim();
    storesState.groupByName = groupByNameInput.checked;
    storesState.pageSize = Number((changedPageSizeInput ?? pageSizeInputs[0]).value);
    syncPageSizeInputs(storesState.pageSize);
    savePreferences();
    void loadPage(1);
}

async function loadPage(page: number): Promise<void> {
    if (page <= 0) {
        return;
    }

    const request = listRequests.begin({ search: storesState.search, page, pageSize: storesState.pageSize,
        sortBy: storesState.sortBy, sortDirection: storesState.sortDirection, accountId: accountFilterSelect.value, groupByName: storesState.groupByName });
    const p = request.parameters;
    updatePageQuery(page);
    tableLoadingIndicator.show();

    try {
        hideAlertMessage(alertElement);

        const result = await getStores(p.search, p.groupByName, p.page, p.pageSize, p.sortBy, p.sortDirection, p.accountId, request.signal);
        if (!request.isCurrent()) {
            return;
        }

        storesState.stores = result.items;
        storesState.page = result.page;
        storesState.pageSize = result.pageSize;
        storesState.totalCount = result.totalCount;
        storesState.totalPages = result.totalPages;
        storesState.hasPreviousPage = result.hasPreviousPage;
        storesState.hasNextPage = result.hasNextPage;
        storesCountElement.textContent = `Магазинов: ${result.totalCount}`;
        storesSumElement.textContent = formatReportTotals(result.totalSum, result.incomeSum, formatCurrency);
        syncPageSizeInputs(storesState.pageSize);
        updatePageQuery(storesState.page);

        renderStores(ui, storesState, pageNumber => {
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

    if (sortBy === "name" || sortBy === "receiptCount" || sortBy === "totalSpent") {
        storesState.sortBy = sortBy;
    }

    if (sortDirection === "asc" || sortDirection === "desc") {
        storesState.sortDirection = sortDirection;
    }

    if (pageSize === 20 || pageSize === 50 || pageSize === 100) {
        storesState.pageSize = pageSize;
    }

    storesState.showOriginalNames = getCookie(SHOW_ORIGINAL_NAMES_COOKIE_NAME) === "true";
    storesState.groupByName = getCookie(GROUP_BY_NAME_COOKIE_NAME) === "true";
    showOriginalNamesInput.checked = storesState.showOriginalNames;
    groupByNameInput.checked = storesState.groupByName;
}

function savePreferences(): void {
    setCookie(SORT_BY_COOKIE_NAME, storesState.sortBy);
    setCookie(SORT_DIRECTION_COOKIE_NAME, storesState.sortDirection);
    setCookie(PAGE_SIZE_COOKIE_NAME, String(storesState.pageSize));
    setCookie(SHOW_ORIGINAL_NAMES_COOKIE_NAME, String(storesState.showOriginalNames));
    setCookie(GROUP_BY_NAME_COOKIE_NAME, String(storesState.groupByName));
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

async function handleTableClick(event: MouseEvent): Promise<void> {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const receiptsButton = target.closest<HTMLButtonElement>("[data-store-receipts-button]");
    if (receiptsButton) {
        await openStoreReceiptsModal(receiptsButton);
        return;
    }

    const adaptiveNameButton = target.closest<HTMLButtonElement>("[data-store-adaptive-name-button]");
    if (!adaptiveNameButton) {
        return;
    }

    const row = adaptiveNameButton.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.storeId) {
        throw new Error("Не найдена строка магазина.");
    }

    openAdaptiveNameModal(getStoreById(row.dataset.storeId));
}

function handleShowOriginalNamesChange(): void {
    storesState.showOriginalNames = showOriginalNamesInput.checked;
    savePreferences();
    renderStores(ui, storesState, pageNumber => {
        void loadPage(pageNumber);
    });
}

function openAdaptiveNameModal(store: StoreListItem): void {
    namingContext.invalidate();
    editedStoreId = store.id;
    saveAdaptiveNameButton.disabled = false;
    requireElementById<HTMLElement>("storeAdaptiveNameModalTitle").textContent = `Укажите общее наименование для магазина - ${store.name}`;
    originalNameInput.value = store.name;
    adaptiveNameInput.value = store.adaptiveName ?? "";
    clearAdaptiveNameButton.classList.toggle("d-none", !store.adaptiveName);
    adaptiveNameModal.show();
}

async function saveAdaptiveName(): Promise<void> {
    const id = editedStoreId;
    if (!id) { throw new Error("Не выбран объект для изменения наименования."); }
    if (namingWrites.has(id)) { return; }
    const adaptiveName = adaptiveNameInput.value;
    const isCurrent = namingContext.capture();
    namingWrites.add(id);
    saveAdaptiveNameButton.disabled = true;
    try {
        hideAlertMessage(alertElement);
        await updateStoreAdaptiveName(id, adaptiveName);
        if (!isCurrent()) { return; }
        await loadPage(storesState.page);
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

function getStoreById(storeId: string): StoreListItem {
    const store = storesState.stores
        .flatMap(item => [item, ...item.children])
        .find(item => item.id === storeId);
    if (!store) {
        throw new Error("Магазин не найден в состоянии страницы.");
    }

    return store;
}

function getStoreByGroupKey(storeGroupKey: string): StoreListItem {
    const store = storesState.stores.find(item => item.groupKey === storeGroupKey);
    if (!store) {
        throw new Error("Группа магазинов не найдена в состоянии страницы.");
    }

    return store;
}

async function openStoreReceiptsModal(button: HTMLButtonElement): Promise<void> {
    const storeId = button.dataset.storeGroupKey ? null : button.dataset.storeId ?? null;
    const groupKey = button.dataset.storeGroupKey ?? null;
    const store = groupKey ? getStoreByGroupKey(groupKey) : getStoreById(button.dataset.storeId ?? "");

    storeContext.invalidate();
    storeReceiptRequests.invalidate();
    detailRequests.invalidate();
    selectedStoreReceipts = [];
    selectedStoreReceiptsStoreId = storeId;
    selectedStoreReceiptsGroupKey = groupKey;
    selectedStoreReceiptsPage = 1;
    storeReceiptsTitleElement.textContent = "Чеки магазина - " + (store.displayName || store.name || "Магазин");
    requireElementById<HTMLElement>("storeReceiptsSummary").textContent = "Итоги ещё не загружены";
    hideStoreReceiptsAlert();
    clearElement(storeReceiptsListElement);
    clearElement(storeReceiptsPaginationElement);
    storeReceiptsModal.show();

    await loadSelectedStoreReceiptsPage(1);
}

async function loadSelectedStoreReceiptsPage(page: number): Promise<void> {
    if (!selectedStoreReceiptsStoreId && !selectedStoreReceiptsGroupKey) {
        throw new Error("Не выбран магазин для просмотра чеков.");
    }

    const request = storeReceiptRequests.begin({ storeId: selectedStoreReceiptsStoreId,
        groupKey: selectedStoreReceiptsGroupKey, page, pageSize: Number(storeReceiptsPageSizeSelect.value) });
    const p = request.parameters;
    try {
        hideStoreReceiptsAlert();
        const result = await getStoreReceipts(p.storeId, p.groupKey, p.page, p.pageSize, request.signal);
        if (!request.isCurrent()) { return; }
        selectedStoreReceipts = result.items;
        selectedStoreReceiptsPage = result.page;
        requireElementById<HTMLElement>("storeReceiptsSummary").textContent = formatReportTotals(result.totalSum, result.incomeSum, formatCurrency);
        renderStoreReceipts(result.items);
        renderStoreReceiptsPagination(result.page, result.totalPages, result.totalCount, result.pageSize, result.hasPreviousPage, result.hasNextPage);
    }
    catch (error) {
        if (!request.isCurrent() || isIntentionalAbort(error)) { return; }
        showStoreReceiptsAlert(getErrorMessage(error));
    }
}

function renderStoreReceipts(receipts: ReceiptDto[]): void {
    clearElement(storeReceiptsListElement);

    if (receipts.length === 0) {
        const empty = document.createElement("div");
        empty.className = "text-muted py-2";
        empty.textContent = "Чеки магазина не найдены.";
        storeReceiptsListElement.append(empty);
        return;
    }

    for (const receipt of receipts) {
        const card = buildReceiptCard(receipt, formatCurrency);
        removeUnsupportedReceiptActions(card);
        storeReceiptsListElement.append(card);
    }
}

function removeUnsupportedReceiptActions(card: HTMLElement): void {
    const deleteButton = card.querySelector('[data-action="delete"]');
    deleteButton?.remove();
}

function renderStoreReceiptsPagination(page: number, totalPages: number, totalCount: number, pageSize: number, hasPreviousPage: boolean, hasNextPage: boolean): void {
    clearElement(storeReceiptsPaginationElement);

    requireElementById<HTMLElement>("storeReceiptsPageSizeContainer").classList.toggle("d-none", page === 1 && totalCount < pageSize);

    if (totalPages <= 1) {
        return;
    }

    if (hasPreviousPage) {
        storeReceiptsPaginationElement.append(createStoreReceiptsPageButton("<<", () => loadSelectedStoreReceiptsPage(1), false));
        storeReceiptsPaginationElement.append(createStoreReceiptsPageButton("<", () => loadSelectedStoreReceiptsPage(page - 1), false));
    }

    for (const pageNumber of getPageRange(page, totalPages)) {
        storeReceiptsPaginationElement.append(createStoreReceiptsPageButton(String(pageNumber), () => loadSelectedStoreReceiptsPage(pageNumber), pageNumber === page));
    }

    if (hasNextPage) {
        storeReceiptsPaginationElement.append(createStoreReceiptsPageButton(">", () => loadSelectedStoreReceiptsPage(page + 1), false));
        storeReceiptsPaginationElement.append(createStoreReceiptsPageButton(">>", () => loadSelectedStoreReceiptsPage(totalPages), false));
    }
}

function createStoreReceiptsPageButton(text: string, onClick: () => Promise<void>, isActive: boolean): HTMLButtonElement {
    const button = document.createElement("button");
    button.type = "button";
    button.className = isActive ? "btn btn-primary" : "btn btn-outline-secondary";
    button.textContent = text;
    button.addEventListener("click", () => {
        void onClick();
    });
    return button;
}

function getPageRange(currentPage: number, totalPages: number): number[] {
    let start = 1;

    if (currentPage >= 5) {
        start = currentPage - 2;
    }

    if (start + 4 > totalPages) {
        start = Math.max(1, totalPages - 4);
    }

    const end = Math.min(totalPages, start + 4);
    const pages: number[] = [];

    for (let page = start; page <= end; page += 1) {
        pages.push(page);
    }

    return pages;
}

async function handleStoreReceiptClick(event: MouseEvent): Promise<void> {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const accountButton = target.closest<HTMLButtonElement>('[data-action="edit-account-link"]');
    const assignAccountButton = target.closest<HTMLButtonElement>('[data-action="assign-account-link"]');
    if (accountButton) {
        if (accountButton.disabled) {
            return;
        }

        const receiptId = accountButton.dataset.accountReceiptId;
        const accountId = accountButton.dataset.accountId;
        if (receiptId && accountId) {
            await openMoveReceiptAccountModal(receiptId, accountId);
        }
        return;
    }

    if (assignAccountButton) {
        const receiptId = assignAccountButton.closest<HTMLElement>(".card")?.dataset.receiptId;
        if (receiptId) {
            await openMoveReceiptAccountModal(receiptId, "");
        }
        return;
    }

    const moneyMovementsButton = target.closest<HTMLButtonElement>('[data-action="open-money-movements"]');
    if (moneyMovementsButton) {
        const receiptId = moneyMovementsButton.dataset.receiptId;
        if (receiptId) {
            await receiptMoneyMovementsModal.open(receiptId);
        }
        return;
    }

    const openButton = target.closest<HTMLButtonElement>('[data-action="open"]');
    if (openButton) {
        const receiptId = getReceiptCardId(openButton);
        if (receiptId) {
            await openReceipt(receiptId);
        }
        return;
    }

    const refreshButton = target.closest<HTMLButtonElement>('[data-action="refresh"]');
    if (refreshButton) {
        const card = refreshButton.closest<HTMLElement>(".card");
        const receiptId = card?.dataset.receiptId;
        if (card && receiptId) {
            await refreshReceipt(receiptId, card, refreshButton);
        }
    }
}

async function loadAvailableAccounts(): Promise<boolean> {
    const request = accountRequests.begin(null);
    try {
        const accounts = await loadStoreAvailableAccounts(request.signal);
        if (!request.isCurrent()) { return false; }
        availableAccounts = accounts.map(normalizeAvailableAccount);
        accountFilterSelect.replaceChildren();
        const allAccountsOption = document.createElement("option");
        allAccountsOption.value = "";
        allAccountsOption.textContent = "Все счета";
        accountFilterSelect.append(allAccountsOption);
        for (const account of availableAccounts) {
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

async function openMoveReceiptAccountModal(receiptId: string, sourceAccountId: string): Promise<void> {
    confirmationContext.invalidate();
    const request = accountActionContext.begin({ receiptId, sourceAccountId });
    const matchesStore = storeContext.capture();
    if (availableAccounts.length === 0) {
        if (!await loadAvailableAccounts()) { return; }
        if (!request.isCurrent() || !matchesStore()) { return; }
    }

    const receipt = sourceAccountId
        ? findReceiptByAccountReceiptId(selectedStoreReceipts, receiptId, sourceAccountId)
        : findReceiptById(selectedStoreReceipts, receiptId);
    if (!receipt) {
        showStoreReceiptsAlert("Чек не найден в текущем списке.");
        return;
    }

    const sourceAccount = sourceAccountId ? findReceiptAccount(receipt, sourceAccountId, receiptId) : null;
    if (sourceAccountId && !sourceAccount) {
        showStoreReceiptsAlert("Связь со счётом не найдена.");
        return;
    }

    if (sourceAccount && !sourceAccount.canEditReceipt) {
        showStoreReceiptsAlert("Недостаточно прав для изменения чека в выбранном счёте.");
        return;
    }

    confirmMoveReceiptAccountButton.textContent = "Перенести";
    pendingReceiptAccountAction = {
        receiptId: receiptId,
        sourceAccountId: sourceAccountId
    };

    moveReceiptSourceAccountElement.textContent = sourceAccount ? sourceAccount.name : "Без счёта";
    moveReceiptSourceAccountElement.style.border = "2px solid " + (sourceAccount ? sourceAccount.colorHex : "#dee2e6");
    hideMoveReceiptAccountAlert();
    renderMoveReceiptTargetOptions(receipt, sourceAccountId);
    updateMoveReceiptActionState();
    moveReceiptAccountModal.show();
}

function renderMoveReceiptTargetOptions(receipt: ReceiptDto, sourceAccountId: string): void {
    const selectedValue = moveReceiptTargetAccountSelect.value || "";
    moveReceiptTargetAccountSelect.replaceChildren();

    const accounts = getAvailableTargetAccounts(availableAccounts, receipt, sourceAccountId);
    for (const account of accounts) {
        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;
        option.disabled = account.isDisabled === true;
        option.setAttribute("data-reason", account.disabledReason || "");
        moveReceiptTargetAccountSelect.appendChild(option);
    }

    if (selectedValue) {
        const matchingOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(option => option.value === selectedValue && option.disabled === false);
        moveReceiptTargetAccountSelect.value = matchingOption ? selectedValue : "";
    } else {
        const firstEnabledOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(option => option.value && option.disabled === false);
        moveReceiptTargetAccountSelect.value = firstEnabledOption ? firstEnabledOption.value : "";
    }

    moveReceiptTargetAccountSelect.disabled = accounts.length === 0;
    moveReceiptTargetAccountSelect.onchange = updateMoveReceiptActionState;
}

function updateMoveReceiptActionState(): void {
    const canRemove = canRemoveReceiptFromSelectedAccount();
    const selectedOption = getSelectedMoveReceiptTargetOption();
    const hasOtherAccounts = moveReceiptTargetAccountSelect.options.length > 0;
    const canMove = hasOtherAccounts && !!selectedOption && !selectedOption.disabled;

    removeReceiptFromAccountButton.disabled = !canRemove;
    confirmMoveReceiptAccountButton.disabled = !canMove;

    if (!hasOtherAccounts) {
        hideMoveReceiptAccountAlert();
        return;
    }

    if (!selectedOption) {
        const unavailableReason = getUnavailableMoveReceiptReason();
        if (unavailableReason) {
            showMoveReceiptAccountAlert(unavailableReason);
            return;
        }

        hideMoveReceiptAccountAlert();
        return;
    }

    const reason = selectedOption.getAttribute("data-reason") || "";
    if (selectedOption.disabled && reason) {
        showMoveReceiptAccountAlert(reason);
        return;
    }

    hideMoveReceiptAccountAlert();
}

function getUnavailableMoveReceiptReason(): string {
    if (moveReceiptTargetAccountSelect.options.length === 0) {
        return "";
    }

    const enabledOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(option => option.value && option.disabled === false);
    if (enabledOption) {
        return "";
    }

    const disabledOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(option => option.value && option.disabled === true && option.getAttribute("data-reason"));
    return disabledOption ? disabledOption.getAttribute("data-reason") || "" : "Нет доступных счетов для переноса этого чека.";
}

async function onConfirmMoveReceiptToAccount(): Promise<void> {
    if (!pendingReceiptAccountAction) {
        return;
    }

    const action = pendingReceiptAccountAction;
    if (receiptWrites.has(action.receiptId)) { return; }
    const matchesStore = storeContext.capture();
    const matchesPage = storeReceiptRequests.capture();
    const isCurrent = () => pendingReceiptAccountAction === action && matchesStore() && matchesPage();
    const selectedOption = getSelectedMoveReceiptTargetOption();
    if (!selectedOption || selectedOption.disabled) {
        updateMoveReceiptActionState();
        return;
    }

    const targetAccountId = selectedOption.value;
    receiptWrites.add(action.receiptId);
    const originalText = confirmMoveReceiptAccountButton.textContent;
    confirmMoveReceiptAccountButton.disabled = true;
    confirmMoveReceiptAccountButton.textContent = "Перенос...";
    removeReceiptFromAccountButton.disabled = true;

    try {
        await moveStoreReceiptToAccount(action.receiptId, action.sourceAccountId, targetAccountId);
        if (!isCurrent()) { return; }
        replaceReceiptAccountLink(selectedStoreReceipts, availableAccounts, action.receiptId, action.sourceAccountId, targetAccountId);
        closeMoveReceiptAccountModal();
        await loadSelectedStoreReceiptsPage(selectedStoreReceiptsPage);
    }
    catch (error) {
        if (isCurrent()) { showMoveReceiptAccountAlert(getWriteErrorMessage(error)); }
    }
    finally {
        receiptWrites.delete(action.receiptId);
        if (pendingReceiptAccountAction !== action) { return; }
        confirmMoveReceiptAccountButton.textContent = originalText;
        updateMoveReceiptActionState();
    }
}

function openRemoveReceiptFromAccountConfirmation(): void {
    confirmationContext.invalidate();
    if (!pendingReceiptAccountAction) {
        return;
    }

    if (!canRemoveReceiptFromSelectedAccount()) {
        updateMoveReceiptActionState();
        return;
    }

    const receipt = findReceiptByAccountReceiptId(selectedStoreReceipts, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
    if (!receipt) {
        showMoveReceiptAccountAlert("Чек не найден в текущем списке.");
        return;
    }

    fillDeleteReceiptModalUi(
        {
            titleElement: removeReceiptAccountModalTitleElement,
            messageElement: removeReceiptAccountModalMessageElement,
            receiptInfoElement: removeReceiptAccountModalReceiptInfoElement
        },
        "Удаление чека из счёта",
        "Вы уверены, что хотите удалить этот чек из счёта?",
        receipt,
        formatCurrency
    );

    confirmRemoveReceiptAccountButton.disabled = false;
    confirmRemoveReceiptAccountButton.textContent = "Удалить";
    preserveMoveReceiptAccountModalStateOnHide = true;
    shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = true;
    moveReceiptAccountModal.hide();
    removeReceiptAccountModal.show();
}

async function onConfirmRemoveReceiptFromAccount(): Promise<void> {
    if (!pendingReceiptAccountAction) {
        return;
    }

    const action = pendingReceiptAccountAction;
    if (receiptWrites.has(action.receiptId)) { return; }
    const matchesStore = storeContext.capture();
    const matchesPage = storeReceiptRequests.capture();
    const isCurrent = () => pendingReceiptAccountAction === action && matchesStore() && matchesPage();
    receiptWrites.add(action.receiptId);
    const originalText = confirmRemoveReceiptAccountButton.textContent;
    confirmRemoveReceiptAccountButton.disabled = true;
    confirmRemoveReceiptAccountButton.textContent = "Удаление...";

    try {
        shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = false;
        preserveMoveReceiptAccountModalStateOnHide = false;
        await removeStoreReceiptFromAccount(action.receiptId, action.sourceAccountId);
        if (!isCurrent()) { return; }
        selectedStoreReceipts = removeReceiptAccountLink(selectedStoreReceipts, action.receiptId, action.sourceAccountId);
        closeMoveReceiptAccountModal();
        removeReceiptAccountModal.hide();
        await loadSelectedStoreReceiptsPage(selectedStoreReceiptsPage);
    }
    catch (error) {
        if (isCurrent()) { showStoreReceiptsAlert(getWriteErrorMessage(error)); }
    }
    finally {
        receiptWrites.delete(action.receiptId);
        if (pendingReceiptAccountAction !== action) { return; }
        confirmRemoveReceiptAccountButton.disabled = false;
        confirmRemoveReceiptAccountButton.textContent = originalText;
    }
}

function getSelectedMoveReceiptTargetOption(): HTMLOptionElement | null {
    const selectedIndex = moveReceiptTargetAccountSelect.selectedIndex;
    if (selectedIndex < 0) {
        return null;
    }

    const option = moveReceiptTargetAccountSelect.options[selectedIndex];
    if (!option || !option.value) {
        return null;
    }

    return option;
}

function canRemoveReceiptFromSelectedAccount(): boolean {
    if (!pendingReceiptAccountAction) {
        return false;
    }

    const receipt = findReceiptByAccountReceiptId(selectedStoreReceipts, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
    if (!receipt) {
        return false;
    }

    const sourceAccount = findReceiptAccount(receipt, pendingReceiptAccountAction.sourceAccountId, pendingReceiptAccountAction.receiptId);
    return sourceAccount?.canEditReceipt === true;
}

function closeMoveReceiptAccountModal(): void {
    clearMoveReceiptAccountState();
    moveReceiptAccountModal.hide();
}

function clearMoveReceiptAccountState(): void {
    accountActionContext.invalidate();
    pendingReceiptAccountAction = null;
    hideMoveReceiptAccountAlert();
    moveReceiptTargetAccountSelect.replaceChildren();
    moveReceiptTargetAccountSelect.disabled = false;
    moveReceiptSourceAccountElement.textContent = "";
    moveReceiptSourceAccountElement.style.border = "";
}

function showMoveReceiptAccountAlert(message: string): void {
    moveReceiptAccountAlertElement.textContent = message || "";
    moveReceiptAccountAlertElement.classList.toggle("d-none", !message);
}

function hideMoveReceiptAccountAlert(): void {
    showMoveReceiptAccountAlert("");
}

function getReceiptCardId(element: HTMLElement): string | null {
    return element.closest<HTMLElement>(".card")?.dataset.receiptId ?? null;
}

async function openReceipt(receiptId: string): Promise<void> {
    const request = detailRequests.begin(receiptId);
    const matchesStore = storeContext.capture();
    try {
        const receipt = await openStoreReceipt(request.parameters, request.signal);
        if (!request.isCurrent() || !matchesStore()) { return; }
        renderReceiptDetailsModal(
            receipt,
            {
                detailsList: receiptDetailsListElement,
                modalHeader: receiptDetailsHeaderElement,
                modalTotal: receiptDetailsTotalElement,
                bootstrapModal: receiptDetailsModal
            },
            formatNumber,
            formatCurrency
        );
    }
    catch (error) {
        if (!request.isCurrent() || !matchesStore() || isIntentionalAbort(error)) { return; }
        showStoreReceiptsAlert(getErrorMessage(error));
    }
}

async function refreshReceipt(receiptId: string, card: HTMLElement, button: HTMLButtonElement): Promise<void> {
    if (button.disabled || receiptWrites.has(receiptId)) { return; }
    receiptWrites.add(receiptId);
    const matchesStore = storeContext.capture();
    const matchesPage = storeReceiptRequests.capture();
    const isCurrent = () => matchesStore() && matchesPage();
    const originalText = button.textContent;
    button.disabled = true;
    button.textContent = "Обновление...";

    try {
        const receipt = await refreshStoreReceipt(receiptId);
        if (!isCurrent()) { return; }
        updateCardFromDto(card, receipt, formatCurrency);
        replaceSelectedReceipt(receipt);
    }
    catch (error) {
        if (isCurrent()) { showStoreReceiptsAlert(getWriteErrorMessage(error)); }
    }
    finally {
        receiptWrites.delete(receiptId);
        if (!isCurrent()) { return; }
        button.disabled = false;
        button.textContent = originalText;
    }
}

function replaceSelectedReceipt(receipt: ReceiptDto): void {
    selectedStoreReceipts = selectedStoreReceipts.map(item => item.id === receipt.id ? receipt : item);
}

function clearSelectedStoreReceipts(): void {
    storeContext.invalidate();
    storeReceiptRequests.invalidate();
    detailRequests.invalidate();
    selectedStoreReceiptsStoreId = null;
    selectedStoreReceiptsGroupKey = null;
    selectedStoreReceiptsPage = 1;
    selectedStoreReceipts = [];
    hideStoreReceiptsAlert();
}

function showStoreReceiptsAlert(message: string): void {
    storeReceiptsAlertElement.textContent = message;
    storeReceiptsAlertElement.classList.toggle("d-none", !message);
}

function hideStoreReceiptsAlert(): void {
    showStoreReceiptsAlert("");
}

function formatNumber(value: number): string {
    return formatRuNumber(value);
}

function formatCurrency(value: number): string {
    return formatMoneyRub(value);
}

function getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : "Не удалось выполнить действие.";
}
