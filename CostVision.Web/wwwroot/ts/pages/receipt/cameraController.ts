import { qrScanNormalizeCameraLabel, qrScanIsBackCamera, qrScanIsFrontCamera, qrScanGetCameraPriority } from "./cameraUi.js";

export function createCameraController(scanner: Html5QrcodeInstance, callbacks: {
    onDecoded(text: string): void;
    onRunning(cameraId: string | null): void;
}) {
    let revision = 0;
    let disposed = false;
    let queue: Promise<void> = Promise.resolve();

    function enqueue(action: () => Promise<void>): Promise<void> {
        const result = queue.then(action, action);
        queue = result;
        return result;
    }
    function enqueueOwned(owner: number, action: () => Promise<void>): Promise<void> {
        return enqueue(async () => {
            try {
                await action();
            } catch (error) {
                if (!disposed && owner === revision) {
                    throw error;
                }
            }
        });
    }
    async function stopScanner(): Promise<void> {
        if (scanner.getState() !== Html5QrcodeScannerState.NOT_STARTED) {
            // stop() accepts both SCANNING and PAUSED in the actual vendor.
            await scanner.stop();
        }
    }
    return {
        start(cameraId: string): Promise<void> {
            if (disposed) {
                return Promise.reject(new Error("Сканер камеры освобождён."));
            }
            if (!cameraId) {
                return Promise.reject(new Error("Камера не выбрана."));
            }
            const owner = ++revision;
            return enqueueOwned(owner, async () => {
                if (disposed || owner !== revision) {
                    return;
                }
                await stopScanner();
                if (disposed || owner !== revision) {
                    return;
                }
                await scanner.start(cameraId, qrScanBuildCameraConfig(), text => {
                    if (!disposed && owner === revision && scanner.getState() === Html5QrcodeScannerState.SCANNING) {
                        callbacks.onDecoded(text);
                    }
                }, () => { /* A frame without a QR is expected. */ });
                if (disposed || owner !== revision) {
                    await stopScanner();
                    return;
                }
                qrScanApplyCameraViewportStyles();
                callbacks.onRunning(cameraId);
            });
        },
        stop(): Promise<void> {
            const owner = ++revision;
            return enqueueOwned(owner, async () => {
                await stopScanner();
                if (!disposed && owner === revision) {
                    callbacks.onRunning(null);
                }
            });
        },
        pause(): Promise<void> {
            const owner = revision;
            return enqueueOwned(owner, async () => {
                if (!disposed && owner === revision && scanner.getState() === Html5QrcodeScannerState.SCANNING) {
                    scanner.pause(true);
                }
            });
        },
        resume(): Promise<void> {
            const owner = revision;
            return enqueueOwned(owner, async () => {
                if (!disposed && owner === revision && scanner.getState() === Html5QrcodeScannerState.PAUSED) {
                    scanner.resume();
                }
            });
        },
        dispose(): Promise<void> {
            if (disposed) {
                return queue;
            }
            disposed = true;
            revision++;
            return enqueue(async () => {
                await stopScanner();
                scanner.clear();
            });
        }
    };
}

export async function loadAvailableCameras(): Promise<Html5QrcodeCamera[]> {
    const cameras = await Html5Qrcode.getCameras();
    const normalized = cameras.map((camera, index) => ({
        id: camera.id,
        label: qrScanNormalizeCameraLabel(camera.label, index + 1)
    }));
    const back = normalized.filter(camera => qrScanIsBackCamera(camera.label));
    const available = back.length > 0 ? back.filter(camera => !qrScanIsFrontCamera(camera.label)) : normalized;
    return available.sort((left, right) => qrScanGetCameraPriority(right.label) - qrScanGetCameraPriority(left.label));
}

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
