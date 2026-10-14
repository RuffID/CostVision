export interface CameraView {
    select: HTMLSelectElement;
    renderStatus(message: string, isError: boolean): void;
    renderRunning(running: boolean): void;
    dispose(): void;
}

export function createCameraView(qrReaderElement: HTMLElement, handlers: {
    switchCamera: () => void; start: () => void; stop: () => void; gallery: () => void;
}): CameraView {
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
    cameraSelect.addEventListener("change", handlers.switchCamera);
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
    startButton.addEventListener("click", handlers.start);
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
    stopButton.addEventListener("click", handlers.stop);
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
    galleryButton.addEventListener("click", handlers.gallery);
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



    return {
        select: cameraSelect,
        renderStatus(message, isError) {
            status.textContent = message;
            status.classList.toggle("text-danger", isError);
            status.classList.toggle("text-body-secondary", !isError);
        },
        renderRunning(running) {
            startButton.disabled = running;
            stopButton.disabled = !running;
        },
        dispose() {
            cameraSelect.removeEventListener("change", handlers.switchCamera);
            startButton.removeEventListener("click", handlers.start);
            stopButton.removeEventListener("click", handlers.stop);
            galleryButton.removeEventListener("click", handlers.gallery);
        }
    };
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
