import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess, type ServiceResult, type ServiceResultWithData } from "../../shared/http.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { loadAvailableAccountsApi, moveReceiptToAccountApi, openReceiptApi, refreshReceiptApi, removeReceiptFromAccountApi } from "../reports/receipts/api.js";
import type { AvailableAccountDto, ReceiptDto } from "../reports/receipts/types.js";
import type { StoreList, StoreReceiptList, StoreSortBy, StoreSortDirection } from "./types.js";

export async function getStores(search: string, groupByName: boolean, page: number, pageSize: number, sortBy: StoreSortBy, sortDirection: StoreSortDirection, accountId: string): Promise<StoreList> {
    const params = new URLSearchParams({
        search: search,
        groupByName: String(groupByName),
        page: String(page),
        pageSize: String(pageSize),
        sortBy: sortBy,
        sortDirection: sortDirection
    });
    if (accountId) params.set("accountId", accountId);

    const result = await sendJsonRequest<ServiceResultWithData<StoreList>>(`/stores?handler=List&${params.toString()}`);
    return unwrapServiceResult(result);
}

export async function updateStoreAdaptiveName(storeId: string, adaptiveName: string): Promise<void> {
    const result = await sendJsonRequest<ServiceResult>(
        "/stores?handler=UpdateAdaptiveName",
        "POST",
        buildJsonHeaders(getRequestVerificationToken()),
        {
            storeId: storeId,
            adaptiveName: adaptiveName
        });

    unwrapServiceSuccess(result);
}

export async function getStoreReceipts(storeId: string | null, groupKey: string | null, page: number, pageSize: number): Promise<StoreReceiptList> {
    const params = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize)
    });

    if (storeId) {
        params.set("storeId", storeId);
    }

    if (groupKey) {
        params.set("groupKey", groupKey);
    }

    const result = await sendJsonRequest<ServiceResultWithData<StoreReceiptList>>(`/stores?handler=Receipts&${params.toString()}`);
    return unwrapServiceResult(result);
}

export function openStoreReceipt(receiptId: string): Promise<ReceiptDto> {
    return openReceiptApi(receiptId, getRequestVerificationToken());
}

export function refreshStoreReceipt(receiptId: string): Promise<ReceiptDto> {
    return refreshReceiptApi(receiptId, getRequestVerificationToken());
}

export function loadStoreAvailableAccounts(): Promise<AvailableAccountDto[]> {
    return loadAvailableAccountsApi(getRequestVerificationToken());
}

export function moveStoreReceiptToAccount(receiptId: string, sourceAccountId: string, targetAccountId: string): Promise<void> {
    return moveReceiptToAccountApi(receiptId, sourceAccountId, targetAccountId, getRequestVerificationToken());
}

export function removeStoreReceiptFromAccount(receiptId: string, accountId: string): Promise<void> {
    return removeReceiptFromAccountApi(receiptId, accountId, getRequestVerificationToken());
}
