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
import * as draftPersistence from "./draftPersistence.js";

export function initManualCheckValidation(): void {
    const root = requireElementById<HTMLElement>("manual-request-root");
    const btn = requireElementById<HTMLButtonElement>("manual-check-btn");
    const resultMessage = requireElementById<HTMLElement>("manual-result-message");

    const fnInput = requireInputById("manual-fn");
    const fdInput = requireInputById("manual-fd");
    const fpInput = requireInputById("manual-fp");
    const sumInput = requireInputById("manual-sum");
    const dateInput = requireInputById("manual-date");
    const typeSelect = requireSelectById("manual-type");

    draftPersistence.qrScanRestoreManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
    draftPersistence.qrScanSetDefaultManualDate(dateInput);
    draftPersistence.qrScanBindManualDraftPersistence(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);

    btn.addEventListener("click", async function () {
        const isValid = validateManualFields(
            root,
            fnInput,
            fdInput,
            fpInput,
            sumInput,
            dateInput,
            typeSelect,
            resultMessage
        );

        if (!isValid) {
            return;
        }

        const fnDigits = (fnInput?.value || "").replace(/\D/g, "");
        const fdDigits = (fdInput?.value || "").replace(/\D/g, "");
        const fpDigits = (fpInput?.value || "").replace(/\D/g, "");
        const sumRaw = (sumInput?.value || "").trim();
        const dateValue = (dateInput?.value || "").trim();
        const typeValue = (typeSelect?.value || "").trim();

        // сбрасывает локальное сообщение
        if (resultMessage) {
            resultMessage.textContent = "";
            resultMessage.classList.remove("text-danger", "text-success");
        }

        receiptPageState.qrScanLastSubmitSource = "manual";

        fileScanWorkflow.qrScanShowOverlay();
        try {
            const payload: ManualReceiptPayload = {
                receipt: {
                    fiscalDriveNumber: fnDigits,
                    fiscalDocumentNumber: fdDigits,
                    fiscalSign: fpDigits,
                    sum: parseFloat(sumRaw.replace(",", ".")),
                    dateTime: dateValue,
                    operationType: parseInt(typeValue, 10)
                },
                accountId: initialization.getSelectedAccountId()
            };

            const responseData = await submitManualReceiptAsync(payload, receiptPageState.antiForgeryToken);
            if (responseData.outcome === ManualReceiptOutcome.Created || responseData.outcome === ManualReceiptOutcome.AddedToAccount) {
                draftPersistence.qrScanClearManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
                const successMessage = responseData.outcome === ManualReceiptOutcome.AddedToAccount
                    ? responseData.message
                    : "Чек добавлен в систему";
                rendering.qrScanRenderManualStatus(successMessage, "success");
                if (resultMessage) {
                    resultMessage.textContent = successMessage;
                    resultMessage.classList.remove("text-danger");
                    resultMessage.classList.add("text-success");
                }
            } else {
                rendering.qrScanRenderManualStatus(responseData.message, "warning");
                if (resultMessage) {
                    resultMessage.textContent = responseData.message;
                    resultMessage.classList.remove("text-success");
                    resultMessage.classList.add("text-danger");
                }
            }
        } catch (e) {
            console.error("QR scan: ошибка ручного запроса", e);
            const errorMessage = e instanceof Error ? e.message : "Указаны некорректные данные чека";
            rendering.qrScanRenderManualStatus(errorMessage, "error");
            if (resultMessage) {
                resultMessage.textContent = errorMessage;
                resultMessage.classList.remove("text-success");
                resultMessage.classList.add("text-danger");
            }
        } finally {
            fileScanWorkflow.qrScanHideOverlay();
        }
    });
}

export function validateManualFields(root: HTMLElement, fnInput: HTMLInputElement, fdInput: HTMLInputElement, fpInput: HTMLInputElement, sumInput: HTMLInputElement, dateInput: HTMLInputElement, typeSelect: HTMLSelectElement, resultMessage: HTMLElement): boolean {
    clearManualFieldError(root, fnInput);
    clearManualFieldError(root, fdInput);
    clearManualFieldError(root, fpInput);
    clearManualFieldError(root, sumInput);
    clearManualFieldError(root, dateInput);
    clearManualFieldError(root, typeSelect);

    if (resultMessage) {
        resultMessage.textContent = "";
        resultMessage.classList.remove("text-danger", "text-success");
    }

    let hasError = false;

    const fnDigits = (fnInput?.value || "").replace(/\D/g, "");
    const fdDigits = (fdInput?.value || "").replace(/\D/g, "");
    const fpDigits = (fpInput?.value || "").replace(/\D/g, "");
    const sumRaw = (sumInput?.value || "").trim();
    const dateValue = (dateInput?.value || "").trim();
    const typeValue = (typeSelect?.value || "").trim();

    // ФН: строго 16 цифр
    if (!fnDigits || fnDigits.length !== 16) {
        setManualFieldError(root, fnInput, "ФН должен содержать 16 цифр");
        hasError = true;
    }

    // ФД: обязательно, 1–10 цифр
    if (!fdDigits || fdDigits.length > 10) {
        setManualFieldError(root, fdInput, "ФД должен содержать от 4 до 10 цифр");
        hasError = true;
    }

    // ФПД: обязательно, 8–10 цифр
    if (!fpDigits || fpDigits.length < 8 || fpDigits.length > 10) {
        setManualFieldError(root, fpInput, "ФПД должен содержать от 8 до 10 цифр");
        hasError = true;
    }

    // Сумма: обязательно, число (0 допустим)
    if (sumRaw === "" || Number.isNaN(parseFloat(sumRaw))) {
        setManualFieldError(root, sumInput, "Укажите сумму");
        hasError = true;
    }

    // Дата: обязательно
    if (!dateValue) {
        setManualFieldError(root, dateInput, "Укажите дату и время");
        hasError = true;
    }

    // Тип чека: обязательно
    // если добавишь в select пустую опцию с value="" или "0", это тоже отловится
    if (!typeValue || typeValue === "0") {
        setManualFieldError(root, typeSelect, "Выберите тип чека");
        hasError = true;
    }

    if (hasError) {
        return false;
    }

    return true;
}

export function setManualFieldError(root: HTMLElement, input: HTMLInputElement | HTMLSelectElement, message: string): void {
    if (!root || !input) {
        return;
    }

    const row = input.closest(".manual-row");
    if (!row) {
        return;
    }

    const label = row.querySelector("label");
    if (label) {
        label.classList.add("text-danger");
    }

    input.classList.add("is-invalid");

    let errorSpan = row.querySelector<HTMLElement>(".manual-field-error");
    if (!errorSpan) {
        errorSpan = document.createElement("div");
        errorSpan.className = "manual-field-error invalid-feedback d-block";
        row.appendChild(errorSpan);
    }

    errorSpan.textContent = message;
}

export function clearManualFieldError(root: HTMLElement, input: HTMLInputElement | HTMLSelectElement): void {
    if (!root || !input) {
        return;
    }

    const row = input.closest(".manual-row");
    if (!row) {
        return;
    }

    const label = row.querySelector("label");
    if (label) {
        label.classList.remove("text-danger");
    }

    input.classList.remove("is-invalid");

    const errorSpan = row.querySelector(".manual-field-error");
    if (errorSpan) {
        errorSpan.textContent = "";
    }
}

// ===================== Доп. функции =====================
