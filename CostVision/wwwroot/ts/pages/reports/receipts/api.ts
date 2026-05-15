import { buildJsonHeaders, ServiceResultWithData, sendJsonRequest, unwrapServiceResult } from "../../../shared/http.js";
import { AvailableAccountDto, ReceiptDto } from "./types.js";

type ReceiptListResponse = ServiceResultWithData<ReceiptDto[]>;
type ReceiptResponse = ServiceResultWithData<ReceiptDto>;
type AvailableAccountsResponse = ServiceResultWithData<AvailableAccountDto[]>;
type BooleanResponse = ServiceResultWithData<boolean>;

export async function loadReceiptsApi(rangeQuery: string, forgeryToken: string | null): Promise<ReceiptDto[]> {
    const url = rangeQuery
        ? "?handler=ReceiptList&" + rangeQuery
        : "?handler=ReceiptList";

    const response = await sendJsonRequest<ReceiptListResponse>(url, "GET", buildJsonHeaders(forgeryToken), null);
    return unwrapServiceResult(response);
}

export async function loadAvailableAccountsApi(forgeryToken: string | null): Promise<AvailableAccountDto[]> {
    const response = await sendJsonRequest<AvailableAccountsResponse>("?handler=Accounts", "GET", buildJsonHeaders(forgeryToken), null);
    return unwrapServiceResult(response);
}

export async function openReceiptApi(receiptId: string, forgeryToken: string | null): Promise<ReceiptDto> {
    const response = await sendJsonRequest<ReceiptResponse>(
        "?handler=OpenReceipt",
        "POST",
        buildJsonHeaders(forgeryToken),
        { receiptId: receiptId }
    );

    return unwrapServiceResult(response);
}

export async function refreshReceiptApi(receiptId: string, forgeryToken: string | null): Promise<ReceiptDto> {
    const response = await sendJsonRequest<ReceiptResponse>(
        "?handler=RefreshReceipt",
        "POST",
        buildJsonHeaders(forgeryToken),
        { receiptId: receiptId }
    );

    return unwrapServiceResult(response);
}

export async function deleteReceiptApi(receiptId: string, forgeryToken: string | null): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>(
        "?handler=DeleteReceipt",
        "POST",
        buildJsonHeaders(forgeryToken),
        { receiptId: receiptId }
    );

    return unwrapServiceResult(response);
}

export async function moveReceiptToAccountApi(receiptId: string, sourceAccountId: string, targetAccountId: string, forgeryToken: string | null): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>(
        "?handler=MoveReceiptToAccount",
        "POST",
        buildJsonHeaders(forgeryToken),
        {
            receiptId: receiptId,
            sourceAccountId: sourceAccountId,
            targetAccountId: targetAccountId
        }
    );

    return unwrapServiceResult(response);
}

export async function removeReceiptFromAccountApi(receiptId: string, accountId: string, forgeryToken: string | null): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>(
        "?handler=RemoveReceiptFromAccount",
        "POST",
        buildJsonHeaders(forgeryToken),
        {
            receiptId: receiptId,
            accountId: accountId
        }
    );

    return unwrapServiceResult(response);
}
