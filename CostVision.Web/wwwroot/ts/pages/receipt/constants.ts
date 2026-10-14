export const CAMERA_STORAGE_KEY = "qrScan.selectedCameraId";

export function getReceiptStorageKeys(userId: string): { account: string; draft: string } {
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(userId) ||
        userId === "00000000-0000-0000-0000-000000000000") {
        throw new Error("Не определён текущий пользователь для черновика.");
    }
    const prefix = "qrScan.user." + userId.toLowerCase();
    return { account: prefix + ".selectedAccountId", draft: prefix + ".manualReceiptDraft" };
}
