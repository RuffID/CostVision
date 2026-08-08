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
import * as fileScanWorkflow from "./fileScanWorkflow.js";
import { CAMERA_STORAGE_KEY } from "./constants.js";

export function isMobileDevice(): boolean {
    try {
        const nav = typeof navigator !== "undefined" ? navigator : null;
        const win = typeof window !== "undefined" ? window : null;

        if (nav) {
            if (typeof nav.maxTouchPoints === "number" && nav.maxTouchPoints > 1) {
                return true;
            }

            const ua = nav.userAgent || "";
            if (/Android|iPhone|iPad|iPod/i.test(ua)) {
                return true;
            }
        }

        if (win && win.matchMedia) {
            const mq = win.matchMedia("(pointer: coarse)");
            if (mq && mq.matches) {
                return true;
            }
        }
    } catch (e) {
        // Игнорировать ошибки окружения
    }

    return false;
}

export function qrScanPauseCameraForFeedback(): void {
    if (!receiptPageState.qrScanHtml5QrcodeScanner || receiptPageState.qrScanCameraPaused) {
        return;
    }

    try {
        receiptPageState.qrScanHtml5QrcodeScanner.pause(true);
        receiptPageState.qrScanCameraPaused = true;
    } catch (error) {
        console.warn("QR scan: не удалось поставить камеру на паузу", error);
    }
}

export function qrScanResumeCameraAfterFeedback(): void {
    if (!receiptPageState.qrScanHtml5QrcodeScanner || !receiptPageState.qrScanCameraPaused) {
        return;
    }

    try {
        receiptPageState.qrScanHtml5QrcodeScanner.resume();
        receiptPageState.qrScanCameraPaused = false;
    } catch (error) {
        console.warn("QR scan: не удалось возобновить камеру", error);
    }
}

export async function initQrScanMobileScannerUiAsync(qrReaderElement: HTMLElement): Promise<void> {
    if (!qrReaderElement) {
        return;
    }

    if (qrReaderElement.dataset.mobileUiInitialized === "1" && receiptPageState.qrScanHtml5QrcodeScanner) {
        return;
    }

    qrReaderElement.dataset.mobileUiInitialized = "1";
    qrReaderElement.className = "qr-reader-shell card shadow-lg border overflow-hidden";
    qrReaderElement.style.borderRadius = "2rem";
    qrReaderElement.style.borderColor = "#d9e2ec";
    qrReaderElement.style.backgroundColor = "#ffffff";
    qrReaderElement.style.boxShadow = "0 24px 48px rgba(15, 23, 42, 0.10)";
    qrReaderElement.replaceChildren();

    const panel = document.createElement("div");
    panel.className = "qr-reader-panel";

    const scanRegion = document.createElement("div");
    scanRegion.className = "qr-reader-panel__scan-region card-body";
    scanRegion.style.backgroundColor = "#ffffff";
    scanRegion.style.padding = "1.5rem 1.5rem 1rem 1.5rem";

    const viewport = document.createElement("div");
    viewport.id = "qr-reader-viewport";
    viewport.className = "qr-reader-panel__viewport rounded-4 overflow-hidden border-0";
    viewport.style.minHeight = "340px";
    viewport.style.position = "relative";
    viewport.style.backgroundColor = "#dbeafe";
    viewport.style.borderRadius = "1.75rem";
    scanRegion.appendChild(viewport);

    const hint = document.createElement("div");
    hint.className = "qr-reader-panel__hint text-center mt-3";
    hint.style.color = "#4b5563";
    hint.style.fontSize = "1rem";
    hint.textContent = "Наведите камеру на QR-код чека";
    scanRegion.appendChild(hint);

    const dashboard = document.createElement("div");
    dashboard.className = "qr-reader-panel__dashboard card-body pt-0";
    dashboard.style.padding = "0 1.5rem 1.5rem 1.5rem";

    const header = document.createElement("div");
    header.className = "qr-reader-panel__header d-flex flex-column gap-1";

    const title = document.createElement("div");
    title.className = "qr-reader-panel__title mb-0";
    title.style.fontSize = "1rem";
    title.style.fontWeight = "700";
    title.style.lineHeight = "1.1";
    title.style.color = "#111827";
    title.textContent = "Сканирование чека";
    header.appendChild(title);

    const status = document.createElement("div");
    status.className = "qr-reader-panel__status";
    status.style.fontSize = "1rem";
    status.style.color = "#6b7280";
    status.style.minHeight = "1.75rem";
    status.textContent = "Запрашиваем доступ к камере...";
    header.appendChild(status);

    dashboard.appendChild(header);

    const controls = document.createElement("div");
    controls.className = "qr-reader-panel__controls d-grid gap-3 mt-3";

    const cameraGroup = document.createElement("div");
    cameraGroup.className = "qr-reader-panel__camera-group d-grid gap-2";

    const cameraLabel = document.createElement("label");
    cameraLabel.className = "qr-reader-panel__label form-label fw-semibold mb-0";
    cameraLabel.style.fontSize = "1rem";
    cameraLabel.style.color = "#374151";
    cameraLabel.htmlFor = "qr-reader-camera-select";
    cameraLabel.textContent = "Камера";
    cameraGroup.appendChild(cameraLabel);

    const cameraSelect = document.createElement("select");
    cameraSelect.id = "qr-reader-camera-select";
    cameraSelect.name = "qr-reader-camera-select";
    cameraSelect.className = "qr-reader-panel__select form-select";
    cameraSelect.disabled = true;
    cameraSelect.style.minHeight = "2.85rem";
    cameraSelect.style.borderRadius = "1rem";
    cameraSelect.style.fontSize = "0.95rem";
    cameraSelect.style.paddingLeft = "1rem";
    cameraSelect.style.borderColor = "#d1d5db";
    cameraSelect.style.boxShadow = "0 4px 16px rgba(15, 23, 42, 0.06)";
    cameraSelect.addEventListener("change", function () {
        localStorage.setItem(CAMERA_STORAGE_KEY, cameraSelect.value);
        qrScanStartCameraByIdAsync(cameraSelect.value).catch(function (error) {
            qrScanUpdateCameraStatus(error && error.message ? error.message : "Не удалось переключить камеру.", true);
        });
    });
    cameraGroup.appendChild(cameraSelect);

    controls.appendChild(cameraGroup);

    const buttonGroup = document.createElement("div");
    buttonGroup.className = "qr-reader-panel__button-group d-grid gap-2";

    const startButton = document.createElement("button");
    startButton.type = "button";
    startButton.className = "qr-reader-panel__button btn btn-primary";
    startButton.style.minHeight = "2.75rem";
    startButton.style.borderRadius = "1rem";
    startButton.style.background = "linear-gradient(135deg, #0f766e 0%, #0f5d78 100%)";
    startButton.style.border = "0";
    startButton.style.fontSize = "0.95rem";
    startButton.style.fontWeight = "700";
    startButton.style.boxShadow = "0 14px 28px rgba(15, 118, 110, 0.24)";
    startButton.textContent = "Сканировать камерой";
    startButton.addEventListener("click", function () {
        const cameraId = cameraSelect.value || qrScanGetPreferredCameraId(receiptPageState.qrScanAvailableCameras);
        if (!cameraId) {
            qrScanUpdateCameraStatus("Камера не найдена.", true);
            return;
        }

        qrScanStartCameraByIdAsync(cameraId).catch(function (error) {
            qrScanUpdateCameraStatus(error instanceof Error ? error.message : "Не удалось запустить камеру.", true);
        });
    });
    buttonGroup.appendChild(startButton);

    const stopButton = document.createElement("button");
    stopButton.type = "button";
    stopButton.className = "qr-reader-panel__button btn btn-outline-secondary";
    stopButton.style.minHeight = "2.75rem";
    stopButton.style.borderRadius = "1rem";
    stopButton.style.fontSize = "0.95rem";
    stopButton.style.fontWeight = "600";
    stopButton.style.border = "0";
    stopButton.style.backgroundColor = "#eef2ff";
    stopButton.style.color = "#6b7280";
    stopButton.textContent = "Остановить";
    stopButton.addEventListener("click", function () {
        qrScanStopCameraAsync().catch(function (error) {
            qrScanUpdateCameraStatus(error && error.message ? error.message : "Не удалось остановить камеру.", true);
        });
    });
    buttonGroup.appendChild(stopButton);

    const galleryButton = document.createElement("button");
    galleryButton.type = "button";
    galleryButton.className = "qr-reader-panel__button btn btn-outline-primary";
    galleryButton.style.minHeight = "2.75rem";
    galleryButton.style.borderRadius = "1rem";
    galleryButton.style.fontSize = "0.95rem";
    galleryButton.style.fontWeight = "700";
    galleryButton.style.borderColor = "#d1d5db";
    galleryButton.style.backgroundColor = "#ffffff";
    galleryButton.style.color = "#0f5d78";
    galleryButton.style.boxShadow = "0 10px 24px rgba(15, 23, 42, 0.06)";
    galleryButton.textContent = "Выбрать из галереи";
    galleryButton.addEventListener("click", function () {
        if (receiptPageState.qrScanFileInput) {
            receiptPageState.qrScanFileInput.click();
        }
    });
    buttonGroup.appendChild(galleryButton);

    controls.appendChild(buttonGroup);
    dashboard.appendChild(controls);
    panel.appendChild(scanRegion);
    panel.appendChild(dashboard);
    qrReaderElement.appendChild(panel);

    qrReaderElement.dataset.cameraStatusElementId = "";
    qrReaderElement.dataset.cameraStartButtonId = "";
    qrReaderElement.dataset.cameraStopButtonId = "";

    status.id = "qr-reader-camera-status";
    startButton.id = "qr-reader-camera-start";
    stopButton.id = "qr-reader-camera-stop";
    qrReaderElement.dataset.cameraStatusElementId = status.id;
    qrReaderElement.dataset.cameraStartButtonId = startButton.id;
    qrReaderElement.dataset.cameraStopButtonId = stopButton.id;
    qrScanSetCameraButtonsState(false);

    receiptPageState.qrScanHtml5QrcodeScanner = new Html5Qrcode("qr-reader-viewport");

    try {
        receiptPageState.qrScanAvailableCameras = await qrScanLoadAvailableCamerasAsync();
        qrScanRenderCameraOptions(cameraSelect, receiptPageState.qrScanAvailableCameras);

        const preferredCameraId = qrScanGetPreferredCameraId(receiptPageState.qrScanAvailableCameras);
        if (preferredCameraId) {
            cameraSelect.value = preferredCameraId;
            await qrScanStartCameraByIdAsync(preferredCameraId);
            return;
        }

        qrScanUpdateCameraStatus("Камера не найдена.", true);
    } catch (error) {
        qrScanUpdateCameraStatus(error instanceof Error ? error.message : "Не удалось получить доступ к камере.", true);
    }
}

export async function qrScanLoadAvailableCamerasAsync(): Promise<Html5QrcodeCamera[]> {
    const cameras = await Html5Qrcode.getCameras();
    if (!Array.isArray(cameras) || cameras.length === 0) {
        return [];
    }

    const normalizedCameras = cameras.map(function (camera, index) {
        return {
            id: camera.id,
            label: qrScanNormalizeCameraLabel(camera.label, index + 1)
        };
    });

    const backCameras = normalizedCameras.filter(function (camera) {
        return qrScanIsBackCamera(camera.label);
    });

    const camerasForList = backCameras.length > 0
        ? backCameras.filter(function (camera) { return !qrScanIsFrontCamera(camera.label); })
        : normalizedCameras;

    return camerasForList.sort(function (left, right) {
        return qrScanGetCameraPriority(right.label) - qrScanGetCameraPriority(left.label);
    });
}

export function qrScanRenderCameraOptions(select: HTMLSelectElement, cameras: Html5QrcodeCamera[]): void {
    if (!select) {
        return;
    }

    select.replaceChildren();

    const availableCameras = Array.isArray(cameras) ? cameras : [];
    for (let i = 0; i < availableCameras.length; i++) {
        const camera = availableCameras[i];
        const option = document.createElement("option");
        option.value = camera.id;
        option.textContent = camera.label;
        select.appendChild(option);
    }

    select.disabled = availableCameras.length === 0;
}

export function qrScanNormalizeCameraLabel(label: string, fallbackIndex: number): string {
    const value = (label || "").trim();
    if (!value) {
        return `Камера ${fallbackIndex}`;
    }

    if (/anonymous camera/i.test(value)) {
        return `Камера ${fallbackIndex}`;
    }

    return value
        .replace(/camera/gi, "Камера")
        .replace(/back/gi, "задняя")
        .replace(/rear/gi, "задняя")
        .replace(/front/gi, "фронтальная")
        .replace(/facing/gi, "")
        .replace(/\s+/g, " ")
        .trim();
}

export function qrScanIsFrontCamera(label: string): boolean {
    return /front|user|selfie|frontal|фронт|перед/i.test(label || "");
}

export function qrScanIsBackCamera(label: string): boolean {
    return /back|rear|environment|world|задн|тыл/i.test(label || "");
}

export function qrScanGetCameraPriority(label: string): number {
    const value = (label || "").toLowerCase();
    let priority = 0;

    if (qrScanIsBackCamera(value)) {
        priority += 200;
    }

    if (/main|wide|1x|standard|default|основ/i.test(value)) {
        priority += 80;
    }

    if (/ultra|macro|tele|zoom|depth|0\.5x|2x/i.test(value)) {
        priority -= 40;
    }

    if (qrScanIsFrontCamera(value)) {
        priority -= 200;
    }

    return priority;
}

export function qrScanGetPreferredCameraId(cameras: Html5QrcodeCamera[]): string | null {
    if (!Array.isArray(cameras) || cameras.length === 0) {
        return null;
    }

    const savedCameraId = localStorage.getItem(CAMERA_STORAGE_KEY);
    if (savedCameraId && cameras.some(function (camera) { return camera.id === savedCameraId; })) {
        return savedCameraId;
    }

    return cameras[0].id;
}

export function qrScanGetMobileCameraElements(): { statusElement: HTMLElement | null; startButton: HTMLButtonElement | null; stopButton: HTMLButtonElement | null } {
    const qrReaderElement = document.getElementById("qr-reader");
    if (!qrReaderElement) {
        return {
            statusElement: null,
            startButton: null,
            stopButton: null
        };
    }

    const statusElementId = qrReaderElement.dataset.cameraStatusElementId || "";
    const startButtonId = qrReaderElement.dataset.cameraStartButtonId || "";
    const stopButtonId = qrReaderElement.dataset.cameraStopButtonId || "";

    return {
        statusElement: statusElementId ? document.getElementById(statusElementId) : null,
        startButton: startButtonId ? document.getElementById(startButtonId) as HTMLButtonElement | null : null,
        stopButton: stopButtonId ? document.getElementById(stopButtonId) as HTMLButtonElement | null : null
    };
}

export function qrScanUpdateCameraStatus(message: string, isError: boolean): void {
    const elements = qrScanGetMobileCameraElements();
    if (!elements.statusElement) {
        return;
    }

    elements.statusElement.textContent = message;
    elements.statusElement.classList.toggle("text-danger", !!isError);
    elements.statusElement.classList.toggle("text-body-secondary", !isError);
    elements.statusElement.style.color = isError ? "#b91c1c" : "#6b7280";
}

export function qrScanSetCameraButtonsState(isRunning: boolean): void {
    const elements = qrScanGetMobileCameraElements();
    setCameraButtonState(elements.startButton, !!isRunning);
    setCameraButtonState(elements.stopButton, !isRunning);
}

export function qrScanBuildCameraConfig(): Html5QrcodeConfig {
    return {
        fps: 10,
        aspectRatio: 1.3333333,
        qrbox: function (viewfinderWidth: number, viewfinderHeight: number) {
            const minEdge = Math.min(viewfinderWidth, viewfinderHeight);
            const boxSize = Math.max(180, Math.min(280, Math.floor(minEdge * 0.72)));
            return {
                width: boxSize,
                height: boxSize
            };
        }
    };
}

export async function qrScanStartCameraByIdAsync(cameraId: string): Promise<void> {
    if (!receiptPageState.qrScanHtml5QrcodeScanner) {
        throw new Error("Сканер камеры не инициализирован.");
    }

    const targetCameraId = cameraId || qrScanGetPreferredCameraId(receiptPageState.qrScanAvailableCameras);
    if (!targetCameraId) {
        throw new Error("Не найдена подходящая камера.");
    }

    qrScanUpdateCameraStatus("Открываем камеру...", false);

    const scannerState = typeof receiptPageState.qrScanHtml5QrcodeScanner.getState === "function"
        ? receiptPageState.qrScanHtml5QrcodeScanner.getState()
        : Html5QrcodeScannerState.NOT_STARTED;

    if (scannerState !== Html5QrcodeScannerState.NOT_STARTED) {
        await qrScanStopCameraAsync();
    }

    await receiptPageState.qrScanHtml5QrcodeScanner.start(
        targetCameraId,
        qrScanBuildCameraConfig(),
        fileScanWorkflow.qrScanOnScanSuccess,
        fileScanWorkflow.qrScanOnScanError
    );
    qrScanApplyCameraViewportStyles();

    receiptPageState.qrScanCurrentCameraId = targetCameraId;
    receiptPageState.qrScanCameraPaused = false;
    localStorage.setItem(CAMERA_STORAGE_KEY, targetCameraId);
    qrScanSetCameraButtonsState(true);
    qrScanUpdateCameraStatus("Сканирование активно", false);
}

export function qrScanApplyCameraViewportStyles(): void {
    const viewport = document.getElementById("qr-reader-viewport");
    if (!viewport) {
        return;
    }

    const video = viewport.querySelector("video");
    if (video) {
        video.style.width = "100%";
        video.style.height = "100%";
        video.style.objectFit = "cover";
    }

    const section = viewport.querySelector("section");
    if (section) {
        section.style.borderRadius = "1rem";
        section.style.overflow = "hidden";
    }
}

export async function qrScanStopCameraAsync(): Promise<void> {
    if (!receiptPageState.qrScanHtml5QrcodeScanner || typeof receiptPageState.qrScanHtml5QrcodeScanner.getState !== "function") {
        return;
    }

    const scannerState = receiptPageState.qrScanHtml5QrcodeScanner.getState();
    if (scannerState === Html5QrcodeScannerState.NOT_STARTED) {
        receiptPageState.qrScanCameraPaused = false;
        qrScanSetCameraButtonsState(false);
        qrScanUpdateCameraStatus("Камера остановлена", false);
        return;
    }

    if (receiptPageState.qrScanCameraPaused) {
        try {
            receiptPageState.qrScanHtml5QrcodeScanner.resume();
            receiptPageState.qrScanCameraPaused = false;
        } catch (error) {
            console.warn("QR scan: не удалось снять паузу перед остановкой камеры", error);
        }
    }

    await receiptPageState.qrScanHtml5QrcodeScanner.stop();
    receiptPageState.qrScanCameraPaused = false;
    receiptPageState.qrScanCurrentCameraId = null;
    qrScanSetCameraButtonsState(false);
    qrScanUpdateCameraStatus("Камера остановлена", false);
}

// получить дату фото из EXIF (DateTimeOriginal / Digitized / DateTime)
// вернуть ISO-строку или null
