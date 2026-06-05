import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { createBootstrapModal, type BootstrapModal } from "../../shared/bootstrap.js";
import { clearElement, requireElementById, requireInputById } from "../../shared/dom.js";
import { formatMoneyRub, formatRuNumber } from "../../shared/formatters.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { fillDeleteReceiptModal as fillDeleteReceiptModalUi } from "../reports/receipts/modals/deleteReceiptModal.js";
import { initReceiptMoneyMovementsModal, type ReceiptMoneyMovementsModalController } from "../reports/receipts/modals/receiptMoneyMovementsModal.js";
import { findReceiptAccount, findReceiptByAccountReceiptId, findReceiptById, getAvailableTargetAccounts, normalizeAvailableAccount, removeReceiptAccountLink, replaceReceiptAccountLink } from "../reports/receipts/state/accountModel.js";
import { renderReceiptDetails as renderReceiptDetailsModal } from "../reports/receipts/ui/receiptDetailsModal.js";
import { buildReceiptCard, updateCardFromDto } from "../reports/receipts/ui/receiptCards.js";
import type { AvailableAccountDto, MoveReceiptAccountAction, ReceiptDto } from "../reports/receipts/types.js";
import { getStores, getStoreReceipts, loadStoreAvailableAccounts, moveStoreReceiptToAccount, openStoreReceipt, refreshStoreReceipt, removeStoreReceiptFromAccount, updateStoreAdaptiveName } from "./api.js";
import { createStoreRow, renderStores, updateSaveButtonVisibility, type StoresUi } from "./render.js";
import { storesState } from "./state.js";
import type { StoreListItem, StoreSortBy } from "./types.js";

let searchInput: HTMLInputElement;
let useAdaptiveNamesInput: HTMLInputElement;
let pageSizeInputs: HTMLSelectElement[];
let alertElement: HTMLElement;
let ui: StoresUi;
let storeReceiptsModalElement: HTMLElement;
let storeReceiptsModal: BootstrapModal;
let storeReceiptsTitleElement: HTMLElement;
let storeReceiptsAlertElement: HTMLElement;
let storeReceiptsListElement: HTMLElement;
let storeReceiptsPaginationElement: HTMLElement;
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
const STORE_RECEIPTS_PAGE_SIZE = 20;

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
    useAdaptiveNamesInput = requireInputById("storesUseAdaptiveNames");
    pageSizeInputs = [
        requireElementById<HTMLSelectElement>("storesTopPageSize")
    ];
    alertElement = requireElementById<HTMLElement>("storesAlert");
    storeReceiptsModalElement = requireElementById<HTMLElement>("storeReceiptsModal");
    storeReceiptsModal = createBootstrapModal(storeReceiptsModalElement);
    storeReceiptsTitleElement = requireElementById<HTMLElement>("storeReceiptsModalLabel");
    storeReceiptsAlertElement = requireElementById<HTMLElement>("storeReceiptsAlert");
    storeReceiptsListElement = requireElementById<HTMLElement>("storeReceiptsList");
    storeReceiptsPaginationElement = requireElementById<HTMLElement>("storeReceiptsPagination");
    receiptDetailsModal = createBootstrapModal(requireElementById<HTMLElement>("receiptDetailsModal"));
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
        adaptiveNameHeader: requireElementById<HTMLElement>("storesAdaptiveNameHeader"),
        actionsHeader: requireElementById<HTMLElement>("storesActionsHeader"),
        nameSortButton: requireElementById<HTMLButtonElement>("storesNameSortButton"),
        receiptCountSortButton: requireElementById<HTMLButtonElement>("storesReceiptCountSortButton")
    };

    renderHelpTooltip(requireElementById<HTMLElement>("storesReceiptCountHelp"), {
        title: "Количество",
        text: "Сколько чеков текущего пользователя относятся к этому магазину."
    });

    requireElementById<HTMLButtonElement>("storesApplyFilter").addEventListener("click", () => reloadFromFirstPage());
    searchInput.addEventListener("keydown", handleSearchKeyDown);
    useAdaptiveNamesInput.addEventListener("change", () => reloadFromFirstPage());
    ui.nameSortButton.addEventListener("click", () => handleSortClick("name"));
    ui.receiptCountSortButton.addEventListener("click", () => handleSortClick("receiptCount"));
    for (const pageSizeInput of pageSizeInputs) {
        pageSizeInput.addEventListener("change", () => reloadFromFirstPage(pageSizeInput));
    }
    ui.tableBody.addEventListener("input", handleTableInput);
    ui.tableBody.addEventListener("click", event => {
        void handleTableClick(event);
    });
    storeReceiptsListElement.addEventListener("click", event => {
        void handleStoreReceiptClick(event);
    });
    storeReceiptsModalElement.addEventListener("hidden.bs.modal", clearSelectedStoreReceipts);
    moveReceiptAccountModalElement.addEventListener("hidden.bs.modal", () => {
        if (preserveMoveReceiptAccountModalStateOnHide) {
            return;
        }

        clearMoveReceiptAccountState();
    });
    requireElementById<HTMLElement>("removeReceiptAccountModal").addEventListener("hidden.bs.modal", () => {
        if (shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation) {
            shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = false;
            preserveMoveReceiptAccountModalStateOnHide = false;
            moveReceiptAccountModal.show();
            return;
        }

        pendingReceiptAccountAction = null;
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

    await loadAvailableAccounts();
    await loadPage(1);
}

function handleSortClick(sortBy: StoreSortBy): void {
    if (storesState.sortBy === sortBy) {
        storesState.sortDirection = storesState.sortDirection === "asc" ? "desc" : "asc";
    } else {
        storesState.sortBy = sortBy;
        storesState.sortDirection = "asc";
    }

    storesState.editedAdaptiveNames.clear();
    storesState.expandedStoreGroups.clear();
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
    storesState.useAdaptiveNames = useAdaptiveNamesInput.checked;
    storesState.pageSize = Number((changedPageSizeInput ?? pageSizeInputs[0]).value);
    syncPageSizeInputs(storesState.pageSize);
    storesState.editedAdaptiveNames.clear();
    storesState.expandedStoreGroups.clear();
    void loadPage(1);
}

async function loadPage(page: number): Promise<void> {
    if (page <= 0) {
        return;
    }

    try {
        hideAlertMessage(alertElement);

        const result = await getStores(storesState.search, storesState.useAdaptiveNames, page, storesState.pageSize, storesState.sortBy, storesState.sortDirection);
        storesState.stores = result.items;
        storesState.page = result.page;
        storesState.pageSize = result.pageSize;
        storesState.totalCount = result.totalCount;
        storesState.totalPages = result.totalPages;
        storesState.hasPreviousPage = result.hasPreviousPage;
        storesState.hasNextPage = result.hasNextPage;
        syncPageSizeInputs(storesState.pageSize);

        renderStores(ui, storesState, pageNumber => {
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
    if (!(target instanceof HTMLInputElement) || target.dataset.storeAdaptiveNameInput !== "true") {
        return;
    }

    const row = target.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.storeId) {
        throw new Error("Не найдена строка магазина.");
    }

    const store = getStoreById(row.dataset.storeId);
    storesState.editedAdaptiveNames.set(store.id, target.value);
    updateSaveButtonVisibility(row, store);
}

async function handleTableClick(event: MouseEvent): Promise<void> {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const groupToggleButton = target.closest<HTMLButtonElement>("[data-store-group-toggle]");
    if (groupToggleButton) {
        toggleStoreGroup(groupToggleButton);
        return;
    }

    const receiptsButton = target.closest<HTMLButtonElement>("[data-store-receipts-button]");
    if (receiptsButton) {
        await openStoreReceiptsModal(receiptsButton);
        return;
    }

    const saveButton = target.closest<HTMLButtonElement>("[data-store-save-button]");
    if (!saveButton) {
        return;
    }

    const row = saveButton.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.storeId) {
        throw new Error("Не найдена строка магазина.");
    }

    const input = row.querySelector<HTMLInputElement>("[data-store-adaptive-name-input]");
    if (!input) {
        throw new Error("Не найдено поле адаптивного названия.");
    }

    try {
        saveButton.disabled = true;
        hideAlertMessage(alertElement);
        await updateStoreAdaptiveName(row.dataset.storeId, input.value);
        storesState.editedAdaptiveNames.delete(row.dataset.storeId);
        await loadPage(storesState.page);
    }
    catch (error) {
        saveButton.disabled = false;
        showAlertMessage(alertElement, getErrorMessage(error));
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

function toggleStoreGroup(button: HTMLButtonElement): void {
    const row = button.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.storeGroupKey) {
        throw new Error("Не найдена группа магазинов.");
    }

    const storeGroupKey = row.dataset.storeGroupKey;
    button.blur();

    if (storesState.expandedStoreGroups.has(storeGroupKey)) {
        storesState.expandedStoreGroups.delete(storeGroupKey);
        removeStoreGroupChildRows(storeGroupKey);
    } else {
        storesState.expandedStoreGroups.add(storeGroupKey);
        appendStoreGroupChildRows(row, storeGroupKey);
    }

    button.textContent = storesState.expandedStoreGroups.has(storeGroupKey) ? "⌃" : "⌄";
    button.ariaLabel = storesState.expandedStoreGroups.has(storeGroupKey) ? "Свернуть группу магазинов" : "Развернуть группу магазинов";
}

function appendStoreGroupChildRows(parentRow: HTMLTableRowElement, storeGroupKey: string): void {
    const store = getStoreByGroupKey(storeGroupKey);
    let previousRow = parentRow;

    for (const child of store.children) {
        const childRow = createStoreRow(child, storesState, true, storeGroupKey);
        previousRow.after(childRow);
        previousRow = childRow;
    }
}

function removeStoreGroupChildRows(storeGroupKey: string): void {
    const rows = ui.tableBody.querySelectorAll<HTMLTableRowElement>("tr[data-store-parent-group-key]");
    for (const row of rows) {
        if (row.dataset.storeParentGroupKey === storeGroupKey) {
            row.remove();
        }
    }
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

    selectedStoreReceiptsStoreId = storeId;
    selectedStoreReceiptsGroupKey = groupKey;
    selectedStoreReceiptsPage = 1;
    storeReceiptsTitleElement.textContent = "Чеки магазина - " + (store.displayName || store.name || "Магазин");
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

    try {
        hideStoreReceiptsAlert();
        const result = await getStoreReceipts(selectedStoreReceiptsStoreId, selectedStoreReceiptsGroupKey, page, STORE_RECEIPTS_PAGE_SIZE);
        selectedStoreReceipts = result.items;
        selectedStoreReceiptsPage = result.page;
        renderStoreReceipts(result.items);
        renderStoreReceiptsPagination(result.page, result.totalPages, result.hasPreviousPage, result.hasNextPage);
    }
    catch (error) {
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

function renderStoreReceiptsPagination(page: number, totalPages: number, hasPreviousPage: boolean, hasNextPage: boolean): void {
    clearElement(storeReceiptsPaginationElement);

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

async function loadAvailableAccounts(): Promise<void> {
    availableAccounts = (await loadStoreAvailableAccounts()).map(normalizeAvailableAccount);
}

async function openMoveReceiptAccountModal(receiptId: string, sourceAccountId: string): Promise<void> {
    if (availableAccounts.length === 0) {
        await loadAvailableAccounts();
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

    const selectedOption = getSelectedMoveReceiptTargetOption();
    if (!selectedOption || selectedOption.disabled) {
        updateMoveReceiptActionState();
        return;
    }

    const originalText = confirmMoveReceiptAccountButton.textContent;
    confirmMoveReceiptAccountButton.disabled = true;
    confirmMoveReceiptAccountButton.textContent = "Перенос...";
    removeReceiptFromAccountButton.disabled = true;

    try {
        await moveStoreReceiptToAccount(pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId, selectedOption.value);
        replaceReceiptAccountLink(selectedStoreReceipts, availableAccounts, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId, selectedOption.value);
        closeMoveReceiptAccountModal();
        await loadSelectedStoreReceiptsPage(selectedStoreReceiptsPage);
    }
    catch (error) {
        showMoveReceiptAccountAlert(getErrorMessage(error));
    }
    finally {
        confirmMoveReceiptAccountButton.textContent = originalText;
        updateMoveReceiptActionState();
    }
}

function openRemoveReceiptFromAccountConfirmation(): void {
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

    const originalText = confirmRemoveReceiptAccountButton.textContent;
    confirmRemoveReceiptAccountButton.disabled = true;
    confirmRemoveReceiptAccountButton.textContent = "Удаление...";

    try {
        shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = false;
        preserveMoveReceiptAccountModalStateOnHide = false;
        await removeStoreReceiptFromAccount(pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
        selectedStoreReceipts = removeReceiptAccountLink(selectedStoreReceipts, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
        closeMoveReceiptAccountModal();
        removeReceiptAccountModal.hide();
        await loadSelectedStoreReceiptsPage(selectedStoreReceiptsPage);
    }
    catch (error) {
        showStoreReceiptsAlert(getErrorMessage(error));
    }
    finally {
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
    try {
        const receipt = await openStoreReceipt(receiptId);
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
        showStoreReceiptsAlert(getErrorMessage(error));
    }
}

async function refreshReceipt(receiptId: string, card: HTMLElement, button: HTMLButtonElement): Promise<void> {
    const originalText = button.textContent;
    button.disabled = true;
    button.textContent = "Обновление...";

    try {
        const receipt = await refreshStoreReceipt(receiptId);
        updateCardFromDto(card, receipt, formatCurrency);
        replaceSelectedReceipt(receipt);
    }
    catch (error) {
        showStoreReceiptsAlert(getErrorMessage(error));
    }
    finally {
        button.disabled = false;
        button.textContent = originalText;
    }
}

function replaceSelectedReceipt(receipt: ReceiptDto): void {
    selectedStoreReceipts = selectedStoreReceipts.map(item => item.id === receipt.id ? receipt : item);
}

function clearSelectedStoreReceipts(): void {
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
