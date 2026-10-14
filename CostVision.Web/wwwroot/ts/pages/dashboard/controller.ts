import { isIntentionalAbort } from "../../shared/requestCoordinator.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { getDateRangeByPeriodPreset, formatDateForQuery } from "../../shared/dateRange.js";
import { loadDashboardAccountsAsync, loadIncomeExpenseReportAsync } from "./api.js";
import { createDashboardState, ACCOUNT_FILTER_WITHOUT_ACCOUNT } from "./state.js";
import { getDashboardUi, type DashboardUi } from "./ui.js";
import { createDashboardRenderer } from "./render.js";

export function createDashboardPage() {
    const state = createDashboardState();
    let ui: DashboardUi;
    let view: ReturnType<typeof createDashboardRenderer>;
    window.addEventListener("pagehide", () => { state.reportRequests.invalidate(); state.accountRequests.invalidate(); });

    async function initDashboardPage(): Promise<void> {
        state.antiForgeryToken = getRequestVerificationToken();
        ui = getDashboardUi();
        view = createDashboardRenderer(ui, state);
        setDefaultFilters();
        bindEvents();
        view.renderExpenseSourceHelp();
        if (!await loadAndRenderAccountsAsync()) { return; }
        await loadAndRenderReportAsync();
    }

    function bindEvents(): void {
        ui.periodPresetSelect.addEventListener("change", onPeriodPresetChanged);
        ui.periodSelect.addEventListener("change", onFilterChanged);
        ui.expenseSourceSelect.addEventListener("change", onExpenseSourceChanged);
        ui.dateFromInput.addEventListener("change", onFilterChanged);
        ui.dateToInput.addEventListener("change", onFilterChanged);
        ui.accountFilterList.addEventListener("change", onAccountFilterChanged);
        ui.storeSearchInput.addEventListener("input", onStoreSearchInput);
        ui.storeFilterList.addEventListener("change", onStoreFilterChanged);
        ui.storeFilterButton.addEventListener("shown.bs.dropdown", onStoreFilterDropdownShown);
    }

    function setDefaultFilters(): void {
        ui.periodPresetSelect.value = "currentYear";
        ui.periodSelect.value = "month";
        ui.expenseSourceSelect.value = "receiptsAndMoneyMovements";
        applySelectedPeriodPreset();
    }

    function onPeriodPresetChanged(): void {
        applySelectedPeriodPreset();
        loadAndRenderReportAsync();
    }

    function onExpenseSourceChanged(): void {
        if (ui.expenseSourceSelect.value === "moneyMovements") {
            state.selectedStores.clear();
            ui.storeSearchInput.value = "";
            state.storeSearchQuery = "";
        }

        view.updateStoreFilterState();
        view.updateStoreFilterButtonText();
        loadAndRenderReportAsync();
    }

    function applySelectedPeriodPreset(): void {
        const range = getDateRangeByPeriodPreset(ui.periodPresetSelect.value || "currentYear", new Date());
        ui.dateFromInput.value = formatDateForQuery(range.dateFrom);
        ui.dateToInput.value = formatDateForQuery(range.dateTo);
    }

    async function onFilterChanged(): Promise<void> {
        await loadAndRenderReportAsync();
    }

    async function loadAndRenderReportAsync(): Promise<void> {
        const request = state.reportRequests.begin({ dateFrom: ui.dateFromInput.value, dateTo: ui.dateToInput.value,
            period: ui.periodSelect.value, source: ui.expenseSourceSelect.value, accounts: getSelectedRealAccountIds(),
            withoutAccount: state.selectedAccountIds.has(ACCOUNT_FILTER_WITHOUT_ACCOUNT), stores: Array.from(state.selectedStores) });
        const parameters = request.parameters;
        view.hideError();

        try {
            const report = await loadIncomeExpenseReportAsync(
                parameters.dateFrom, parameters.dateTo, parameters.period, parameters.source,
                parameters.accounts, parameters.withoutAccount, parameters.stores, state.antiForgeryToken, request.signal
            );

            if (!request.isCurrent()) { return; }
            state.reportStores = report.stores;
            removeUnavailableSelectedStores();
            view.renderStoreFilter();
            view.updateStoreFilterButtonText();
            view.updateStoreFilterState();
            view.renderSummary(report);
            view.renderChart(report);
        }
        catch (error) {
            if (!request.isCurrent() || isIntentionalAbort(error)) { return; }
            view.showError(error instanceof Error ? error.message : "Не удалось загрузить отчёт.");
        }
    }

    async function loadAndRenderAccountsAsync(): Promise<boolean> {
        const request = state.accountRequests.begin(state.antiForgeryToken);
        try {
            const accounts = await loadDashboardAccountsAsync(request.parameters, request.signal);
            if (!request.isCurrent()) { return false; }
            state.dashboardAccounts = accounts;
            view.renderAccountFilter();
            view.updateAccountFilterButtonText();
            return true;
        }
        catch (error) {
            if (!request.isCurrent() || isIntentionalAbort(error)) { return false; }
            view.showError(error instanceof Error ? error.message : "Не удалось загрузить счета.");
            return false;
        }
    }

    function onAccountFilterChanged(event: Event): void {
        const target = event.target;

        if (!(target instanceof HTMLInputElement) || target.type !== "checkbox") {
            return;
        }

        if (target.checked) {
            state.selectedAccountIds.add(target.value);
        } else {
            state.selectedAccountIds.delete(target.value);
        }

        state.selectedStores.clear();
        ui.storeSearchInput.value = "";
        state.storeSearchQuery = "";
        view.updateAccountFilterButtonText();
        loadAndRenderReportAsync();
    }

    function onStoreFilterChanged(event: Event): void {
        const target = event.target;

        if (!(target instanceof HTMLInputElement) || target.type !== "checkbox") {
            return;
        }

        if (target.checked) {
            state.selectedStores.add(target.value);
        } else {
            state.selectedStores.delete(target.value);
        }

        state.shouldShowSelectedStoresFirst = false;
        view.updateStoreFilterButtonText();
        loadAndRenderReportAsync();
    }

    function onStoreSearchInput(): void {
        state.storeSearchQuery = ui.storeSearchInput.value;
        view.renderStoreFilter();
    }

    function removeUnavailableSelectedStores(): void {
        state.selectedStores = new Set(Array.from(state.selectedStores).filter(function (store) {
            return state.reportStores.includes(store);
        }));
    }

    function getSelectedRealAccountIds(): string[] {
        return Array.from(state.selectedAccountIds).filter(accountId => accountId !== ACCOUNT_FILTER_WITHOUT_ACCOUNT);
    }

    function onStoreFilterDropdownShown(): void {
        state.shouldShowSelectedStoresFirst = true;
        view.renderStoreFilter();
        ui.storeFilterList.scrollTop = 0;
    }

    return { init: initDashboardPage };
}
