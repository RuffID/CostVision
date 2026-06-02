import { requireElementById, requireInputById, requireSelectById, setElementVisible } from "../../shared/dom.js";
import { formatMoneyRub } from "../../shared/formatters.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { loadDashboardAccountsAsync, loadIncomeExpenseReportAsync } from "./api.js";
import { DashboardAccountDto, DashboardIncomeExpenseReportDto } from "./types.js";

interface DashboardDateRange {
    dateFrom: Date;
    dateTo: Date;
}

let antiForgeryToken: string | null = null;
let periodPresetSelect: HTMLSelectElement;
let periodSelect: HTMLSelectElement;
let expenseSourceSelect: HTMLSelectElement;
let dateFromInput: HTMLInputElement;
let dateToInput: HTMLInputElement;
let expenseSourceHelpHost: HTMLElement;
let accountFilterButton: HTMLButtonElement;
let accountFilterList: HTMLElement;
let storeSearchInput: HTMLInputElement;
let storeFilterButton: HTMLButtonElement;
let storeFilterList: HTMLElement;
let summaryElement: HTMLElement;
let errorElement: HTMLElement;
let chartCanvas: HTMLCanvasElement;
let incomeExpenseChart: any = null;
let dashboardAccounts: DashboardAccountDto[] = [];
let reportStores: string[] = [];
let selectedAccountIds = new Set<string>();
let selectedStores = new Set<string>();

document.addEventListener("DOMContentLoaded", () => {
    initDashboardPage().catch(function (error) {
        console.error(error);
    });
});

async function initDashboardPage(): Promise<void> {
    antiForgeryToken = getRequestVerificationToken();
    bindElements();
    setDefaultFilters();
    bindEvents();
    renderExpenseSourceHelp();
    await loadAndRenderAccountsAsync();
    await loadAndRenderReportAsync();
}

function bindElements(): void {
    periodPresetSelect = requireSelectById("dashboardIncomeExpensePeriodPreset");
    periodSelect = requireSelectById("dashboardIncomeExpensePeriod");
    expenseSourceSelect = requireSelectById("dashboardIncomeExpenseExpenseSource");
    dateFromInput = requireInputById("dashboardIncomeExpenseDateFrom");
    dateToInput = requireInputById("dashboardIncomeExpenseDateTo");
    expenseSourceHelpHost = requireElementById("dashboardExpenseSourceHelp");
    accountFilterButton = requireElementById<HTMLButtonElement>("dashboardAccountFilterButton");
    accountFilterList = requireElementById("dashboardAccountFilterList");
    storeSearchInput = requireInputById("dashboardStoreSearch");
    storeFilterButton = requireElementById<HTMLButtonElement>("dashboardStoreFilterButton");
    storeFilterList = requireElementById("dashboardStoreFilterList");
    summaryElement = requireElementById("dashboardIncomeExpenseSummary");
    errorElement = requireElementById("dashboardIncomeExpenseError");
    chartCanvas = requireElementById<HTMLCanvasElement>("dashboardIncomeExpenseChart");
}

function bindEvents(): void {
    periodPresetSelect.addEventListener("change", onPeriodPresetChanged);
    periodSelect.addEventListener("change", onFilterChanged);
    expenseSourceSelect.addEventListener("change", onExpenseSourceChanged);
    dateFromInput.addEventListener("change", onFilterChanged);
    dateToInput.addEventListener("change", onFilterChanged);
    accountFilterList.addEventListener("change", onAccountFilterChanged);
    storeSearchInput.addEventListener("input", renderStoreFilter);
    storeFilterList.addEventListener("change", onStoreFilterChanged);
}

function setDefaultFilters(): void {
    periodPresetSelect.value = "currentYear";
    periodSelect.value = "month";
    expenseSourceSelect.value = "receiptsAndMoneyMovements";
    applySelectedPeriodPreset();
}

function renderExpenseSourceHelp(): void {
    renderHelpTooltip(expenseSourceHelpHost, {
        title: "Расходы",
        text: [
            "Фильтр выбирает, какие данные попадут в сумму расходов на графике.",
            "Чеки учитывают суммы чеков. Операции учитывают расходные движения по счетам. Чеки и операции берут операции, а чеки без связанной операции добавляют отдельно."
        ]
    });
}

function onPeriodPresetChanged(): void {
    applySelectedPeriodPreset();
    loadAndRenderReportAsync();
}

function onExpenseSourceChanged(): void {
    if (expenseSourceSelect.value === "moneyMovements") {
        selectedStores.clear();
        storeSearchInput.value = "";
    }

    updateStoreFilterState();
    updateStoreFilterButtonText();
    loadAndRenderReportAsync();
}

function applySelectedPeriodPreset(): void {
    const range = getDateRangeByPeriodPreset(periodPresetSelect.value || "currentYear", new Date());
    dateFromInput.value = formatDateInputValue(range.dateFrom);
    dateToInput.value = formatDateInputValue(range.dateTo);
}

async function onFilterChanged(): Promise<void> {
    await loadAndRenderReportAsync();
}

async function loadAndRenderReportAsync(): Promise<void> {
    hideError();

    try {
        const report = await loadIncomeExpenseReportAsync(
            dateFromInput.value,
            dateToInput.value,
            periodSelect.value,
            expenseSourceSelect.value,
            Array.from(selectedAccountIds),
            Array.from(selectedStores),
            antiForgeryToken
        );

        reportStores = report.stores || [];
        removeUnavailableSelectedStores();
        renderStoreFilter();
        updateStoreFilterButtonText();
        updateStoreFilterState();
        renderSummary(report);
        renderChart(report);
    }
    catch (error) {
        showError(error instanceof Error ? error.message : "Не удалось загрузить отчёт.");
    }
}

async function loadAndRenderAccountsAsync(): Promise<void> {
    try {
        dashboardAccounts = await loadDashboardAccountsAsync(antiForgeryToken);
        renderAccountFilter();
        updateAccountFilterButtonText();
    }
    catch (error) {
        showError(error instanceof Error ? error.message : "Не удалось загрузить счета.");
        throw error;
    }
}

function onAccountFilterChanged(event: Event): void {
    const target = event.target;

    if (!(target instanceof HTMLInputElement) || target.type !== "checkbox") {
        return;
    }

    if (target.checked) {
        selectedAccountIds.add(target.value);
    } else {
        selectedAccountIds.delete(target.value);
    }

    selectedStores.clear();
    storeSearchInput.value = "";
    updateAccountFilterButtonText();
    loadAndRenderReportAsync();
}

function renderAccountFilter(): void {
    accountFilterList.replaceChildren();

    if (dashboardAccounts.length === 0) {
        const emptyText = document.createElement("div");
        emptyText.classList.add("text-muted", "small");
        emptyText.textContent = "Счета не найдены";
        accountFilterList.append(emptyText);
        return;
    }

    for (const account of dashboardAccounts) {
        const id = `dashboardAccount_${account.id}`;

        const wrapper = document.createElement("div");
        wrapper.classList.add("form-check");

        const input = document.createElement("input");
        input.type = "checkbox";
        input.classList.add("form-check-input");
        input.id = id;
        input.name = "dashboardAccountFilter";
        input.value = account.id;
        input.checked = selectedAccountIds.has(account.id);

        const label = document.createElement("label");
        label.classList.add("form-check-label");
        label.htmlFor = id;
        label.textContent = account.name;

        wrapper.append(input, label);
        accountFilterList.append(wrapper);
    }
}

function onStoreFilterChanged(event: Event): void {
    const target = event.target;

    if (!(target instanceof HTMLInputElement) || target.type !== "checkbox") {
        return;
    }

    if (target.checked) {
        selectedStores.add(target.value);
    } else {
        selectedStores.delete(target.value);
    }

    updateStoreFilterButtonText();
    loadAndRenderReportAsync();
}

function renderStoreFilter(): void {
    const searchText = normalizeSearchText(storeSearchInput.value);
    const filteredStores = reportStores.filter(function (store) {
        return normalizeSearchText(store).includes(searchText);
    });

    storeFilterList.replaceChildren();

    if (filteredStores.length === 0) {
        const emptyText = document.createElement("div");
        emptyText.classList.add("text-muted", "small");
        emptyText.textContent = "Магазины не найдены";
        storeFilterList.append(emptyText);
        return;
    }

    for (const store of filteredStores) {
        const id = buildStoreCheckboxId(store);

        const wrapper = document.createElement("div");
        wrapper.classList.add("form-check");

        const input = document.createElement("input");
        input.type = "checkbox";
        input.classList.add("form-check-input");
        input.id = id;
        input.name = "dashboardStoreFilter";
        input.value = store;
        input.checked = selectedStores.has(store);

        const label = document.createElement("label");
        label.classList.add("form-check-label");
        label.htmlFor = id;
        label.textContent = store;

        wrapper.append(input, label);
        storeFilterList.append(wrapper);
    }
}

function renderSummary(report: DashboardIncomeExpenseReportDto): void {
    const incomeTotal = report.points.reduce(function (sum, point) {
        return sum + Number(point.incomeSum || 0);
    }, 0);

    const expenseTotal = report.points.reduce(function (sum, point) {
        return sum + Number(point.expenseSum || 0);
    }, 0);

    summaryElement.replaceChildren(
        createSummaryBadge("Доход", incomeTotal, "text-bg-success"),
        createSummaryBadge("Расход", expenseTotal, "text-bg-danger")
    );
}

function renderChart(report: DashboardIncomeExpenseReportDto): void {
    const context = chartCanvas.getContext("2d");

    if (!context) {
        throw new Error("Не удалось получить контекст графика.");
    }

    if (incomeExpenseChart) {
        incomeExpenseChart.destroy();
    }

    incomeExpenseChart = new Chart(context, {
        type: "bar",
        data: {
            labels: report.points.map(function (point) { return point.label; }),
            datasets: [
                {
                    label: "Доход",
                    data: report.points.map(function (point) { return point.incomeSum; }),
                    backgroundColor: "rgba(25, 135, 84, 0.78)",
                    borderColor: "rgb(25, 135, 84)",
                    borderWidth: 1
                },
                {
                    label: "Расход",
                    data: report.points.map(function (point) { return point.expenseSum; }),
                    backgroundColor: "rgba(220, 53, 69, 0.78)",
                    borderColor: "rgb(220, 53, 69)",
                    borderWidth: 1
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function (value: unknown) {
                            return formatMoneyRub(value);
                        }
                    }
                }
            },
            plugins: {
                tooltip: {
                    callbacks: {
                        label: function (context: { dataset: { label: string }, parsed: { y: number } }) {
                            return `${context.dataset.label}: ${formatMoneyRub(context.parsed.y)}`;
                        }
                    }
                }
            }
        }
    });
}

function createSummaryBadge(label: string, value: number, className: string): HTMLElement {
    const badge = document.createElement("span");
    badge.classList.add("badge", className, "fs-6");
    badge.textContent = `${label}: ${formatMoneyRub(value)}`;
    return badge;
}

function updateStoreFilterButtonText(): void {
    if (expenseSourceSelect.value === "moneyMovements") {
        storeFilterButton.textContent = "Магазины недоступны";
        return;
    }

    if (selectedStores.size === 0) {
        storeFilterButton.textContent = "Все магазины";
        return;
    }

    storeFilterButton.textContent = `Магазины: ${selectedStores.size}`;
}

function removeUnavailableSelectedStores(): void {
    selectedStores = new Set(Array.from(selectedStores).filter(function (store) {
        return reportStores.includes(store);
    }));
}

function updateStoreFilterState(): void {
    const isStoreFilterAvailable = expenseSourceSelect.value !== "moneyMovements";
    storeFilterButton.disabled = !isStoreFilterAvailable;
    storeSearchInput.disabled = !isStoreFilterAvailable;
}

function updateAccountFilterButtonText(): void {
    if (selectedAccountIds.size === 0) {
        accountFilterButton.textContent = "Все счета";
        return;
    }

    accountFilterButton.textContent = `Счета: ${selectedAccountIds.size}`;
}

function showError(message: string): void {
    errorElement.textContent = message;
    setElementVisible(errorElement, true);
}

function hideError(): void {
    errorElement.textContent = "";
    setElementVisible(errorElement, false);
}

function formatDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function getDateRangeByPeriodPreset(periodPreset: string, now: Date): DashboardDateRange {
    const currentDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    if (periodPreset === "currentDay") {
        return {
            dateFrom: currentDate,
            dateTo: currentDate
        };
    }

    if (periodPreset === "previousDay") {
        const previousDay = new Date(currentDate);
        previousDay.setDate(currentDate.getDate() - 1);

        return {
            dateFrom: previousDay,
            dateTo: previousDay
        };
    }

    if (periodPreset === "currentWeek") {
        const dayOfWeek = currentDate.getDay();
        const daysFromMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
        const dateFrom = new Date(currentDate);
        dateFrom.setDate(currentDate.getDate() - daysFromMonday);

        const dateTo = new Date(dateFrom);
        dateTo.setDate(dateFrom.getDate() + 6);

        return {
            dateFrom: dateFrom,
            dateTo: dateTo
        };
    }

    if (periodPreset === "previousWeek") {
        const dayOfWeek = currentDate.getDay();
        const daysFromMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
        const dateTo = new Date(currentDate);
        dateTo.setDate(currentDate.getDate() - daysFromMonday - 1);

        const dateFrom = new Date(dateTo);
        dateFrom.setDate(dateTo.getDate() - 6);

        return {
            dateFrom: dateFrom,
            dateTo: dateTo
        };
    }

    if (periodPreset === "currentYear") {
        return {
            dateFrom: new Date(currentDate.getFullYear(), 0, 1),
            dateTo: new Date(currentDate.getFullYear(), 11, 31)
        };
    }

    if (periodPreset === "previousMonth") {
        return {
            dateFrom: new Date(currentDate.getFullYear(), currentDate.getMonth() - 1, 1),
            dateTo: new Date(currentDate.getFullYear(), currentDate.getMonth(), 0)
        };
    }

    return {
        dateFrom: new Date(currentDate.getFullYear(), currentDate.getMonth(), 1),
        dateTo: new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0)
    };
}

function normalizeSearchText(value: string): string {
    return value.trim().toLocaleLowerCase("ru-RU");
}

function buildStoreCheckboxId(store: string): string {
    let hash = 0;

    for (const char of store) {
        hash = ((hash << 5) - hash) + char.charCodeAt(0);
        hash |= 0;
    }

    return `dashboardStore_${Math.abs(hash)}`;
}
