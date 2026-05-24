import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult, type ServiceResultWithData } from "../../shared/http.js";
import type { CreateMoneyMovementRequest, MoneyMovementDto, UserAccountViewModel } from "./types.js";

type AccountListResponse = ServiceResultWithData<UserAccountViewModel[]>;
type MoneyMovementListResponse = ServiceResultWithData<MoneyMovementDto[]>;
type MoneyMovementResponse = ServiceResultWithData<MoneyMovementDto>;

export async function loadAccounts(forgeryToken: string | null): Promise<UserAccountViewModel[]> {
    const response = await sendJsonRequest<AccountListResponse>("?handler=Accounts", "GET", buildJsonHeaders(forgeryToken));
    return unwrapServiceResult<UserAccountViewModel[]>(response);
}

export async function loadMoneyMovements(forgeryToken: string | null, dateFrom: string, dateTo: string, accountId: string): Promise<MoneyMovementDto[]> {
    const params = new URLSearchParams();
    params.set("dateFrom", dateFrom);
    params.set("dateTo", dateTo);

    if (accountId) {
        params.set("accountId", accountId);
    }

    const response = await sendJsonRequest<MoneyMovementListResponse>(`?handler=List&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken));
    return unwrapServiceResult<MoneyMovementDto[]>(response);
}

export async function createMoneyMovement(forgeryToken: string | null, request: CreateMoneyMovementRequest): Promise<MoneyMovementDto> {
    const response = await sendJsonRequest<MoneyMovementResponse>("?handler=Create", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult<MoneyMovementDto>(response);
}
