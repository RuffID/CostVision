import { requireElementById, requireInputById, requireSelectById } from "../../shared/dom.js";

export function getDashboardUi() {
    return {
        periodPresetSelect: requireSelectById("dashboardIncomeExpensePeriodPreset"),
        periodSelect: requireSelectById("dashboardIncomeExpensePeriod"),
        expenseSourceSelect: requireSelectById("dashboardIncomeExpenseExpenseSource"),
        dateFromInput: requireInputById("dashboardIncomeExpenseDateFrom"),
        dateToInput: requireInputById("dashboardIncomeExpenseDateTo"),
        expenseSourceHelpHost: requireElementById("dashboardExpenseSourceHelp"),
        accountFilterButton: requireElementById<HTMLButtonElement>("dashboardAccountFilterButton", HTMLButtonElement),
        accountFilterList: requireElementById("dashboardAccountFilterList"),
        storeSearchInput: requireInputById("dashboardStoreSearch"),
        storeFilterButton: requireElementById<HTMLButtonElement>("dashboardStoreFilterButton", HTMLButtonElement),
        storeFilterList: requireElementById("dashboardStoreFilterList"),
        summaryElement: requireElementById("dashboardIncomeExpenseSummary"),
        errorElement: requireElementById("dashboardIncomeExpenseError"),
        chartCanvas: requireElementById<HTMLCanvasElement>("dashboardIncomeExpenseChart", HTMLCanvasElement),
    };
}

export type DashboardUi = ReturnType<typeof getDashboardUi>;
