export { openReceiptApi, refreshReceiptApi, deleteReceiptApi, moveReceiptToAccountApi, removeReceiptFromAccountApi } from "../../../shared/receipts/api.js";
import type { PagedList } from "../../../shared/paging.js";
import { isPagedResponse, isResponseArray, buildJsonHeaders, ServiceResult, ServiceResultWithData, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess } from "../../../shared/http.js";
import { AvailableAccountDto, GetReceiptMoneyMovementCandidatesRequest, LinkReceiptMoneyMovementRequest, ReceiptList, ReceiptMoneyMovementDto } from "./types.js";

type ReceiptListResponse = ServiceResultWithData<ReceiptList>;
type AvailableAccountsResponse = ServiceResultWithData<AvailableAccountDto[]>;
type ReceiptMoneyMovementListResponse = ServiceResultWithData<ReceiptMoneyMovementDto[]>;

export async function loadReceiptsApi(rangeQuery: string, forgeryToken: string | null, signal?: AbortSignal): Promise<ReceiptList> {
    const url = rangeQuery
        ? "?handler=ReceiptList&" + rangeQuery
        : "?handler=ReceiptList";

    const response = await sendJsonRequest<ReceiptListResponse>(url, "GET", buildJsonHeaders(forgeryToken), null, signal);
    return unwrapServiceResult(response, value => isPagedResponse(value, ["totalSum","incomeSum"]));
}

export async function loadAvailableAccountsApi(forgeryToken: string | null, signal?: AbortSignal): Promise<AvailableAccountDto[]> {
    const response = await sendJsonRequest<AvailableAccountsResponse>("?handler=Accounts", "GET", buildJsonHeaders(forgeryToken), null, signal);
    return unwrapServiceResult(response, isResponseArray);
}

export async function loadLinkedMoneyMovementsApi(receiptId: string, forgeryToken: string | null, signal?: AbortSignal): Promise<ReceiptMoneyMovementDto[]> {
    const params = new URLSearchParams();
    params.set("receiptId", receiptId);

    const response = await sendJsonRequest<ReceiptMoneyMovementListResponse>(`?handler=LinkedMoneyMovements&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken), null, signal);
    return unwrapServiceResult(response, isResponseArray);
}

export async function loadMoneyMovementCandidatesApi(request: GetReceiptMoneyMovementCandidatesRequest, forgeryToken: string | null, signal?: AbortSignal): Promise<PagedList<ReceiptMoneyMovementDto>> {
    const params = new URLSearchParams();
    params.set("receiptId", request.receiptId);
    params.set("page", String(request.page ?? 1));
    params.set("pageSize", String(request.pageSize ?? 20));
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

    const response = await sendJsonRequest<ServiceResultWithData<PagedList<ReceiptMoneyMovementDto>>>(`?handler=MoneyMovementCandidates&${params.toString()}`, "GET", buildJsonHeaders(forgeryToken), null, signal);
    return unwrapServiceResult(response, value => isPagedResponse(value, []));
}

export async function linkReceiptMoneyMovementApi(request: LinkReceiptMoneyMovementRequest, forgeryToken: string | null): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>("?handler=LinkMoneyMovement", "POST", buildJsonHeaders(forgeryToken), request);
    unwrapServiceSuccess(response);
}

export async function unlinkReceiptMoneyMovementApi(request: LinkReceiptMoneyMovementRequest, forgeryToken: string | null): Promise<void> {
    const response = await sendJsonRequest<ServiceResult>("?handler=UnlinkMoneyMovement", "POST", buildJsonHeaders(forgeryToken), request);
    unwrapServiceSuccess(response);
}
