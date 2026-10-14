import { isIntentionalAbort } from "../../../shared/requestCoordinator.js";

import { clearElement } from "../../../shared/dom.js";

import { loadAvailableAccountsApi, loadReceiptsApi } from "./api.js";

import { updateReceiptsSummary, renderReceiptCards, createReceiptLoadingIndicator, renderReceiptPagination } from "./render.js";

import { hideReceiptAccountFilterError as hideAccountFilterError, renderReceiptAccountFilterError as renderAccountFilterError, renderReceiptAccountFilterLoading as renderAccountFilterLoading, renderReceiptAccountFilterOptions as renderAccountFilterOptions } from "./ui/accountFilter.js";

import { normalizeAvailableAccount as normalizeAvailableAccountCore } from "./state/accountModel.js";

import type { ReceiptsSession } from "./state.js";
import type { ReceiptsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<ReceiptsSession,
        "accountRequests" | "currentReceiptList" | "detailRequests" | "forgeryToken" | "listRequests" | "pageState">;
    bindings: Pick<ReceiptsBindings,
        "listContainer" | "receiptAccountFilterError" | "receiptAccountFilterSelect" | "receiptsCountElement" | "receiptsPaginationElement" | "receiptsSumElement">;
    buildDateRangeQuery: (page: number) => string;
    formatCurrency: (value: number) => string;
    updatePageQuery: (page: number) => void;
    getErrorMessage: (error: unknown, fallback?: string) => string;
}

export function createReceiptListController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    async function loadReceiptList(page = 1): Promise<void> {
        const request = session.listRequests.begin(dependencies.buildDateRangeQuery(page));
        session.detailRequests.invalidate();
        const matchesQuery = () => request.parameters === dependencies.buildDateRangeQuery(page);
        session.currentReceiptList = null;
        session.pageState.receipts = [];
        updateReceiptsSummary(bindings.receiptsCountElement, bindings.receiptsSumElement, null, dependencies.formatCurrency);
        clearElement(bindings.receiptsPaginationElement);
        try {
            bindings.listContainer.replaceChildren(createReceiptLoadingIndicator());
            const result = await loadReceiptsApi(request.parameters, session.forgeryToken, request.signal);
            if (!request.isCurrent() || !matchesQuery()) { return; }
            session.currentReceiptList = result;
            session.pageState.receipts = session.currentReceiptList.items;
            renderReceiptCards(bindings.listContainer, session.currentReceiptList.items, dependencies.formatCurrency);
            updateReceiptsSummary(bindings.receiptsCountElement, bindings.receiptsSumElement, session.currentReceiptList, dependencies.formatCurrency);
            renderReceiptPagination(bindings.receiptsPaginationElement, session.currentReceiptList, page => { void loadReceiptList(page); });
            dependencies.updatePageQuery(session.currentReceiptList.page);
        } catch (error) {
            if (!request.isCurrent() || !matchesQuery() || isIntentionalAbort(error)) { return; }
            bindings.listContainer.replaceChildren();
            alert(dependencies.getErrorMessage(error));
        }
    }

    async function loadAvailableAccountsAsync(): Promise<boolean> {
        const request = session.accountRequests.begin(session.forgeryToken);
        if (!bindings.receiptAccountFilterSelect) {
            throw new Error('Не найден фильтр счетов.');
        }

        bindings.receiptAccountFilterSelect.disabled = true;
        renderAccountFilterLoading(bindings.receiptAccountFilterSelect);

        try {
            const accounts = await loadAvailableAccountsApi(request.parameters, request.signal);
            if (!request.isCurrent()) { return false; }
            session.pageState.availableAccounts = accounts.map(normalizeAvailableAccountCore);

            renderAccountFilterOptions(bindings.receiptAccountFilterSelect, session.pageState.availableAccounts);
            hideAccountFilterError(bindings.receiptAccountFilterError);
            bindings.receiptAccountFilterSelect.disabled = false;
            return true;
        } catch (error) {
            if (!request.isCurrent() || isIntentionalAbort(error)) { return false; }
            renderAccountFilterError({ select: bindings.receiptAccountFilterSelect, error: bindings.receiptAccountFilterError }, error instanceof Error ? error : new Error("Не удалось загрузить список счетов."));
            return false;
        }
    }

    return { loadReceiptList, loadAvailableAccountsAsync };
}
