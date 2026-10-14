import { throwIfScanCancelled } from "./fileDecoding.js";

// Vendor scanFileV2 creates URLs outside its public clear() contract.
// A separate same-origin realm lets this adapter own them without changing the page's URL API.
interface ScannerWindow extends Window {
    Html5Qrcode: Html5QrcodeConstructor;
    File: typeof File;
    URL: typeof URL;
}

export interface FileScanner {
    scanFileV2(file: File, showImage?: boolean): Promise<{ decodedText: string }>;
    dispose(): void;
}

export async function createFileScanner(root: HTMLElement, vendorUrl: string, signal: AbortSignal): Promise<FileScanner> {
    throwIfScanCancelled(signal);
    const frame = document.createElement("iframe");
    frame.title = "Декодирование изображения";
    frame.className = "d-none";
    root.appendChild(frame);
    const realm = frame.contentWindow as ScannerWindow | null;
    const frameDocument = frame.contentDocument;
    if (!realm || !frameDocument) {
        frame.remove();
        throw new Error("Не удалось создать сканер файлов.");
    }
    const urls = new Set<string>();
    const createUrl = realm.URL.createObjectURL.bind(realm.URL);
    const revokeUrl = realm.URL.revokeObjectURL.bind(realm.URL);
    realm.URL.createObjectURL = blob => {
        const url = createUrl(blob);
        urls.add(url);
        return url;
    };
    realm.URL.revokeObjectURL = url => {
        urls.delete(url);
        revokeUrl(url);
    };
    let scanner: Html5QrcodeInstance | null = null;
    let disposed = false;
    let cancelPending: (() => void) | null = null;
    const releaseUrls = () => {
        for (const url of urls) {
            revokeUrl(url);
        }
        urls.clear();
    };
    const dispose = () => {
        if (disposed) {
            return;
        }
        disposed = true;
        signal.removeEventListener("abort", dispose);
        cancelPending?.();
        cancelPending = null;
        releaseUrls();
        frame.remove();
    };
    signal.addEventListener("abort", dispose, { once: true });

    try {
        const target = frameDocument.createElement("div");
        target.id = "file-scan-root";
        frameDocument.body.appendChild(target);
        await new Promise<void>((resolve, reject) => {
            const script = frameDocument.createElement("script");
            const cleanup = () => {
                script.onload = null;
                script.onerror = null;
                cancelPending = null;
            };
            cancelPending = () => {
                cleanup();
                reject(new DOMException("Обработка отменена.", "AbortError"));
            };
            script.onload = () => {
                cleanup();
                resolve();
            };
            script.onerror = () => {
                cleanup();
                reject(new Error("Не удалось загрузить библиотеку сканирования файлов."));
            };
            script.src = vendorUrl;
            frameDocument.head.appendChild(script);
        });
        throwIfScanCancelled(signal);
        scanner = new realm.Html5Qrcode("file-scan-root");
    } catch (error) {
        dispose();
        throw error;
    }

    return {
        dispose,
        async scanFileV2(file: File, showImage = false) {
            throwIfScanCancelled(signal);
            if (disposed || !scanner) {
                throw new Error("Сканер файлов освобождён.");
            }
            const current = scanner;
            // File instanceof must belong to the vendor's realm.
            const realmFile = new realm.File([file], file.name, { type: file.type, lastModified: file.lastModified });
            try {
                return await new Promise<{ decodedText: string }>((resolve, reject) => {
                    cancelPending = () => reject(new DOMException("Обработка отменена.", "AbortError"));
                    current.scanFileV2(realmFile, showImage).then(resolve, reject);
                });
            } finally {
                cancelPending = null;
                releaseUrls();
                if (!disposed) {
                    current.clear();
                }
            }
        }
    };
}
