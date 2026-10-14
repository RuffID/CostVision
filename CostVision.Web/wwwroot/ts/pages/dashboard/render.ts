import { formatMoneyRub } from "../../shared/formatters.js";
import { setElementVisible } from "../../shared/dom.js";
import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import { ACCOUNT_FILTER_WITHOUT_ACCOUNT, type DashboardState } from "./state.js";
import type { DashboardUi } from "./ui.js";
import type { DashboardIncomeExpenseReportDto } from "./types.js";

const STORE_NAME_MAX_LENGTH = 70;

type DashboardRenderState = Pick<DashboardState, "incomeExpenseChart" | "dashboardAccounts" | "reportStores" |
    "storeSearchQuery" | "selectedAccountIds" | "selectedStores" | "shouldShowSelectedStoresFirst">;

export function createDashboardRenderer(ui: DashboardUi, state: DashboardRenderState) {
    function renderExpenseSourceHelp(): void {
        renderHelpTooltip(ui.expenseSourceHelpHost, {
            title: "Источники доходов и расходов",
            text: [
                "Чеки учитывают только загруженные фискальные документы. Операции учитывают только денежные движения по выбранным счетам.",
                "В режиме Чеки и операции каждый платёж учитывается один раз; непокрытая часть связанного чека добавляется на дате чека. Сначала покрываются более ранние чеки; переплата остаётся на дате платежа.",
                "Покупка и возврат выплаты — расходы. Возврат покупки и выплата вам — доходы. Магазины ограничивают расходную часть."
            ]
        });
    }

    function renderAccountFilter(): void {
        ui.accountFilterList.replaceChildren();

        appendWithoutAccountFilterOption();

        if (state.dashboardAccounts.length === 0) {
            const emptyText = document.createElement("div");
            emptyText.classList.add("text-muted", "small");
            emptyText.textContent = "Счета не найдены";
            ui.accountFilterList.append(emptyText);
            return;
        }

        for (const account of state.dashboardAccounts) {
            const id = `dashboardAccount_${account.id}`;

            const wrapper = document.createElement("div");
            wrapper.classList.add("form-check");

            const input = document.createElement("input");
            input.type = "checkbox";
            input.classList.add("form-check-input");
            input.id = id;
            input.name = "dashboardAccountFilter";
            input.value = account.id;
            input.checked = state.selectedAccountIds.has(account.id);

            const label = document.createElement("label");
            label.classList.add("form-check-label");
            label.htmlFor = id;
            label.textContent = account.name;

            wrapper.append(input, label);
            ui.accountFilterList.append(wrapper);
        }
    }

    function appendWithoutAccountFilterOption(): void {
        const wrapper = document.createElement("div");
        wrapper.classList.add("form-check");

        const input = document.createElement("input");
        input.type = "checkbox";
        input.classList.add("form-check-input");
        input.id = "dashboardAccountWithoutAccount";
        input.name = "dashboardAccountFilter";
        input.value = ACCOUNT_FILTER_WITHOUT_ACCOUNT;
        input.checked = state.selectedAccountIds.has(ACCOUNT_FILTER_WITHOUT_ACCOUNT);

        const label = document.createElement("label");
        label.classList.add("form-check-label");
        label.htmlFor = input.id;
        label.textContent = "Без счёта";

        wrapper.append(input, label);
        ui.accountFilterList.append(wrapper);
    }

    function renderStoreFilter(): void {
        const searchText = normalizeSearchText(state.storeSearchQuery);
        const filteredStores = state.reportStores.filter(function (store) {
            return normalizeSearchText(store).includes(searchText);
        });
        const sortedStores = state.shouldShowSelectedStoresFirst ? sortStoresForFilter(filteredStores) : filteredStores;
        const currentScrollTop = ui.storeFilterList.scrollTop;

        ui.storeFilterList.replaceChildren();

        if (sortedStores.length === 0) {
            const emptyText = document.createElement("div");
            emptyText.classList.add("text-muted", "small");
            emptyText.textContent = "Магазины не найдены";
            ui.storeFilterList.append(emptyText);
            ui.storeFilterList.scrollTop = currentScrollTop;
            return;
        }

        for (const store of sortedStores) {
            const id = buildStoreCheckboxId(store);

            const wrapper = document.createElement("div");
            wrapper.classList.add("d-flex", "align-items-start", "gap-2");

            const input = document.createElement("input");
            input.type = "checkbox";
            input.classList.add("form-check-input", "flex-shrink-0", "mt-1");
            input.id = id;
            input.name = "dashboardStoreFilter";
            input.value = store;
            input.checked = state.selectedStores.has(store);

            const label = document.createElement("label");
            label.classList.add("d-block", "flex-grow-1", "lh-sm", "text-break");
            label.htmlFor = id;
            label.textContent = truncateStoreName(store);
            label.title = store;

            wrapper.append(input, label);
            ui.storeFilterList.append(wrapper);
        }

        ui.storeFilterList.scrollTop = currentScrollTop;
    }

    function renderSummary(report: DashboardIncomeExpenseReportDto): void {
        const incomeTotal = report.points.reduce(function (sum, point) {
            return sum + point.incomeSum;
        }, 0);

        const expenseTotal = report.points.reduce(function (sum, point) {
            return sum + point.expenseSum;
        }, 0);

        ui.summaryElement.replaceChildren(
            createSummaryBadge("Доход", incomeTotal, "text-bg-success"),
            createSummaryBadge("Расход", expenseTotal, "text-bg-danger")
        );
    }

    function renderChart(report: DashboardIncomeExpenseReportDto): void {
        const context = ui.chartCanvas.getContext("2d");

        if (!context) {
            throw new Error("Не удалось получить контекст графика.");
        }

        if (state.incomeExpenseChart) {
            state.incomeExpenseChart.destroy();
        }

        state.incomeExpenseChart = new Chart(context, {
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
                            callback: function (value: string | number) {
                                return formatMoneyRub(Number(value));
                            }
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (context: { dataset: { label?: string }, parsed: { y: number } }) {
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
        badge.classList.add("badge", "text-dark", "fw-bold", "fs-6");
        badge.textContent = `${label}: ${formatMoneyRub(value)}`;
        return badge;
    }

    function updateStoreFilterButtonText(): void {
        if (ui.expenseSourceSelect.value === "moneyMovements") {
            ui.storeFilterButton.textContent = "Магазины недоступны";
            return;
        }

        if (state.selectedStores.size === 0) {
            ui.storeFilterButton.textContent = "Все магазины";
            return;
        }

        ui.storeFilterButton.textContent = `Магазины: ${state.selectedStores.size}`;
    }

    function updateStoreFilterState(): void {
        const isStoreFilterAvailable = ui.expenseSourceSelect.value !== "moneyMovements";
        ui.storeFilterButton.disabled = !isStoreFilterAvailable;
        ui.storeSearchInput.disabled = !isStoreFilterAvailable;
    }

    function updateAccountFilterButtonText(): void {
        if (state.selectedAccountIds.size === 0) {
            ui.accountFilterButton.textContent = "Все счета";
            return;
        }

        ui.accountFilterButton.textContent = `Счета: ${state.selectedAccountIds.size}`;
    }

    function showError(message: string): void {
        ui.errorElement.textContent = message;
        setElementVisible(ui.errorElement, true);
    }

    function hideError(): void {
        ui.errorElement.textContent = "";
        setElementVisible(ui.errorElement, false);
    }

    function normalizeSearchText(value: string): string {
        return value.trim().toLocaleLowerCase("ru-RU");
    }

    function sortStoresForFilter(stores: string[]): string[] {
        return stores.slice().sort(function (left, right) {
            const leftSelected = state.selectedStores.has(left);
            const rightSelected = state.selectedStores.has(right);

            if (leftSelected === rightSelected) {
                return 0;
            }

            return leftSelected ? -1 : 1;
        });
    }

    function truncateStoreName(store: string): string {
        const characters = Array.from(store);

        if (characters.length <= STORE_NAME_MAX_LENGTH) {
            return store;
        }

        return characters.slice(0, STORE_NAME_MAX_LENGTH).join("");
    }

    function buildStoreCheckboxId(store: string): string {
        let hash = 0;

        for (const char of store) {
            hash = ((hash << 5) - hash) + char.charCodeAt(0);
            hash |= 0;
        }

        return `dashboardStore_${Math.abs(hash)}`;
    }

    return { renderExpenseSourceHelp, renderAccountFilter, renderStoreFilter, renderSummary, renderChart, updateStoreFilterButtonText, updateStoreFilterState, updateAccountFilterButtonText, showError, hideError };
}
