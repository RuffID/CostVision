interface BootstrapModalInstance {
    show(): void;
    hide(): void;
}

interface BootstrapModalConstructor {
    new (element: Element): BootstrapModalInstance;
    getOrCreateInstance(element: Element): BootstrapModalInstance;
}

declare const bootstrap: {
    Modal: BootstrapModalConstructor;
};

interface Html5QrcodeCamera {
    id: string;
    label: string;
}

interface Html5QrcodeConfig {
    fps?: number;
    qrbox?: { width: number; height: number } | ((viewfinderWidth: number, viewfinderHeight: number) => { width: number; height: number });
    aspectRatio?: number;
    disableFlip?: boolean;
}

interface Html5QrcodeDecodedResult {
    decodedText?: string;
    result?: {
        text?: string;
    };
}

interface Html5QrcodeInstance {
    clear(): Promise<void>;
    pause(shouldPauseVideo?: boolean): void;
    resume(): void;
    getState(): number;
    start(
        cameraIdOrConfig: string | { facingMode: string },
        configuration: Html5QrcodeConfig,
        onScanSuccess: (decodedText: string, decodedResult: Html5QrcodeDecodedResult) => void,
        onScanFailure?: (errorMessage: string) => void
    ): Promise<void>;
    stop(): Promise<void>;
    scanFile(file: File, showImage?: boolean): Promise<string>;
    scanFileV2(file: File, showImage?: boolean): Promise<{ decodedText: string }>;
}

interface Html5QrcodeConstructor {
    new (elementId: string): Html5QrcodeInstance;
    getCameras(): Promise<Html5QrcodeCamera[]>;
}

declare const Html5Qrcode: Html5QrcodeConstructor;

declare const Html5QrcodeScannerState: {
    NOT_STARTED: number;
    SCANNING: number;
    PAUSED: number;
};

interface ExifReader {
    getData(image: File, callback: (this: unknown) => void): void;
    getTag(image: unknown, tag: string): string | undefined;
}

declare const EXIF: ExifReader;

interface JsQrResult {
    data: string;
}

declare function jsQR(data: Uint8ClampedArray, width: number, height: number, options?: { inversionAttempts?: string }): JsQrResult | null;

// Используемая поверхность Chart.js 4.5.1 из wwwroot/lib/chartjs.
interface ChartInstance {
    destroy(): void;
}

interface ChartBarConfiguration {
    type: "bar";
    data: {
        labels: string[];
        datasets: {
            label: string;
            data: number[];
            backgroundColor: string;
            borderColor: string;
            borderWidth: number;
        }[];
    };
    options: {
        responsive: boolean;
        maintainAspectRatio: boolean;
        scales: {
            y: {
                beginAtZero: boolean;
                ticks: { callback(value: string | number): string };
            };
        };
        plugins: {
            tooltip: {
                callbacks: {
                    label(context: { dataset: { label?: string }, parsed: { y: number } }): string;
                };
            };
        };
    };
}

declare const Chart: {
    new (context: CanvasRenderingContext2D, configuration: ChartBarConfiguration): ChartInstance;
};

interface Window {
    bootstrap?: {
        Modal: BootstrapModalConstructor;
    };
    EXIF?: ExifReader;
}
