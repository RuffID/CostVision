import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { clearElement, requireElementById, requireInputById, requireSelectById } from "../../shared/dom.js";
import { initFileDropzone } from "../../shared/dropzone.js";
import { loadReceiptAccountsAsync } from "./accountsApi.js";
import { setCameraButtonState } from "./cameraUi.js";
import { getFilesFromFileList } from "./fileScanner.js";
import { isImageFile } from "./imageProcessing.js";
import { submitManualReceiptAsync } from "./manualReceipt.js";
import { getStatusClassName } from "./receiptStatusRender.js";
import { submitQrScanAsync } from "./qrScanner.js";
import { DecodedQrFileResult, ImageDebugInfo, ImageSize, ManualReceiptOutcome, ManualReceiptPayload, QrScanPayload, QrScanResult } from "./types.js";
import { receiptPageState } from "./state.js";
import * as initialization from "./initialization.js";
import * as rendering from "./rendering.js";
import * as fileScanWorkflow from "./fileScanWorkflow.js";
import * as cameraController from "./cameraController.js";
import type { QrScanServerResponse } from "./types.js";

export async function qrScanSubmitFormAjax(): Promise<void> {
    if (!receiptPageState.qrScanForm) {
        return;
    }

    const url = receiptPageState.qrScanForm.getAttribute("action") || window.location.href;

    try {
        const payload: QrScanPayload = {
            accountId: initialization.getSelectedAccountId(),
            results: receiptPageState.qrScanResults
        };

        const data = await submitQrScanAsync(url, payload, receiptPageState.antiForgeryToken);
        qrScanApplyServerResponse(data);
    } catch (err) {
        console.error("QR scan: ошибка AJAX-запроса", err);
        cameraController.qrScanResumeCameraAfterFeedback();
    } finally {
        // любой поток, который инициировал запрос (drag&drop, выбор файла, Ctrl+V, камера, ручной ввод через AJAX)
        // должен снять оверлей здесь
        fileScanWorkflow.qrScanHideOverlay();
    }
}

export function qrScanApplyServerResponse(responseData: QrScanServerResponse): void {
    const scannedCount = typeof responseData.scannedCount === "number" ? responseData.scannedCount : 0;
    const addedToDbCount = typeof responseData.addedToDbCount === "number" ? responseData.addedToDbCount : 0;
    const errorCount = typeof responseData.errorCount === "number" ? responseData.errorCount : 0;
    const errorMessage = typeof responseData.errorMessage === "string" ? responseData.errorMessage : "";
    const results = Array.isArray(responseData.results) ? responseData.results : [];

    rendering.qrScanRenderStatus(scannedCount, addedToDbCount, errorCount, errorMessage, results);
    rendering.qrScanRenderResults(results);

    if (document.body.classList.contains("qr-mobile-scan-mode") &&
        (receiptPageState.qrScanLastSubmitSource === "camera" || receiptPageState.qrScanLastSubmitSource === "files")) {
        rendering.qrScanShowCameraReceiptFeedback(results, addedToDbCount, errorCount, errorMessage, receiptPageState.qrScanLastSubmitSource);
    }
}
