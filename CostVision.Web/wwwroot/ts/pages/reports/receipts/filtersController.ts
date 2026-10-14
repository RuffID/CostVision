

import { applyReceiptFilters as applyReceiptFiltersCore } from "./filters.js";

import { formatDateForQuery, getDateRangeByPeriodPreset } from "./dateRange.js";
import { ReceiptDto } from "./types.js";

import type { ReceiptsSession } from "./state.js";
import type { ReceiptsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<ReceiptsSession,
        "detailRequests" | "listRequests" | "searchDebounceTimerId">;
    bindings: Pick<ReceiptsBindings,
        "dateFromInput" | "dateToInput" | "receiptAccountFilterSelect" | "receiptOperationFilterSelect" | "receiptPageSizeSelect" | "receiptPeriodPresetSelect" | "receiptSearchInput" | "receiptSearchModeSelect">;
    loadReceiptList: (page?: number) => Promise<void>;
}

export function createReceiptFiltersController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function onReceiptPeriodPresetChanged(): void {
        if (bindings.receiptPeriodPresetSelect.value === 'other') return;
        applySelectedReceiptPeriodPreset();
        void dependencies.loadReceiptList(1);
    }

    function applySelectedReceiptPeriodPreset(): void {
        const periodPreset = bindings.receiptPeriodPresetSelect.value || 'currentMonth';
        const range = getDateRangeByPeriodPreset(periodPreset, new Date());

        setReceiptDateRange(range.dateFrom, range.dateTo);
    }

    function setCurrentMonthPeriod(): void {
        bindings.receiptPeriodPresetSelect.value = 'currentMonth';

        applySelectedReceiptPeriodPreset();
    }

    function setReceiptDateRange(dateFrom: Date, dateTo: Date): void {
        bindings.dateFromInput.value = formatDateForQuery(dateFrom);
        bindings.dateToInput.value = formatDateForQuery(dateTo);
    }

    function buildDateRangeQuery(page: number): string {
        const fromVal = bindings.dateFromInput.value;
        const toVal = bindings.dateToInput.value;

        const parts = [];

        if (fromVal) {
            parts.push('dateFrom=' + encodeURIComponent(fromVal));
        }
        if (toVal) {
            parts.push('dateTo=' + encodeURIComponent(toVal));
        }

        parts.push('page=' + page, 'pageSize=' + bindings.receiptPageSizeSelect.value, 'search=' + encodeURIComponent(getSearchQuery()), 'searchMode=' + encodeURIComponent(getSearchMode()), 'operationFilter=' + encodeURIComponent(getReceiptOperationFilter()));
        const accountId = getSelectedReceiptAccountId();
        if (accountId) parts.push('accountId=' + encodeURIComponent(accountId));
        return parts.join('&');
    }

    function onSearchChanged(): void {
        session.listRequests.invalidate();
        session.detailRequests.invalidate();
        if (session.searchDebounceTimerId) {
            clearTimeout(session.searchDebounceTimerId);
        }

        session.searchDebounceTimerId = setTimeout(function () {
            void dependencies.loadReceiptList(1);
        }, 250);
    }

    function updatePeriodPresetForManualDateRange(): void {
        session.listRequests.invalidate();
        session.detailRequests.invalidate();
        const preset = bindings.receiptPeriodPresetSelect.value;
        if (preset === 'other') return;

        const range = getDateRangeByPeriodPreset(preset, new Date());
        if (bindings.dateFromInput.value !== formatDateForQuery(range.dateFrom) || bindings.dateToInput.value !== formatDateForQuery(range.dateTo)) {
            bindings.receiptPeriodPresetSelect.value = 'other';
        }
    }

    function applyReceiptFilters(list: ReceiptDto[]): ReceiptDto[] {
        const query = getSearchQuery();
        const mode = getSearchMode();
        const accountId = getSelectedReceiptAccountId();
        const operationFilter = getReceiptOperationFilter();
        return applyReceiptFiltersCore(list, query, mode, accountId, operationFilter);
    }

    function getSearchQuery(): string {
        if (!bindings.receiptSearchInput) return '';
        const v = (bindings.receiptSearchInput.value || '').trim();
        return v;
    }

    function getSearchMode(): string {
        if (!bindings.receiptSearchModeSelect) return 'all';
        return bindings.receiptSearchModeSelect.value || 'all';
    }

    function getSelectedReceiptAccountId(): string {
        if (!bindings.receiptAccountFilterSelect) return '';
        return bindings.receiptAccountFilterSelect.value || '';
    }

    function getReceiptOperationFilter(): string {
        if (!bindings.receiptOperationFilterSelect) return 'all';
        return bindings.receiptOperationFilterSelect.value || 'all';
    }

    return { onSearchChanged, onReceiptPeriodPresetChanged, updatePeriodPresetForManualDateRange, setCurrentMonthPeriod, buildDateRangeQuery };
}
