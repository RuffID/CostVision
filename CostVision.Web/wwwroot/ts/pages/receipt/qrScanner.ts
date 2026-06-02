import { buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../../shared/http.js";
import { QrScanPayload, QrScanServerResponse } from "./types.js";

type QrScanServiceResponse = ServiceResultWithData<QrScanServerResponse>;

export async function submitQrScanAsync(url: string, payload: QrScanPayload, antiForgeryToken: string | null): Promise<QrScanServerResponse> {
    const response = await sendJsonRequest<QrScanServiceResponse>(url, "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response);
}
