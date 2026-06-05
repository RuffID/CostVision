import { buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../../shared/http.js";
import { DashboardAccountDto, DashboardIncomeExpenseReportDto } from "./types.js";

type DashboardIncomeExpenseReportResponse = ServiceResultWithData<DashboardIncomeExpenseReportDto>;
type DashboardAccountListResponse = ServiceResultWithData<DashboardAccountDto[]>;

export async function loadDashboardAccountsAsync(antiForgeryToken: string | null): Promise<DashboardAccountDto[]> {
    const response = await sendJsonRequest<DashboardAccountListResponse>("?handler=Accounts", "GET", buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

export async function loadIncomeExpenseReportAsync(dateFrom: string, dateTo: string, period: string, expenseSource: string, accountIds: string[], includeWithoutAccount: boolean, storeNames: string[], antiForgeryToken: string | null): Promise<DashboardIncomeExpenseReportDto> {
    const parameters = new URLSearchParams();
    parameters.set("dateFrom", dateFrom);
    parameters.set("dateTo", dateTo);
    parameters.set("period", period);
    parameters.set("expenseSource", expenseSource);
    parameters.set("includeWithoutAccount", String(includeWithoutAccount));

    for (const accountId of accountIds) {
        parameters.append("accountIds", accountId);
    }

    for (const storeName of storeNames) {
        parameters.append("storeNames", storeName);
    }

    const response = await sendJsonRequest<DashboardIncomeExpenseReportResponse>(
        `?handler=IncomeExpenseReport&${parameters.toString()}`,
        "GET",
        buildJsonHeaders(antiForgeryToken)
    );

    return unwrapServiceResult(response);
}
