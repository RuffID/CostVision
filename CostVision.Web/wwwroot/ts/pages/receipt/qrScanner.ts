import { buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../../shared/http.js";
import { QrScanPayload, QrScanServerResponse } from "./types.js";

type QrScanServiceResponse = ServiceResultWithData<QrScanServerResponse>;

export async function submitQrScanAsync(url: string, payload: QrScanPayload, antiForgeryToken: string | null): Promise<QrScanServerResponse> {
    const response = await sendJsonRequest<QrScanServiceResponse>(url, "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response, value => isQrScanResponse(value, payload.results.length));
}

function isQrScanResponse(value: unknown, expectedRows: number): boolean {
    if (!value || typeof value !== "object") {
        return false;
    }
    const data = value as Record<string, unknown>;
    const counts = [data.scannedCount, data.addedToDbCount, data.errorCount];
    if (!counts.every(count => typeof count === "number" && Number.isSafeInteger(count) && count >= 0)) {
        return false;
    }
    if (!Array.isArray(data.results) || data.results.length !== expectedRows ||
        (data.errorMessage !== undefined && data.errorMessage !== null && typeof data.errorMessage !== "string")) {
        return false;
    }
    let scanned = 0;
    let errors = 0;
    for (const row of data.results) {
        if (!isQrScanRow(row)) {
            return false;
        }
        if (row.decodedText?.trim()) {
            scanned++;
        }
        if (row.errorMessage?.trim()) {
            errors++;
        } else if (!row.decodedText?.trim() || !row.parsed ||
            typeof row.parsed.sum !== "number" || typeof row.parsed.operationType !== "number" ||
            ![1, 2, 3, 4].includes(row.parsed.operationType)) {
            return false;
        }
    }
    return data.scannedCount === scanned && data.errorCount === errors &&
        (data.addedToDbCount as number) === data.results.filter(row => !row.errorMessage?.trim()).length;
}

function isQrScanRow(value: unknown): value is QrScanServerResponse["results"][number] {
    if (!value || typeof value !== "object") {
        return false;
    }
    const row = value as Record<string, unknown>;
    if (typeof row.fileName !== "string" || !isNullableString(row.decodedText) ||
        !isNullableString(row.errorMessage) || (row.photoDateTime !== undefined && !isNullableString(row.photoDateTime))) {
        return false;
    }
    if (row.parsed !== undefined && row.parsed !== null) {
        if (typeof row.parsed !== "object") {
            return false;
        }
        const parsed = row.parsed as Record<string, unknown>;
        if (typeof parsed.dateTime !== "string" || !Number.isFinite(new Date(parsed.dateTime).getTime()) ||
            ![parsed.fiscalDriveNumber, parsed.fiscalDocumentNumber, parsed.fiscalSign].every(field => typeof field === "string") ||
            (parsed.sum !== null && (typeof parsed.sum !== "number" || !Number.isFinite(parsed.sum))) ||
            (parsed.operationType !== null && (typeof parsed.operationType !== "number" || !Number.isSafeInteger(parsed.operationType)))) {
            return false;
        }
    }
    return true;
}

function isNullableString(value: unknown): boolean {
    return value === null || typeof value === "string";
}
