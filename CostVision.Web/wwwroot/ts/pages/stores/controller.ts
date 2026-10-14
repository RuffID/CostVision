import { getPageFromQuery, updatePageQuery, getCookie, setCookie } from "../../shared/preferences.js";
import { isIntentionalAbort } from "../../shared/requestCoordinator.js";
import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { createBootstrapModal } from "../../shared/bootstrap.js";
import { requireElementById, requireInputById } from "../../shared/dom.js";
import { formatMoneyRub, formatRuNumber } from "../../shared/formatters.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { createTableLoadingIndicator } from "../../shared/tableLoadingIndicator.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";

import { initReceiptMoneyMovementsModal } from "../reports/receipts/modals/receiptMoneyMovementsModal.js";
import { normalizeAvailableAccount } from "../reports/receipts/state/accountModel.js";

import { getStores, loadStoreAvailableAccounts } from "./api.js";
import { renderStores } from "./render.js";
import { formatReportTotals } from "../reports/receipts/render.js";
import { createStoresSession } from "./state.js";
import type { StoreListItem, StoreSortBy } from "./types.js";
import { StoresBindings } from "./bindings.js";
import { createStoresPreferences } from "./preferences.js";
import { createStoreNamingController } from "./namingController.js";
import { createStoreReceiptsController } from "./storeReceiptsController.js";
import { createStoreReceiptAccountActionsController } from "./accountActionsController.js";

export function createStoresPage() {
    const session = createStoresSession();
    const bindings = new StoresBindings();
    window.addEventListener("pagehide", () => {
        session.listRequests.invalidate();
        session.accountRequests.invalidate();
        session.namingContext.invalidate();
        session.storeReceiptRequests.invalidate();
        session.storeContext.invalidate();
        session.detailRequests.invalidate();
        session.accountActionContext.invalidate();
        session.confirmationContext.invalidate();
    });
    const preferences = createStoresPreferences({ session, bindings,
        getCookie: (...args) => getCookie(...args),
        setCookie: (...args) => setCookie(...args),
    });
    const naming = createStoreNamingController({ session, bindings,
        loadPage: (...args) => loadPage(...args),
    });
    const storeReceipts = createStoreReceiptsController({ session, bindings,
        getStoreByGroupKey: (...args) => getStoreByGroupKey(...args),
        getStoreById: (...args) => getStoreById(...args),
        formatCurrency: (...args) => formatCurrency(...args),
        getErrorMessage: (...args) => getErrorMessage(...args),
        openMoveReceiptAccountModal: (...args) => accountActions.openMoveReceiptAccountModal(...args),
        formatNumber: (...args) => formatNumber(...args),
    });
    const accountActions = createStoreReceiptAccountActionsController({ session, bindings,
        loadAvailableAccounts: (...args) => loadAvailableAccounts(...args),
        showStoreReceiptsAlert: (...args) => storeReceipts.showStoreReceiptsAlert(...args),
        loadSelectedStoreReceiptsPage: (...args) => storeReceipts.loadSelectedStoreReceiptsPage(...args),
        formatCurrency: (...args) => formatCurrency(...args),
    });

    async function initStoresPage(): Promise<void> {
        bindings.searchInput = requireInputById("storesSearch");
        bindings.showOriginalNamesInput = requireInputById("storesShowOriginalNames");
        bindings.groupByNameInput = requireInputById("storesGroupByName");
        bindings.pageSizeInputs = [
            requireElementById<HTMLSelectElement>("storesTopPageSize", HTMLSelectElement)
        ];
        bindings.alertElement = requireElementById<HTMLElement>("storesAlert");
        bindings.accountFilterSelect = requireElementById<HTMLSelectElement>("storesAccountFilter", HTMLSelectElement);
        bindings.storesCountElement = requireElementById<HTMLElement>("storesCount");
        bindings.storesSumElement = requireElementById<HTMLElement>("storesSum");
        bindings.tableLoadingIndicator = createTableLoadingIndicator(
            requireElementById<HTMLElement>("storesTableContainer"),
            "Идёт загрузка магазинов...",
            480
        );
        bindings.storeReceiptsModalElement = requireElementById<HTMLElement>("storeReceiptsModal");
        bindings.storeReceiptsModal = createBootstrapModal(bindings.storeReceiptsModalElement);
        bindings.storeReceiptsTitleElement = requireElementById<HTMLElement>("storeReceiptsModalLabel");
        bindings.storeReceiptsAlertElement = requireElementById<HTMLElement>("storeReceiptsAlert");
        bindings.storeReceiptsListElement = requireElementById<HTMLElement>("storeReceiptsList");
        bindings.storeReceiptsPaginationElement = requireElementById<HTMLElement>("storeReceiptsPagination");
        bindings.storeReceiptsPageSizeSelect = requireElementById<HTMLSelectElement>("storeReceiptsPageSize", HTMLSelectElement);
        bindings.storeReceiptsPageSizeSelect.addEventListener("change", () => void storeReceipts.loadSelectedStoreReceiptsPage(1));
        bindings.receiptDetailsModal = createBootstrapModal(requireElementById<HTMLElement>("receiptDetailsModal"));
        requireElementById<HTMLElement>("receiptDetailsModal").addEventListener("hide.bs.modal", () => session.detailRequests.invalidate());
        bindings.receiptDetailsListElement = requireElementById<HTMLElement>("receipt-details-list");
        bindings.receiptDetailsHeaderElement = requireElementById<HTMLElement>("receipt-details-header");
        bindings.receiptDetailsTotalElement = requireElementById<HTMLElement>("receipt-details-total");
        bindings.moveReceiptAccountModalElement = requireElementById<HTMLElement>("moveReceiptAccountModal");
        bindings.moveReceiptAccountModal = createBootstrapModal(bindings.moveReceiptAccountModalElement);
        bindings.moveReceiptSourceAccountElement = requireElementById<HTMLElement>("moveReceiptSourceAccount");
        bindings.moveReceiptTargetAccountSelect = requireElementById<HTMLSelectElement>("moveReceiptTargetAccount", HTMLSelectElement);
        bindings.moveReceiptAccountAlertElement = requireElementById<HTMLElement>("moveReceiptAccountAlert");
        bindings.confirmMoveReceiptAccountButton = requireElementById<HTMLButtonElement>("confirmMoveReceiptAccountBtn", HTMLButtonElement);
        bindings.removeReceiptFromAccountButton = requireElementById<HTMLButtonElement>("removeReceiptFromAccountBtn", HTMLButtonElement);
        bindings.removeReceiptAccountModal = createBootstrapModal(requireElementById<HTMLElement>("removeReceiptAccountModal"));
        bindings.removeReceiptAccountModalTitleElement = requireElementById<HTMLElement>("removeReceiptAccountModalTitle");
        bindings.removeReceiptAccountModalMessageElement = requireElementById<HTMLElement>("removeReceiptAccountModalMessage");
        bindings.removeReceiptAccountModalReceiptInfoElement = requireElementById<HTMLElement>("removeReceiptAccountModalReceiptInfo");
        bindings.confirmRemoveReceiptAccountButton = requireElementById<HTMLButtonElement>("confirmRemoveReceiptAccountBtn", HTMLButtonElement);
        bindings.ui = {
            tableBody: requireElementById<HTMLTableSectionElement>("storesTableBody", HTMLTableSectionElement),
            pageInfoElements: Array.from(document.querySelectorAll<HTMLElement>("[data-stores-page-info]")),
            paginationElements: Array.from(document.querySelectorAll<HTMLElement>("[data-stores-pagination]")),
            nameSortButton: requireElementById<HTMLButtonElement>("storesNameSortButton", HTMLButtonElement),
            receiptCountSortButton: requireElementById<HTMLButtonElement>("storesReceiptCountSortButton", HTMLButtonElement),
            totalSpentSortButton: requireElementById<HTMLButtonElement>("storesTotalSpentSortButton", HTMLButtonElement)
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

        bindings.adaptiveNameModal = createBootstrapModal(requireElementById<HTMLElement>("storeAdaptiveNameModal"));
        requireElementById<HTMLElement>("storeAdaptiveNameModal").addEventListener("hide.bs.modal", () => {
            session.namingContext.invalidate();
            session.editedStoreId = null;
        });
        bindings.originalNameInput = requireInputById("storeOriginalName");
        bindings.adaptiveNameInput = requireInputById("storeAdaptiveName");
        bindings.clearAdaptiveNameButton = requireElementById<HTMLButtonElement>("storeClearAdaptiveName", HTMLButtonElement);
        bindings.saveAdaptiveNameButton = requireElementById<HTMLButtonElement>("storeSaveAdaptiveName", HTMLButtonElement);

        requireElementById<HTMLButtonElement>("storesApplyFilter", HTMLButtonElement).addEventListener("click", () => reloadFromFirstPage());
        bindings.searchInput.addEventListener("keydown", handleSearchKeyDown);
        bindings.searchInput.addEventListener("input", () => { session.listRequests.invalidate(); bindings.tableLoadingIndicator.hide(); });
        bindings.showOriginalNamesInput.addEventListener("change", handleShowOriginalNamesChange);
        bindings.groupByNameInput.addEventListener("change", () => reloadFromFirstPage());
        bindings.accountFilterSelect.addEventListener("change", () => reloadFromFirstPage());
        bindings.ui.nameSortButton.addEventListener("click", () => handleSortClick("name"));
        bindings.ui.receiptCountSortButton.addEventListener("click", () => handleSortClick("receiptCount"));
        bindings.ui.totalSpentSortButton.addEventListener("click", () => handleSortClick("totalSpent"));
        for (const pageSizeInput of bindings.pageSizeInputs) {
            pageSizeInput.addEventListener("change", () => reloadFromFirstPage(pageSizeInput));
        }
        bindings.ui.tableBody.addEventListener("click", event => {
            void handleTableClick(event);
        });
        requireElementById<HTMLFormElement>("storeAdaptiveNameForm", HTMLFormElement).addEventListener("submit", event => {
            event.preventDefault();
            void naming.saveAdaptiveName();
        });
        bindings.clearAdaptiveNameButton.addEventListener("click", () => {
            bindings.adaptiveNameInput.value = "";
            bindings.adaptiveNameInput.focus();
        });
        bindings.storeReceiptsListElement.addEventListener("click", event => {
            void storeReceipts.handleStoreReceiptClick(event);
        });
        bindings.storeReceiptsModalElement.addEventListener("hide.bs.modal", storeReceipts.clearSelectedStoreReceipts);
        bindings.moveReceiptAccountModalElement.addEventListener("hide.bs.modal", () => {
            if (session.preserveMoveReceiptAccountModalStateOnHide) {
                return;
            }

            accountActions.clearMoveReceiptAccountState();
        });
        const removeModalElement = requireElementById<HTMLElement>("removeReceiptAccountModal");
        removeModalElement.addEventListener("hide.bs.modal", () => {
            const isCurrent = session.confirmationContext.capture();
            const matchesStore = session.storeContext.capture();
            const action = session.pendingReceiptAccountAction;
            const restore = session.shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation;
            if (!restore) { session.pendingReceiptAccountAction = null; }
            removeModalElement.addEventListener("hidden.bs.modal", () => {
                if (!isCurrent() || !matchesStore() || !restore || session.pendingReceiptAccountAction !== action) { return; }
                session.shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = false;
                session.preserveMoveReceiptAccountModalStateOnHide = false;
                bindings.moveReceiptAccountModal.show();
            }, { once: true });
        });
        bindings.confirmMoveReceiptAccountButton.addEventListener("click", () => {
            void accountActions.onConfirmMoveReceiptToAccount();
        });
        bindings.removeReceiptFromAccountButton.addEventListener("click", accountActions.openRemoveReceiptFromAccountConfirmation);
        bindings.confirmRemoveReceiptAccountButton.addEventListener("click", () => {
            void accountActions.onConfirmRemoveReceiptFromAccount();
        });
        bindings.receiptMoneyMovementsModal = initReceiptMoneyMovementsModal({
            getReceipts: () => session.selectedStoreReceipts,
            getForgeryToken: () => getRequestVerificationToken(),
            reloadReceiptList: () => storeReceipts.loadSelectedStoreReceiptsPage(session.selectedStoreReceiptsPage),
            formatCurrency: formatCurrency
        });

        preferences.restorePreferences();
        preferences.syncPageSizeInputs(session.data.pageSize);
        if (!await loadAvailableAccounts()) { return; }
        await loadPage(getPageFromQuery());
    }

    function handleSortClick(sortBy: StoreSortBy): void {
        if (session.data.sortBy === sortBy) {
            session.data.sortDirection = session.data.sortDirection === "asc" ? "desc" : "asc";
        } else {
            session.data.sortBy = sortBy;
            session.data.sortDirection = "asc";
        }

        preferences.savePreferences();
        void loadPage(1);
    }

    function handleSearchKeyDown(event: KeyboardEvent): void {
        if (event.key === "Enter") {
            event.preventDefault();
            reloadFromFirstPage();
        }
    }

    function reloadFromFirstPage(changedPageSizeInput: HTMLSelectElement | null = null): void {
        session.data.search = bindings.searchInput.value.trim();
        session.data.groupByName = bindings.groupByNameInput.checked;
        session.data.pageSize = Number((changedPageSizeInput ?? bindings.pageSizeInputs[0]).value);
        preferences.syncPageSizeInputs(session.data.pageSize);
        preferences.savePreferences();
        void loadPage(1);
    }

    async function loadPage(page: number): Promise<void> {
        if (page <= 0) {
            return;
        }

        const request = session.listRequests.begin({ search: session.data.search, page, pageSize: session.data.pageSize,
            sortBy: session.data.sortBy, sortDirection: session.data.sortDirection, accountId: bindings.accountFilterSelect.value, groupByName: session.data.groupByName });
        const p = request.parameters;
        updatePageQuery(page);
        bindings.tableLoadingIndicator.show();

        try {
            hideAlertMessage(bindings.alertElement);

            const result = await getStores(p.search, p.groupByName, p.page, p.pageSize, p.sortBy, p.sortDirection, p.accountId, request.signal);
            if (!request.isCurrent()) {
                return;
            }

            session.data.stores = result.items;
            session.data.page = result.page;
            session.data.pageSize = result.pageSize;
            session.data.totalCount = result.totalCount;
            session.data.totalPages = result.totalPages;
            session.data.hasPreviousPage = result.hasPreviousPage;
            session.data.hasNextPage = result.hasNextPage;
            bindings.storesCountElement.textContent = `Магазинов: ${result.totalCount}`;
            bindings.storesSumElement.textContent = formatReportTotals(result.totalSum, result.incomeSum, formatCurrency);
            preferences.syncPageSizeInputs(session.data.pageSize);
            updatePageQuery(session.data.page);

            renderStores(bindings.ui, session.data, pageNumber => {
                void loadPage(pageNumber);
            });
            bindings.tableLoadingIndicator.hide();
        }
        catch (error) {
            if (!request.isCurrent() || isIntentionalAbort(error)) {
                return;
            }

            bindings.tableLoadingIndicator.hide();
            showAlertMessage(bindings.alertElement, getErrorMessage(error));
        }
    }

    

    

    

    

    async function handleTableClick(event: MouseEvent): Promise<void> {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const receiptsButton = target.closest<HTMLButtonElement>("[data-store-receipts-button]");
        if (receiptsButton) {
            await storeReceipts.openStoreReceiptsModal(receiptsButton);
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

        naming.openAdaptiveNameModal(getStoreById(row.dataset.storeId));
    }

    function handleShowOriginalNamesChange(): void {
        session.data.showOriginalNames = bindings.showOriginalNamesInput.checked;
        preferences.savePreferences();
        renderStores(bindings.ui, session.data, pageNumber => {
            void loadPage(pageNumber);
        });
    }

    function getStoreById(storeId: string): StoreListItem {
        const store = session.data.stores
            .flatMap(item => [item, ...item.children])
            .find(item => item.id === storeId);
        if (!store) {
            throw new Error("Магазин не найден в состоянии страницы.");
        }

        return store;
    }

    function getStoreByGroupKey(storeGroupKey: string): StoreListItem {
        const store = session.data.stores.find(item => item.groupKey === storeGroupKey);
        if (!store) {
            throw new Error("Группа магазинов не найдена в состоянии страницы.");
        }

        return store;
    }

    async function loadAvailableAccounts(): Promise<boolean> {
        const request = session.accountRequests.begin(null);
        try {
            const accounts = await loadStoreAvailableAccounts(request.signal);
            if (!request.isCurrent()) { return false; }
            session.availableAccounts = accounts.map(normalizeAvailableAccount);
            bindings.accountFilterSelect.replaceChildren();
            const allAccountsOption = document.createElement("option");
            allAccountsOption.value = "";
            allAccountsOption.textContent = "Все счета";
            bindings.accountFilterSelect.append(allAccountsOption);
            for (const account of session.availableAccounts) {
                const option = document.createElement("option");
                option.value = account.id;
                option.textContent = account.name;
                bindings.accountFilterSelect.append(option);
            }
            bindings.accountFilterSelect.disabled = false;
            return true;
        } catch (error) {
            if (request.isCurrent() && !isIntentionalAbort(error)) {
                showAlertMessage(bindings.alertElement, error instanceof Error ? error.message : "Не удалось загрузить счета.");
            }
            return false;
        }
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

    async function init(): Promise<void> {
        try {
            await initStoresPage();
        } catch (error) {
            console.error(error);
            if (bindings.alertElement) {
                showAlertMessage(bindings.alertElement, getErrorMessage(error));
            }
        }
    }

    return { init };
}
