import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess, type ServiceResult, type ServiceResultWithData } from "../../shared/http.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import type { ProductList, ProductSortBy, ProductSortDirection, ProductStorePurchase } from "./types.js";

export interface AccountFilterOption { id: string; name: string; }

export async function getProducts(search: string, page: number, pageSize: number, sortBy: ProductSortBy, sortDirection: ProductSortDirection, accountId: string): Promise<ProductList> {
    const params = new URLSearchParams({
        search: search,
        page: String(page),
        pageSize: String(pageSize),
        sortBy: sortBy,
        sortDirection: sortDirection
    });
    if (accountId) params.set("accountId", accountId);

    const result = await sendJsonRequest<ServiceResultWithData<ProductList>>(`/products?handler=List&${params.toString()}`);
    return unwrapServiceResult(result);
}

export async function getProductAccounts(): Promise<AccountFilterOption[]> {
    const result = await sendJsonRequest<ServiceResultWithData<AccountFilterOption[]>>("/products?handler=Accounts");
    return unwrapServiceResult(result);
}

export async function updateProductAdaptiveName(productId: string, adaptiveName: string): Promise<void> {
    const result = await sendJsonRequest<ServiceResult>(
        "/products?handler=UpdateAdaptiveName",
        "POST",
        buildJsonHeaders(getRequestVerificationToken()),
        {
            productId: productId,
            adaptiveName: adaptiveName
        });

    unwrapServiceSuccess(result);
}

export async function getProductStorePurchases(productId: string): Promise<ProductStorePurchase[]> {
    const params = new URLSearchParams({ productId: productId });
    const result = await sendJsonRequest<ServiceResultWithData<ProductStorePurchase[]>>(`/products?handler=StorePurchases&${params.toString()}`);
    return unwrapServiceResult(result);
}
