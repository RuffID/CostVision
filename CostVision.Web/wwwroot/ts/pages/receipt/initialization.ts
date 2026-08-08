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
import * as accounts from "./accounts.js";
import * as submission from "./submission.js";
import * as fileScanWorkflow from "./fileScanWorkflow.js";
import * as cameraController from "./cameraController.js";
import { ACCOUNT_STORAGE_KEY } from "./constants.js";

export async function initQrScan(): Promise<void> {
    receiptPageState.qrScanForm = requireElementById<HTMLFormElement>("qrForm");
    receiptPageState.qrScanResultsContainer = requireElementById<HTMLElement>("decoded-inputs");
    receiptPageState.qrScanFileInput = requireInputById("multiFileInput");
    receiptPageState.qrScanDropzone = requireElementById<HTMLElement>("qr-dropzone");

    const qrReaderElement = requireElementById<HTMLElement>("qr-reader");
    const fileScanRootElement = requireElementById<HTMLElement>("file-scan-root");
    receiptPageState.accountSelect = requireSelectById("accountSelect");
    receiptPageState.accountSelectError = requireElementById<HTMLElement>("accountSelectError");
    receiptPageState.antiForgeryToken = getRequestVerificationToken();

    const mobile = cameraController.isMobileDevice();

    if (receiptPageState.accountSelect) {
        // при смене — сохранять выбор
        receiptPageState.accountSelect.addEventListener("change", function () {
            localStorage.setItem(ACCOUNT_STORAGE_KEY, receiptPageState.accountSelect.value);
        });

        await accounts.loadAvailableAccountsAsync();
    }

    // Перехватываем submit формы, чтобы всегда работать через AJAX
    receiptPageState.qrScanForm.addEventListener("submit", function (e) {
        e.preventDefault();
        submission.qrScanSubmitFormAjax();
    });

    // ====== Камера: только мобильные ======
    if (mobile && qrReaderElement) {
        document.body.classList.add("qr-mobile-scan-mode");
        if (receiptPageState.qrScanDropzone) {
            receiptPageState.qrScanDropzone.closest(".qr-upload-block")?.classList.add("d-none");
        }
        await cameraController.initQrScanMobileScannerUiAsync(qrReaderElement);
    } else if (qrReaderElement) {
        // На ПК виджет камеры не показываем вообще
        qrReaderElement.style.display = "none";
    }

    // ====== Сканер файлов (общий для всех устройств) ======
    receiptPageState.qrScanFileScanner = new Html5Qrcode("file-scan-root");

    // Инициализация drag-and-drop холста
    if (receiptPageState.qrScanDropzone)
        fileScanWorkflow.initQrScanDropzone();
}

export function qrScanSetCollapseState(button: HTMLButtonElement, body: HTMLElement, isCollapsed: boolean): void {
    if (!button || !body) {
        return;
    }

    button.classList.toggle("qr-section__header--collapsed", isCollapsed);
    button.setAttribute("aria-expanded", (!isCollapsed).toString());
    body.classList.toggle("qr-section__body--collapsed", isCollapsed);
    body.classList.toggle("d-none", isCollapsed);

    const arrow = button.querySelector(".qr-section__arrow");
    if (arrow) {
        arrow.textContent = isCollapsed ? "▸" : "▾";
    }
}

// ===================== AJAX-отправка формы =====================

export function hasOption(select: HTMLSelectElement, value: string): boolean {
    for (let i = 0; i < select.options.length; i++) {
        if (select.options[i].value === value) {
            return true;
        }
    }
    return false;
}

export function getSelectedAccountId(): string {
    if (!receiptPageState.accountSelect || receiptPageState.accountSelect.disabled || !receiptPageState.accountSelect.value) {
        throw new Error("Счёт для сохранения чека не выбран.");
    }

    return receiptPageState.accountSelect.value;
}

// ===================== Глобальная обработка Ctrl+V =====================
export function initGlobalPasteHandler(): void {
    // любой Ctrl+V на странице (кроме ввода в инпуты/textarea/contentEditable)
    document.addEventListener("paste", fileScanWorkflow.qrScanHandlePaste);
}

// ===================== Сворачиваемые секции (ручной ввод / результаты) =====================
export function initCollapseSections(): void {
    document.addEventListener("click", function (e) {
        if (!(e.target instanceof Element)) {
            return;
        }

        const btn = e.target.closest<HTMLButtonElement>(".qr-section__header");
        if (!btn) return;

        const targetSelector = btn.getAttribute("data-collapse-target");
        if (!targetSelector) return;

        const body = document.querySelector<HTMLElement>(targetSelector);
        if (!body) return;

        const isCollapsed = !btn.classList.contains("qr-section__header--collapsed");
        qrScanSetCollapseState(btn, body, isCollapsed);
    });
}
