import { buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../../shared/http.js";
import { ManualReceiptPayload, ManualReceiptResponse } from "./types.js";

type ManualReceiptServiceResponse = ServiceResultWithData<ManualReceiptResponse>;

export async function submitManualReceiptAsync(payload: ManualReceiptPayload, antiForgeryToken: string | null): Promise<ManualReceiptResponse> {
    const response = await sendJsonRequest<ManualReceiptServiceResponse>("?handler=Manual", "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response);
}
