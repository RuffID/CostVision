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
import * as fileScanWorkflow from "./fileScanWorkflow.js";
import * as cameraController from "./cameraController.js";
import type { QrSubmitSource } from "./state.js";
import type { QrParsed, ReceiptStatusType } from "./types.js";
import { getOrCreateBootstrapModal } from "../../shared/bootstrap.js";

export function qrScanEnsureStatusContainer(): HTMLElement | null {
    if (document.body.classList.contains("qr-mobile-scan-mode")) {
        return null;
    }

    let container = document.querySelector<HTMLElement>(".qr-status");
    if (container) {
        return container;
    }

    container = document.createElement("div");
    container.className = "qr-status d-flex flex-column align-items-start gap-2 mt-3 mb-4";

    // Вставить над выбором счёта
    if (receiptPageState.accountSelect) {
        // Берём не сам select, а его "строку" / form-group, если есть
        let anchor = receiptPageState.accountSelect.closest(".form-group, .mb-3, .mb-4, .row") || receiptPageState.accountSelect;

        if (anchor && anchor.parentNode) {
            anchor.parentNode.insertBefore(container, anchor);
            return container;
        }
    }

    return container;
}

export function qrScanRenderStatus(scannedCount: number, addedToDbCount: number, errorCount: number, errorMessage: string, results: QrScanResult[]): void {
    const container = qrScanEnsureStatusContainer();
    if (!container) {
        return;
    }

    clearElement(container);

    const normalizedResults = Array.isArray(results) ? results : [];
    const invalidQrCount = normalizedResults.filter(function (result) {
        return !!result &&
            typeof result.decodedText === "string" &&
            result.decodedText.trim().length > 0 &&
            typeof result.errorMessage === "string" &&
            result.errorMessage.trim().length > 0;
    }).length;
    const duplicateCount = Math.max(0, scannedCount - addedToDbCount - invalidQrCount);

    if (addedToDbCount > 0) {
        const addedItem = document.createElement("div");
        addedItem.className = "qr-status__item alert alert-success py-2 px-3 mb-0 d-inline-block w-auto";

        const addedText = document.createElement("div");
        addedText.className = "qr-status__text mb-0";
        if (addedToDbCount === 1) {
            const addedReceipt = qrScanFindFirstSuccessfulParsedResult(normalizedResults);
            addedText.textContent = addedReceipt
                ? qrScanBuildAddedReceiptStatusText(addedReceipt.parsed)
                : "Добавлен чек";
        } else {
            addedText.textContent = "Добавлено чеков: " + addedToDbCount;
        }

        addedItem.appendChild(addedText);
        container.appendChild(addedItem);
    }

    if (duplicateCount > 0) {
        const duplicateItem = document.createElement("div");
        duplicateItem.className = "qr-status__item alert alert-warning py-2 px-3 mb-0 d-inline-block w-auto";

        const duplicateText = document.createElement("div");
        duplicateText.className = "qr-status__text mb-0";
        duplicateText.textContent = "Уже добавлено: " + duplicateCount;

        duplicateItem.appendChild(duplicateText);
        container.appendChild(duplicateItem);
    }

    if (errorCount > 0) {
        const errorItem = document.createElement("div");
        errorItem.className = "qr-status__item alert alert-danger py-2 px-3 mb-0 d-inline-block w-auto";

        const errorText = document.createElement("div");
        errorText.className = "qr-status__text mb-0";
        if (errorCount === 1) {
            const failedReceipt = qrScanFindFirstFailedParsedResult(normalizedResults);
            errorText.textContent = failedReceipt
                ? qrScanBuildFailedReceiptStatusText(failedReceipt.parsed)
                : "Не удалось добавить чек";
        } else {
            errorText.textContent = "Ошибок: " + errorCount;
        }

        errorItem.appendChild(errorText);
        container.appendChild(errorItem);
    }

    if (errorMessage && errorMessage.length > 0) {
        const err = document.createElement("div");
        err.className = "qr-status__item alert alert-danger py-2 px-3 mb-0 d-inline-block w-auto";

        const title = document.createElement("div");
        title.className = "qr-status__title fw-semibold";
        title.textContent = "Ошибка при сканировании";

        const text = document.createElement("div");
        text.className = "qr-status__text";
        text.textContent = errorMessage;

        err.appendChild(title);
        err.appendChild(text);
        container.appendChild(err);
    }
}

export function qrScanRenderManualStatus(message: string, statusType: ReceiptStatusType): void {
    const container = qrScanEnsureStatusContainer();
    if (!container) {
        return;
    }

    clearElement(container);

    const item = document.createElement("div");
    item.className = "qr-status__item alert py-2 px-3 mb-0 d-inline-block w-auto " + qrScanGetStatusClassName(statusType);

    const title = document.createElement("div");
    title.className = "qr-status__title fw-semibold";
    title.textContent = "Ручной ввод чека";

    const text = document.createElement("div");
    text.className = "qr-status__text";
    text.textContent = message;

    item.appendChild(title);
    item.appendChild(text);
    container.appendChild(item);
}

export function qrScanGetStatusClassName(statusType: ReceiptStatusType): string {
    return getStatusClassName(statusType);
}

export function qrScanRenderResults(results: QrScanResult[]): void {
    if (document.body.classList.contains("qr-mobile-scan-mode")) {
        const existingSection = document.getElementById("decoded-results-section");
        if (existingSection) {
            const wrapper = existingSection.closest(".qr-section");
            if (wrapper) {
                wrapper.remove();
            }
        }

        return;
    }

    if (!results || results.length === 0) {
        return;
    }

    let sectionBody = document.getElementById("decoded-results-section");
    let ul;

    if (!sectionBody) {
        const wrapper = document.createElement("div");
        wrapper.className = "qr-section rounded-4 border overflow-hidden w-100";
        wrapper.style.backgroundColor = "#F9FAFB";
        wrapper.style.borderColor = "#d1d5db";
        wrapper.style.borderRadius = "1rem";
        const headerBtn = document.createElement("button");
        headerBtn.type = "button";
        headerBtn.className = "qr-section__header btn w-100 d-flex align-items-center justify-content-between text-start px-4 py-3 border-0 rounded-top-4 bg-transparent fw-bold fs-6";
        headerBtn.setAttribute("data-collapse-target", "#decoded-results-section");
        headerBtn.setAttribute("aria-expanded", "true");
        headerBtn.style.backgroundColor = "transparent";
        headerBtn.style.border = "0";

        const headerTextSpan = document.createElement("span");
        headerTextSpan.textContent = "Результаты обработки";

        const headerArrowSpan = document.createElement("span");
        headerArrowSpan.className = "qr-section__arrow text-body-secondary fs-5";
        headerArrowSpan.setAttribute("aria-hidden", "true");
        headerArrowSpan.textContent = "▸";

        headerBtn.appendChild(headerTextSpan);
        headerBtn.appendChild(headerArrowSpan);

        sectionBody = document.createElement("div");
        sectionBody.id = "decoded-results-section";
        sectionBody.className = "qr-section__body card-body bg-body-tertiary";
        sectionBody.style.boxSizing = "border-box";
        sectionBody.style.width = "100%";
        sectionBody.style.backgroundColor = "#F9FAFB";
        sectionBody.style.borderTop = "1px solid #d1d5db";

        ul = document.createElement("ul");
        ul.className = "list-group list-group-flush bg-transparent";
        sectionBody.appendChild(ul);

        wrapper.appendChild(headerBtn);
        wrapper.appendChild(sectionBody);

        const resultsHost = document.getElementById("decoded-results-host");
        if (!resultsHost) {
            throw new Error("Не найден контейнер результатов обработки.");
        }

        resultsHost.appendChild(wrapper);
    } else {
        ul = sectionBody.querySelector("ul");
        if (!ul) {
            ul = document.createElement("ul");
            ul.className = "list-group list-group-flush bg-transparent";
            sectionBody.appendChild(ul);
        }
    }

    const resultsHeader = sectionBody.previousElementSibling;
    if (!(resultsHeader instanceof HTMLButtonElement)) {
        throw new Error("Не найден заголовок результатов обработки.");
    }

    initialization.qrScanSetCollapseState(resultsHeader, sectionBody, false);

    clearElement(ul);

    for (let i = 0; i < results.length; i++) {
        const r = results[i];
        const li = document.createElement("li");
        li.className = "list-group-item px-0 bg-transparent";

        const fileName = document.createElement("strong");
        fileName.textContent = r.fileName || "Файл";

        const headerSpan = document.createElement("span");
        headerSpan.className = "text-nowrap";

        let dateText = "Без даты";
        if (r.photoDateTime) {
            try {
                const dt = new Date(r.photoDateTime);
                if (!Number.isNaN(dt.getTime())) {
                    dateText = dt.toLocaleString();
                }
            } catch {
                // игнорировать ошибки парсинга даты
            }
        }

        const dateStrong = document.createElement("strong");
        dateStrong.textContent = dateText;

        headerSpan.appendChild(dateStrong);

        li.appendChild(fileName);
        li.appendChild(document.createTextNode(", "));
        li.appendChild(headerSpan);
        li.appendChild(document.createElement("br"));

        qrScanAppendProcessingResultDetails(li, r);

        ul.appendChild(li);
    }
}

export function createStrongText(text: string): HTMLElement {
    const element = document.createElement("strong");
    element.textContent = text;
    return element;
}

export function qrScanFindFirstSuccessfulParsedResult(results: QrScanResult[]): (QrScanResult & { parsed: QrParsed }) | undefined {
    return results.find(function (result) {
        return !!result && !result.errorMessage && !!result.parsed;
    }) as (QrScanResult & { parsed: QrParsed }) | undefined;
}

export function qrScanFindFirstFailedParsedResult(results: QrScanResult[]): (QrScanResult & { parsed: QrParsed }) | undefined {
    return results.find(function (result) {
        return !!result && !!result.errorMessage && !!result.parsed;
    }) as (QrScanResult & { parsed: QrParsed }) | undefined;
}

export function qrScanBuildAddedReceiptStatusText(parsed: QrParsed): string {
    return "[" + qrScanFormatReceiptDateForStatus(parsed.dateTime) + "] Добавлен чек на сумму: " + qrScanFormatReceiptSumForStatus(parsed.sum);
}

export function qrScanBuildFailedReceiptStatusText(parsed: QrParsed): string {
    return "Не удалось добавить чек на сумму: " + qrScanFormatReceiptSumForStatus(parsed.sum) + ", от " + qrScanFormatReceiptDateForStatus(parsed.dateTime);
}

export function qrScanAppendProcessingResultDetails(container: HTMLElement, result: QrScanResult): void {
    const statusDiv = document.createElement("div");
    statusDiv.className = result.errorMessage ? "fw-semibold text-danger" : "fw-semibold text-success";
    statusDiv.textContent = result.errorMessage
        ? "Не загружен: " + result.errorMessage
        : "Загружен";
    container.appendChild(statusDiv);

    if (!result.parsed) {
        return;
    }

    const parsedDiv = document.createElement("div");
    parsedDiv.className = "small text-body-secondary mt-1";
    parsedDiv.append(
        document.createTextNode("Чек от: "),
        createStrongText(qrScanFormatReceiptDateForStatus(result.parsed.dateTime)),
        document.createTextNode(", сумма: "),
        createStrongText(qrScanFormatReceiptSumForStatus(result.parsed.sum)),
        document.createTextNode(", ФН: "),
        createStrongText(result.parsed.fiscalDriveNumber || "не указан"),
        document.createTextNode(", ФД: "),
        createStrongText(result.parsed.fiscalDocumentNumber || "не указан"),
        document.createTextNode(", ФП: "),
        createStrongText(result.parsed.fiscalSign || "не указан")
    );

    container.appendChild(parsedDiv);
}

export function qrScanFormatReceiptDateForStatus(value: string | null): string {
    if (!value) {
        return "дата не указана";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    const day = String(date.getDate()).padStart(2, "0");
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const year = date.getFullYear();
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");

    return `${hours}:${minutes} ${day}.${month}.${year}`;
}

export function qrScanFormatReceiptSumForStatus(value: number | null): string {
    if (typeof value !== "number" || Number.isNaN(value)) {
        return "сумма не указана";
    }

    return new Intl.NumberFormat("ru-RU", {
        minimumFractionDigits: 1,
        maximumFractionDigits: 2
    }).format(value) + " ₽";
}

// ===================== Работа с результатами (Results) =====================

export function qrScanShowCameraReceiptFeedback(results: QrScanResult[], addedToDbCount: number, errorCount: number, errorMessage: string, source: QrSubmitSource): void {
    const receiptResult = Array.isArray(results) && results.length > 0 ? results[0] : null;
    const modal = qrScanEnsureCameraModal();
    const title = modal.querySelector<HTMLElement>(".qr-camera-modal__title");
    const subtitle = modal.querySelector<HTMLElement>(".qr-camera-modal__subtitle");
    const details = modal.querySelector<HTMLElement>(".qr-camera-modal__details");

    if (!title || !subtitle || !details) {
        return;
    }

    clearElement(details);
    title.classList.remove("text-success", "text-warning", "text-danger");

    if (!receiptResult) {
        title.textContent = "Сканирование завершено";
        title.classList.add("text-warning");
        subtitle.textContent = source === "files"
            ? "Файл обработан, но результат не найден."
            : "Данные чека получены.";
        qrScanAppendCameraModalDetail(details, "Статус", "Результат не найден");
    } else if (receiptResult.errorMessage) {
        title.textContent = source === "files" ? "Файл обработан с ошибкой" : "Чек считан с ошибкой";
        title.classList.add("text-danger");
        subtitle.textContent = receiptResult.errorMessage;
        qrScanAppendParsedReceiptDetails(details, receiptResult.parsed);
    } else {
        title.textContent = addedToDbCount > 0
            ? (receiptResult.parsed ? qrScanBuildAddedReceiptStatusText(receiptResult.parsed) : "Добавлен чек")
            : "Чек уже есть в системе";
        title.classList.add(addedToDbCount > 0 ? "text-success" : "text-warning");
        subtitle.textContent = addedToDbCount > 0
            ? (source === "files"
                ? "Файл успешно распознан. Проверьте данные чека."
                : "")
            : "Такой чек уже был добавлен ранее.";
        qrScanAppendParsedReceiptDetails(details, receiptResult.parsed);
    }

    if (errorCount > 0 && errorMessage) {
        qrScanAppendCameraModalDetail(details, "Ошибка", errorMessage);
    }

    qrScanShowCameraModal();
}

export function qrScanEnsureCameraModal(): HTMLElement {
    if (receiptPageState.qrScanCameraModal) {
        return receiptPageState.qrScanCameraModal;
    }

    const modal = document.createElement("div");
    modal.className = "qr-camera-modal modal fade";
    modal.tabIndex = -1;
    modal.setAttribute("aria-hidden", "true");

    const dialog = document.createElement("div");
    dialog.className = "modal-dialog modal-dialog-centered modal-dialog-scrollable";

    const content = document.createElement("div");
    content.className = "modal-content border-0 shadow";

    const header = document.createElement("div");
    header.className = "modal-header";

    const headingContainer = document.createElement("div");

    const title = document.createElement("div");
    title.id = "qr-camera-modal-title";
    title.className = "qr-camera-modal__title h4 mb-1";

    const subtitle = document.createElement("div");
    subtitle.className = "qr-camera-modal__subtitle text-body-secondary";

    headingContainer.append(title, subtitle);

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "btn-close";
    closeButton.dataset.bsDismiss = "modal";
    closeButton.setAttribute("aria-label", "Закрыть");

    header.append(headingContainer, closeButton);

    const body = document.createElement("div");
    body.className = "modal-body";

    const details = document.createElement("div");
    details.className = "qr-camera-modal__details d-grid gap-2";
    body.appendChild(details);

    const footer = document.createElement("div");
    footer.className = "modal-footer";

    const continueButton = document.createElement("button");
    continueButton.type = "button";
    continueButton.className = "btn btn-primary";
    continueButton.dataset.bsDismiss = "modal";
    continueButton.textContent = "Продолжить сканирование";
    footer.appendChild(continueButton);

    content.append(header, body, footer);
    dialog.appendChild(content);
    modal.appendChild(dialog);

    modal.addEventListener("hidden.bs.modal", function () {
        cameraController.qrScanResumeCameraAfterFeedback();
    });

    document.body.appendChild(modal);
    receiptPageState.qrScanCameraModal = modal;
    receiptPageState.qrScanCameraModalInstance = getOrCreateBootstrapModal(modal);
    return modal;
}

export function qrScanShowCameraModal(): void {
    qrScanEnsureCameraModal();

    if (!receiptPageState.qrScanCameraModalInstance) {
        throw new Error("Не удалось инициализировать модальное окно результата сканирования.");
    }

    if (receiptPageState.qrScanOverlay) {
        fileScanWorkflow.qrScanHideOverlay();
    }

    receiptPageState.qrScanCameraModalInstance.show();
}

export function qrScanHideCameraReceiptFeedback(): void {
    if (!receiptPageState.qrScanCameraModal || !receiptPageState.qrScanCameraModalInstance) {
        cameraController.qrScanResumeCameraAfterFeedback();
        return;
    }

    receiptPageState.qrScanCameraModalInstance.hide();
}

export function qrScanAppendParsedReceiptDetails(container: HTMLElement, parsed: QrParsed | null | undefined): void {
    if (!parsed) {
        qrScanAppendCameraModalDetail(container, "Данные", "Не удалось разобрать реквизиты чека");
        return;
    }

    qrScanAppendCameraModalDetail(container, "Сумма", qrScanFormatReceiptSum(parsed.sum));
    qrScanAppendCameraModalDetail(container, "Дата", qrScanFormatReceiptDate(parsed.dateTime));
    qrScanAppendCameraModalDetail(container, "ФН", parsed.fiscalDriveNumber || "Не указан");
    qrScanAppendCameraModalDetail(container, "ФД", parsed.fiscalDocumentNumber || "Не указан");
    qrScanAppendCameraModalDetail(container, "ФП", parsed.fiscalSign || "Не указан");
    qrScanAppendCameraModalDetail(container, "Тип", qrScanFormatOperationType(parsed.operationType));
}

export function qrScanAppendCameraModalDetail(container: HTMLElement, label: string, value: string): void {
    const item = document.createElement("div");
    item.className = "qr-camera-modal__detail border rounded-3 px-3 py-2 bg-body-tertiary";

    const labelElement = document.createElement("div");
    labelElement.className = "qr-camera-modal__detail-label small text-uppercase text-body-secondary fw-semibold";
    labelElement.textContent = label;

    const valueElement = document.createElement("div");
    valueElement.className = "qr-camera-modal__detail-value fw-semibold";
    valueElement.textContent = value;

    item.appendChild(labelElement);
    item.appendChild(valueElement);
    container.appendChild(item);
}

export function qrScanFormatReceiptSum(value: number | null): string {
    if (typeof value !== "number" || Number.isNaN(value)) {
        return "Не указана";
    }

    return new Intl.NumberFormat("ru-RU", {
        style: "currency",
        currency: "RUB",
        minimumFractionDigits: 2
    }).format(value);
}

export function qrScanFormatReceiptDate(value: string | null): string {
    if (!value) {
        return "Не указана";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    return date.toLocaleString("ru-RU");
}

export function qrScanFormatOperationType(value: number | null): string {
    switch (value) {
        case 1:
            return "Приход";
        case 2:
            return "Возврат прихода";
        case 3:
            return "Расход";
        case 4:
            return "Возврат расхода";
        default:
            return "Не указан";
    }
}
