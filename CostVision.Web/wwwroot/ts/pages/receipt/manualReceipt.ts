import { hasResponseFields, buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../../shared/http.js";
import { ManualReceiptOutcome, ManualReceiptPayload, ManualReceiptResponse } from "./types.js";

type ManualReceiptServiceResponse = ServiceResultWithData<ManualReceiptResponse>;

export async function submitManualReceiptAsync(payload: ManualReceiptPayload, antiForgeryToken: string | null): Promise<ManualReceiptResponse> {
    const response = await sendJsonRequest<ManualReceiptServiceResponse>("?handler=Manual", "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response, isManualReceiptResponse);
}

function isManualReceiptResponse(value: unknown): boolean {
    if (!hasResponseFields(value, [], ["outcome"], [], ["receipt"])) {
        return false;
    }
    if (value.outcome === ManualReceiptOutcome.Created) {
        return true;
    }
    return (value.outcome === ManualReceiptOutcome.AddedToAccount ||
        value.outcome === ManualReceiptOutcome.AlreadyExists ||
        value.outcome === ManualReceiptOutcome.AlreadyExistsInAccount) && typeof value.message === "string";
}
