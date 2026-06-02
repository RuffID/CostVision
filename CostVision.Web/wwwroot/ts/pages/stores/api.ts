import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess, type ServiceResult, type ServiceResultWithData } from "../../shared/http.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import type { StoreList, StoreSortBy, StoreSortDirection } from "./types.js";

export async function getStores(search: string, useAdaptiveNames: boolean, page: number, pageSize: number, sortBy: StoreSortBy, sortDirection: StoreSortDirection): Promise<StoreList> {
    const params = new URLSearchParams({
        search: search,
        useAdaptiveNames: String(useAdaptiveNames),
        page: String(page),
        pageSize: String(pageSize),
        sortBy: sortBy,
        sortDirection: sortDirection
    });

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
