import { isResponseArray, buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../../shared/http.js";
import { ReceiptAccountDto } from "./types.js";

type AccountsResponse = ServiceResultWithData<ReceiptAccountDto[]>;

export async function loadBrowserUserId(token: string | null, signal: AbortSignal): Promise<string> {
    const response = await sendJsonRequest<ServiceResultWithData<string>>("?handler=BrowserUser", "GET",
        buildJsonHeaders(token), null, signal);
    return unwrapServiceResult(response, value => typeof value === "string" &&
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) &&
        value !== "00000000-0000-0000-0000-000000000000");
}

export async function loadReceiptAccountsAsync(antiForgeryToken: string | null, signal?: AbortSignal): Promise<ReceiptAccountDto[]> {
    const response = await sendJsonRequest<AccountsResponse>("?handler=Accounts", "GET", buildJsonHeaders(antiForgeryToken), null, signal);
    return unwrapServiceResult(response, value => isResponseArray(value) && Array.isArray(value) && value.every(item =>
        item && typeof item === "object" && "id" in item && typeof item.id === "string" && item.id.length > 0 &&
        "name" in item && typeof item.name === "string" && item.name.length > 0));
}
