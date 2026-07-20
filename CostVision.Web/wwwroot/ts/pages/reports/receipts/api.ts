import { buildJsonHeaders, ServiceResultWithData, sendJsonRequest, unwrapServiceResult } from "../../../shared/http.js";
import { AvailableAccountDto, GetReceiptMoneyMovementCandidatesRequest, LinkReceiptMoneyMovementRequest, ReceiptDto, ReceiptList, ReceiptMoneyMovementDto } from "./types.js";

type ReceiptListResponse = ServiceResultWithData<ReceiptList>;
type ReceiptResponse = ServiceResultWithData<ReceiptDto>;
type AvailableAccountsResponse = ServiceResultWithData<AvailableAccountDto[]>;
type ReceiptMoneyMovementListResponse = ServiceResultWithData<ReceiptMoneyMovementDto[]>;
type BooleanResponse = ServiceResultWithData<boolean>;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

export async function loadReceiptsApi(rangeQuery: string, forgeryToken: string | null): Promise<ReceiptList> {
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
            sourceAccountId: sourceAccountId || EMPTY_GUID,
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

export async function loadLinkedMoneyMovementsApi(receiptId: string, forgeryToken: string | null): Promise<ReceiptMoneyMovementDto[]> {
    const params = new URLSearchParams();
    params.set("receiptId", receiptId);

    const response = await sendJsonRequest<ReceiptMoneyMovementListResponse>(`?handler=LinkedMoneyMovements&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken), null);
    return unwrapServiceResult(response);
}

export async function loadMoneyMovementCandidatesApi(request: GetReceiptMoneyMovementCandidatesRequest, forgeryToken: string | null): Promise<ReceiptMoneyMovementDto[]> {
    const params = new URLSearchParams();
    params.set("receiptId", request.receiptId);
    params.set("useTimeWindow", String(request.useTimeWindow));
    params.set("useAmountFilter", String(request.useAmountFilter));
    params.set("excludeLinkedMoneyMovements", String(request.excludeLinkedMoneyMovements));

    if (request.dateFrom) {
        params.set("dateFrom", request.dateFrom);
    }

    if (request.dateTo) {
        params.set("dateTo", request.dateTo);
    }

    if (request.amountTolerance !== null) {
        params.set("amountTolerance", String(request.amountTolerance));
    }

    if (request.timeWindowHours !== null) {
        params.set("timeWindowHours", String(request.timeWindowHours));
    }

    const response = await sendJsonRequest<ReceiptMoneyMovementListResponse>(`?handler=MoneyMovementCandidates&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken), null);
    return unwrapServiceResult(response);
}

export async function linkReceiptMoneyMovementApi(request: LinkReceiptMoneyMovementRequest, forgeryToken: string | null): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>("?handler=LinkMoneyMovement", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult(response);
}

export async function unlinkReceiptMoneyMovementApi(request: LinkReceiptMoneyMovementRequest, forgeryToken: string | null): Promise<boolean> {
    const response = await sendJsonRequest<BooleanResponse>("?handler=UnlinkMoneyMovement", "POST", buildJsonHeaders(forgeryToken), request);
    return unwrapServiceResult(response);
}
