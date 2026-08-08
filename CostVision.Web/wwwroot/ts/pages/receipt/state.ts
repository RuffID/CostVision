import type { BootstrapModal } from "../../shared/bootstrap.js";
import type { QrScanResult } from "./types.js";

export type QrSubmitSource = "camera" | "files" | "manual" | null;

class ReceiptPageState {
    private form: HTMLFormElement | null = null;
    private resultsContainer: HTMLElement | null = null;
    private fileInput: HTMLInputElement | null = null;
    private dropzone: HTMLElement | null = null;
    private fileScanner: Html5QrcodeInstance | null = null;
    private selectedAccount: HTMLSelectElement | null = null;
    private selectedAccountError: HTMLElement | null = null;

    public qrScanResultIndex = 0;
    public qrScanHtml5QrcodeScanner: Html5QrcodeInstance | null = null;
    public qrScanLastCameraText: string | null = null;
    public qrScanLastCameraTs = 0;
    public qrScanAvailableCameras: Html5QrcodeCamera[] = [];
    public qrScanCurrentCameraId: string | null = null;
    public qrScanOverlay: HTMLElement | null = null;
    public antiForgeryToken: string | null = null;
    public qrScanResults: QrScanResult[] = [];
    public qrScanLastSubmitSource: QrSubmitSource = null;
    public qrScanCameraPaused = false;
    public qrScanCameraModal: HTMLElement | null = null;
    public qrScanCameraModalInstance: BootstrapModal | null = null;

    public get qrScanForm(): HTMLFormElement {
        return requireInitialized(this.form, "Форма QR-сканирования не инициализирована.");
    }

    public set qrScanForm(value: HTMLFormElement) {
        this.form = value;
    }

    public get qrScanResultsContainer(): HTMLElement {
        return requireInitialized(this.resultsContainer, "Контейнер результатов QR-сканирования не инициализирован.");
    }

    public set qrScanResultsContainer(value: HTMLElement) {
        this.resultsContainer = value;
    }

    public get qrScanFileInput(): HTMLInputElement {
        return requireInitialized(this.fileInput, "Поле выбора файлов не инициализировано.");
    }

    public set qrScanFileInput(value: HTMLInputElement) {
        this.fileInput = value;
    }

    public get qrScanDropzone(): HTMLElement {
        return requireInitialized(this.dropzone, "Область загрузки файлов не инициализирована.");
    }

    public set qrScanDropzone(value: HTMLElement) {
        this.dropzone = value;
    }

    public get qrScanFileScanner(): Html5QrcodeInstance {
        return requireInitialized(this.fileScanner, "Сканер файлов не инициализирован.");
    }

    public set qrScanFileScanner(value: Html5QrcodeInstance) {
        this.fileScanner = value;
    }

    public get accountSelect(): HTMLSelectElement {
        return requireInitialized(this.selectedAccount, "Список счетов не инициализирован.");
    }

    public set accountSelect(value: HTMLSelectElement) {
        this.selectedAccount = value;
    }

    public get accountSelectError(): HTMLElement {
        return requireInitialized(this.selectedAccountError, "Контейнер ошибки выбора счёта не инициализирован.");
    }

    public set accountSelectError(value: HTMLElement) {
        this.selectedAccountError = value;
    }
}

function requireInitialized<T>(value: T | null, message: string): T {
    if (value === null)
        throw new Error(message);

    return value;
}

export const receiptPageState = new ReceiptPageState();
