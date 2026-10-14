import { hasResponseFields, buildJsonHeaders, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess, type ServiceResult, type ServiceResultWithData } from "../http.js";
import type { ReceiptDto } from "./types.js";

type ReceiptResponse = ServiceResultWithData<ReceiptDto>;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export async function openReceiptApi(receiptId: string, forgeryToken: string | null, signal?: AbortSignal): Promise<ReceiptDto> {
    const response = await sendJsonRequest<ReceiptResponse>(
        "?handler=OpenReceipt",
        "POST",
        buildJsonHeaders(forgeryToken),
        { receiptId: receiptId },
        signal
    );

    return unwrapServiceResult(response, value => hasResponseFields(value, ["items", "accounts"], ["totalSum", "operationType"], ["id", "dateTime"]));
}

export async function refreshReceiptApi(receiptId: string, forgeryToken: string | null): Promise<ReceiptDto> {
    const response = await sendJsonRequest<ReceiptResponse>(
        "?handler=RefreshReceipt",
        "POST",
        buildJsonHeaders(forgeryToken),
        { receiptId: receiptId }
    );

    return unwrapServiceResult(response, value => hasResponseFields(value, ["items", "accounts"], ["totalSum", "operationType"], ["id", "dateTime"]));
}

export async function deleteReceiptApi(receiptId: string, forgeryToken: string | null): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>(
        "?handler=DeleteReceipt",
        "POST",
        buildJsonHeaders(forgeryToken),
        { receiptId: receiptId }
    );

    unwrapServiceSuccess(response);
}

export async function moveReceiptToAccountApi(receiptId: string, sourceAccountId: string, targetAccountId: string, forgeryToken: string | null): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>(
        "?handler=MoveReceiptToAccount",
        "POST",
        buildJsonHeaders(forgeryToken),
        {
            receiptId: receiptId,
            sourceAccountId: sourceAccountId || EMPTY_GUID,
            targetAccountId: targetAccountId
        }
    );

    unwrapServiceSuccess(response);
}

export async function removeReceiptFromAccountApi(receiptId: string, accountId: string, forgeryToken: string | null): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>(
        "?handler=RemoveReceiptFromAccount",
        "POST",
        buildJsonHeaders(forgeryToken),
        {
            receiptId: receiptId,
            accountId: accountId
        }
    );

    unwrapServiceSuccess(response);
}
