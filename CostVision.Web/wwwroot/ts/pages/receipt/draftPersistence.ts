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
import { MANUAL_RECEIPT_DRAFT_STORAGE_KEY } from "./constants.js";

type ManualInput = HTMLInputElement | HTMLSelectElement;

export function qrScanRestoreManualDraft(fnInput: HTMLInputElement, fdInput: HTMLInputElement, fpInput: HTMLInputElement, sumInput: HTMLInputElement, dateInput: HTMLInputElement, typeSelect: HTMLSelectElement): void {
    try {
        const raw = localStorage.getItem(MANUAL_RECEIPT_DRAFT_STORAGE_KEY);
        if (!raw) {
            return;
        }

        const draft = JSON.parse(raw) as Record<string, unknown>;
        if (!draft || typeof draft !== "object") {
            return;
        }

        if (fnInput && typeof draft.fn === "string") {
            fnInput.value = draft.fn;
        }

        if (fdInput && typeof draft.fd === "string") {
            fdInput.value = draft.fd;
        }

        if (fpInput && typeof draft.fp === "string") {
            fpInput.value = draft.fp;
        }

        if (sumInput && typeof draft.sum === "string") {
            sumInput.value = draft.sum;
        }

        if (dateInput && typeof draft.date === "string") {
            dateInput.value = draft.date;
        }

        if (typeSelect && typeof draft.type === "string" && draft.type.length > 0) {
            typeSelect.value = draft.type;
        }
    } catch (error) {
        console.warn("QR scan: не удалось восстановить черновик ручного ввода", error);
    }
}

export function qrScanSetDefaultManualDate(dateInput: HTMLInputElement): void {
    if (!dateInput || dateInput.value) {
        return;
    }

    dateInput.value = formatLocalDateTimeInputValue(new Date());
}

export function formatLocalDateTimeInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = (date.getMonth() + 1).toString().padStart(2, "0");
    const day = date.getDate().toString().padStart(2, "0");
    const hours = date.getHours().toString().padStart(2, "0");
    const minutes = date.getMinutes().toString().padStart(2, "0");

    return `${year}-${month}-${day}T${hours}:${minutes}`;
}

export function qrScanBindManualDraftPersistence(fnInput: HTMLInputElement, fdInput: HTMLInputElement, fpInput: HTMLInputElement, sumInput: HTMLInputElement, dateInput: HTMLInputElement, typeSelect: HTMLSelectElement): void {
    const persist = function () {
        qrScanSaveManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
    };

    const inputs: ManualInput[] = [fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect];
    for (let i = 0; i < inputs.length; i++) {
        const element = inputs[i];
        if (!element) {
            continue;
        }

        element.addEventListener("input", persist);
        element.addEventListener("change", persist);
    }
}

export function qrScanSaveManualDraft(fnInput: HTMLInputElement, fdInput: HTMLInputElement, fpInput: HTMLInputElement, sumInput: HTMLInputElement, dateInput: HTMLInputElement, typeSelect: HTMLSelectElement): void {
    try {
        const draft = {
            fn: fnInput ? (fnInput.value || "") : "",
            fd: fdInput ? (fdInput.value || "") : "",
            fp: fpInput ? (fpInput.value || "") : "",
            sum: sumInput ? (sumInput.value || "") : "",
            date: dateInput ? (dateInput.value || "") : "",
            type: typeSelect ? (typeSelect.value || "") : ""
        };

        localStorage.setItem(MANUAL_RECEIPT_DRAFT_STORAGE_KEY, JSON.stringify(draft));
    } catch (error) {
        console.warn("QR scan: не удалось сохранить черновик ручного ввода", error);
    }
}

export function qrScanClearManualDraft(fnInput: HTMLInputElement, fdInput: HTMLInputElement, fpInput: HTMLInputElement, sumInput: HTMLInputElement, dateInput: HTMLInputElement, typeSelect: HTMLSelectElement): void {
    try {
        localStorage.removeItem(MANUAL_RECEIPT_DRAFT_STORAGE_KEY);
    } catch (error) {
        console.warn("QR scan: не удалось очистить черновик ручного ввода", error);
    }

    if (fnInput) {
        fnInput.value = "";
    }

    if (fdInput) {
        fdInput.value = "";
    }

    if (fpInput) {
        fpInput.value = "";
    }

    if (sumInput) {
        sumInput.value = "";
    }

    if (dateInput) {
        dateInput.value = "";
    }

    if (typeSelect) {
        typeSelect.value = "1";
    }
}
