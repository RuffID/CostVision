import { submitQrScanAsync } from "./qrScanner.js";
import { submitManualReceiptAsync } from "./manualReceipt.js";
import type { ManualReceiptPayload, QrScanResult } from "./types.js";

// A write owns its captured data. No read AbortSignal or automatic retry.
export function submitScanPacket(url: string, accountId: string, rows: readonly QrScanResult[], token: string | null) {
    const results = rows.map(row => ({
        ...row,
        debugInfo: row.debugInfo ? { ...row.debugInfo } : row.debugInfo,
        parsed: row.parsed ? { ...row.parsed } : row.parsed
    }));
    return submitQrScanAsync(url, { accountId, results }, token);
}

export function submitManualPacket(payload: ManualReceiptPayload, token: string | null) {
    return submitManualReceiptAsync({ accountId: payload.accountId, receipt: { ...payload.receipt } }, token);
}
