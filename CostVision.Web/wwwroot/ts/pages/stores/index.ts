import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { requireElementById, requireInputById } from "../../shared/dom.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { getStores, updateStoreAdaptiveName } from "./api.js";
import { createStoreRow, renderStores, updateSaveButtonVisibility, type StoresUi } from "./render.js";
import { storesState } from "./state.js";
import type { StoreListItem, StoreSortBy } from "./types.js";

let searchInput: HTMLInputElement;
let useAdaptiveNamesInput: HTMLInputElement;
let pageSizeInputs: HTMLSelectElement[];
let alertElement: HTMLElement;
let ui: StoresUi;

document.addEventListener("DOMContentLoaded", () => {
    initStoresPage();
});

function initStoresPage(): void {
    searchInput = requireInputById("storesSearch");
    useAdaptiveNamesInput = requireInputById("storesUseAdaptiveNames");
    pageSizeInputs = [
        requireElementById<HTMLSelectElement>("storesTopPageSize")
    ];
    alertElement = requireElementById<HTMLElement>("storesAlert");
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

    void loadPage(1);
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
    if (target instanceof HTMLButtonElement && target.dataset.storeGroupToggle === "true") {
        toggleStoreGroup(target);
        return;
    }

    if (!(target instanceof HTMLButtonElement) || target.dataset.storeSaveButton !== "true") {
        return;
    }

    const row = target.closest("tr");
    if (!(row instanceof HTMLTableRowElement) || !row.dataset.storeId) {
        throw new Error("Не найдена строка магазина.");
    }

    const input = row.querySelector<HTMLInputElement>("[data-store-adaptive-name-input]");
    if (!input) {
        throw new Error("Не найдено поле адаптивного названия.");
    }

    try {
        target.disabled = true;
        hideAlertMessage(alertElement);
        await updateStoreAdaptiveName(row.dataset.storeId, input.value);
        storesState.editedAdaptiveNames.delete(row.dataset.storeId);
        await loadPage(storesState.page);
    }
    catch (error) {
        target.disabled = false;
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

function getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : "Не удалось выполнить действие.";
}
