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
import * as submission from "./submission.js";
import * as cameraController from "./cameraController.js";
import * as fileDecoding from "./fileDecoding.js";

export function qrScanClearResults(): void {
    if (receiptPageState.qrScanResultsContainer) {
        clearElement(receiptPageState.qrScanResultsContainer);
    }

    receiptPageState.qrScanResults = [];
    receiptPageState.qrScanResultIndex = 0;
}

export function qrScanAddResult(fileName: string, decodedText: string | null, errorMessage: string | null, debugInfo: ImageDebugInfo | null, photoDateTimeIso: string | null): void {
    const result: QrScanResult = {
        fileName: fileName || "",
        decodedText: decodedText || "",
        errorMessage: errorMessage || null,
        debugInfo: debugInfo || null,
        photoDateTime: photoDateTimeIso || null
    }

    receiptPageState.qrScanResults.push(result);
    receiptPageState.qrScanResultIndex = receiptPageState.qrScanResults.length;
}

// ===================== Камера =====================
export function qrScanOnScanSuccess(decodedText: string, decodedResult: Html5QrcodeDecodedResult): void {
    // анти-спам: если тот же самый текст уже сканили недавно — игнорируем
    const now = Date.now();
    const debounceMs = 5000; // окно, в течение которого повтор одного и того же текста не уходит на сервер

    if (receiptPageState.qrScanLastCameraText === decodedText && (now - receiptPageState.qrScanLastCameraTs) < debounceMs) {
        return;
    }

    receiptPageState.qrScanLastCameraText = decodedText;
    receiptPageState.qrScanLastCameraTs = now;

    qrScanClearResults();
    receiptPageState.qrScanLastSubmitSource = "camera";
    cameraController.qrScanPauseCameraForFeedback();

    const debugInfo = {
        fileName: "CAMERA",
        contentType: "camera",
        fileSizeBytes: 0,
        width: 0,
        height: 0
    };

    qrScanAddResult("CAMERA", decodedText, "", debugInfo, null);

    // один запрос на сервер на один успешный скан
    submission.qrScanSubmitFormAjax();
}

export function qrScanOnScanError(errorMessage: string): void {
    // игнорировать ошибки сканирования
    // console.warn("QR scan error:", errorMessage);
}

// ===================== Файлы (множественный выбор и DnD) =====================
export async function qrScanScanFilesAndSubmit(fileList: FileList | File[]): Promise<void> {
    qrScanClearResults();
    receiptPageState.qrScanLastSubmitSource = "files";

    const files = Array.isArray(fileList) ? fileList : getFilesFromFileList(fileList);

    for (let i = 0; i < files.length; i++) {
        const file = files[i];

        const baseDebug = {
            fileName: file.name,
            contentType: file.type || "",
            fileSizeBytes: file.size || 0,
            width: 0,
            height: 0
        };

        let sizeInfo: ImageSize | null = null;
        try {
            sizeInfo = await fileDecoding.getImageSizeFromFile(file);
            baseDebug.width = sizeInfo.width;
            baseDebug.height = sizeInfo.height;
        } catch (e) {
            console.warn(`Не удалось определить размер для ${file.name}`, e);
        }

        let photoDateTimeIso: string | null = null;
        try {
            photoDateTimeIso = (await fileDecoding.qrScanGetPhotoDateFromFile(file)) || new Date(file.lastModified).toISOString();
        } catch (e) {
            console.warn(`Не удалось прочитать EXIF для ${file.name}`, e);
        }

        try {
            const result = await fileDecoding.qrScanDecodeFile(file);
            qrScanAddResult(file.name, result.decodedText, "", baseDebug, photoDateTimeIso);
        } catch (err) {
            console.warn(`Файл ${file.name} не распознан:`, err);
            qrScanAddResult(file.name, "", "Не удалось распознать QR-код", baseDebug, photoDateTimeIso);
        }
    }

    try {
        await receiptPageState.qrScanFileScanner.clear();
    } catch (e) {
        console.warn("Не удалось очистить fileScanner", e);
    }

    if (receiptPageState.qrScanResultIndex > 0) {
        submission.qrScanSubmitFormAjax();
    } else {
        console.warn("Ни один файл не содержит читаемого кода");
        // если до этого показывали оверлей – скрыть его
        qrScanHideOverlay();
    }
}

// ===================== Drag-and-drop холст =====================

export function initQrScanDropzone(): void {
    initFileDropzone({
        dropzone: receiptPageState.qrScanDropzone,
        fileInput: receiptPageState.qrScanFileInput,
        multiple: true,
        onFilesSelected: function (files) {
            qrScanShowOverlay();
            qrScanScanFilesAndSubmit(files);
        }
    });
}

export function qrScanHandlePaste(event: ClipboardEvent): void {
    const clipboardData = event.clipboardData;
    if (!clipboardData || !clipboardData.items) {
        return;
    }

    // не перехватывать вставку в текстовые поля / textarea / contentEditable
    const target = event.target instanceof HTMLElement ? event.target : null;
    const tagName = target?.tagName.toUpperCase() ?? "";
    const isEditable =
        tagName === "INPUT" ||
        tagName === "TEXTAREA" ||
        (target && target.isContentEditable);

    if (isEditable) {
        // даём обычной вставке отработать
        return;
    }

    const files: File[] = [];

    for (let i = 0; i < clipboardData.items.length; i++) {
        const item = clipboardData.items[i];
        if (item.kind === "file") {
            const file = item.getAsFile();
            if (file) {
                files.push(file);
            }
        }
    }

    if (files.length === 0) {
        return;
    }

    event.preventDefault();
    event.stopPropagation();

    // запускаем обработку вставленных файлов с оверлеем
    qrScanShowOverlay();
    qrScanScanFilesAndSubmit(files);
}

// ===================== Оверлей загрузки =====================
export function qrScanEnsureOverlay(): HTMLElement {
    if (receiptPageState.qrScanOverlay) {
        return receiptPageState.qrScanOverlay;
    }

    const overlay = document.createElement("div");
    overlay.className = "qr-overlay position-fixed top-0 start-0 w-100 h-100 d-none align-items-center justify-content-center flex-column gap-3";
    overlay.style.backgroundColor = "rgba(15, 23, 42, 0.55)";
    overlay.style.zIndex = "1080";

    const spinner = document.createElement("div");
    spinner.className = "spinner-border text-light";
    spinner.setAttribute("role", "status");
    spinner.setAttribute("aria-hidden", "true");

    const text = document.createElement("div");
    text.className = "qr-overlay__text text-white fw-semibold";
    text.textContent = "Обработка данных...";

    overlay.append(spinner, text);

    document.body.appendChild(overlay);
    receiptPageState.qrScanOverlay = overlay;
    return overlay;
}

export function qrScanShowOverlay(): void {
    const overlay = qrScanEnsureOverlay();
    overlay.classList.remove("d-none");
    overlay.classList.add("d-flex");
    document.body.classList.add("overflow-hidden");
}

export function qrScanHideOverlay(): void {
    if (!receiptPageState.qrScanOverlay) {
        return;
    }

    receiptPageState.qrScanOverlay.classList.add("d-none");
    receiptPageState.qrScanOverlay.classList.remove("d-flex");
    document.body.classList.remove("overflow-hidden");
}

// ===================== Ручной ввод: валидация полей =====================
