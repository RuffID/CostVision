import { createRequestCoordinator } from "../../shared/requestCoordinator.js";
import type { DashboardAccountDto } from "./types.js";

export const ACCOUNT_FILTER_WITHOUT_ACCOUNT = "__without_account__";

export function createDashboardState() {
    return {
        antiForgeryToken: null as string | null,
        incomeExpenseChart: null as ChartInstance | null,
        dashboardAccounts: [] as DashboardAccountDto[],
        reportStores: [] as string[],
        storeSearchQuery: "",
        selectedAccountIds: new Set<string>(),
        selectedStores: new Set<string>(),
        shouldShowSelectedStoresFirst: false,
        reportRequests: createRequestCoordinator(),
        accountRequests: createRequestCoordinator()
    };
}

export type DashboardState = ReturnType<typeof createDashboardState>;
