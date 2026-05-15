import { buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../shared/http.js";
import { ReceiptAccountDto } from "./receiptTypes.js";

type AccountsResponse = ServiceResultWithData<ReceiptAccountDto[]>;

export async function loadReceiptAccountsAsync(antiForgeryToken: string | null): Promise<ReceiptAccountDto[]> {
    const response = await sendJsonRequest<AccountsResponse>("?handler=Accounts", "GET", buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}
