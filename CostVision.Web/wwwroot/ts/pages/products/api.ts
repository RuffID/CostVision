import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess, type ServiceResult, type ServiceResultWithData } from "../../shared/http.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import type { ProductList, ProductSortBy, ProductSortDirection } from "./types.js";

export async function getProducts(search: string, page: number, pageSize: number, sortBy: ProductSortBy, sortDirection: ProductSortDirection): Promise<ProductList> {
    const params = new URLSearchParams({
        search: search,
        page: String(page),
        pageSize: String(pageSize),
        sortBy: sortBy,
        sortDirection: sortDirection
    });

    const result = await sendJsonRequest<ServiceResultWithData<ProductList>>(`/products?handler=List&${params.toString()}`);
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
