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
import { DecodedQrFileResult, ImageCropArea, ImageDebugInfo, ImageSize, ManualReceiptOutcome, ManualReceiptPayload, QrScanPayload, QrScanResult } from "./types.js";
import { receiptPageState } from "./state.js";


export async function qrScanGetPhotoDateFromFile(file: File): Promise<string | null> {
    return new Promise<string | null>(function (resolve) {
        if (!window.EXIF || !file) {
            resolve(null);
            return;
        }

        try {
            EXIF.getData(file, function () {
                try {
                    // this — внутренний объект exif-js, связанный с file
                    const raw =
                        EXIF.getTag(this, "DateTimeOriginal") ||
                        EXIF.getTag(this, "DateTimeDigitized") ||
                        EXIF.getTag(this, "DateTime");

                    if (!raw || typeof raw !== "string") {
                        resolve(null);
                        return;
                    }

                    // ожидаемый формат: "2024:11:27 18:03:15"
                    const parts = raw.split(" ");
                    if (parts.length !== 2) {
                        resolve(null);
                        return;
                    }

                    const datePart = parts[0].split(":");
                    const timePart = parts[1].split(":");
                    if (datePart.length !== 3 || timePart.length < 2) {
                        resolve(null);
                        return;
                    }

                    const year = parseInt(datePart[0], 10);
                    const month = parseInt(datePart[1], 10);
                    const day = parseInt(datePart[2], 10);
                    const hour = parseInt(timePart[0], 10);
                    const minute = parseInt(timePart[1], 10);
                    const second = timePart.length > 2 ? parseInt(timePart[2], 10) : 0;

                    if (Number.isNaN(year) || Number.isNaN(month) || Number.isNaN(day)) {
                        resolve(null);
                        return;
                    }

                    const jsDate = new Date(year, month - 1, day, hour, minute, second);

                    resolve(jsDate.toISOString());
                } catch (err) {
                    console.warn("EXIF parse error", err);
                    resolve(null);
                }
            });
        } catch (err) {
            console.warn("EXIF reader error", err);
            resolve(null);
        }
    });
}

export function getImageSizeFromFile(file: File): Promise<ImageSize> {
    return new Promise<ImageSize>((resolve, reject) => {
        const img = new Image();
        const url = URL.createObjectURL(file);

        img.onload = function () {
            const width = img.naturalWidth;
            const height = img.naturalHeight;
            URL.revokeObjectURL(url);
            resolve({ width, height });
        };

        img.onerror = function (e) {
            URL.revokeObjectURL(url);
            reject(e);
        };

        img.src = url;
    });
}

export async function qrScanDecodeFile(file: File): Promise<DecodedQrFileResult> {
    try {
        return await receiptPageState.qrScanFileScanner.scanFileV2(file, false);
    } catch (primaryError) {
        const fallbackResult = await qrScanDecodeFileWithJsQr(file);
        if (fallbackResult) {
            return fallbackResult;
        }

        throw primaryError;
    }
}

export async function qrScanDecodeFileWithJsQr(file: File): Promise<DecodedQrFileResult | null> {
    if (typeof jsQR !== "function") {
        return null;
    }

    const variants = await qrScanBuildJsQrVariants(file);
    for (let i = 0; i < variants.length; i++) {
        const canvas = variants[i];
        const context = canvas.getContext("2d", { willReadFrequently: true });
        if (!context) {
            continue;
        }

        const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
        const decoded = jsQR(imageData.data, canvas.width, canvas.height, {
            inversionAttempts: "attemptBoth"
        });

        if (decoded && typeof decoded.data === "string" && decoded.data.length > 0) {
            return {
                decodedText: decoded.data
            };
        }
    }

    return null;
}

export async function qrScanBuildJsQrVariants(file: File): Promise<HTMLCanvasElement[]> {
    const image = await qrScanLoadImageFromFile(file);
    const sourceCanvas = qrScanCreateCanvasFromImage(image);
    const sourceCrop = qrScanDetectQrCropArea(sourceCanvas);
    const croppedCanvas = qrScanCropCanvas(sourceCanvas, sourceCrop);
    const variants = [
        sourceCanvas,
        qrScanScaleCanvas(sourceCanvas, 2),
        croppedCanvas,
        qrScanScaleCanvas(croppedCanvas, 2),
        qrScanCreateThresholdCanvas(croppedCanvas, 180),
        qrScanCreateThresholdCanvas(croppedCanvas, 210),
        qrScanCreateThresholdCanvas(qrScanScaleCanvas(croppedCanvas, 2), 200)
    ];

    return variants.filter(function (canvas) {
        return canvas && canvas.width > 0 && canvas.height > 0;
    });
}

export function qrScanLoadImageFromFile(file: File): Promise<HTMLImageElement> {
    return new Promise<HTMLImageElement>((resolve, reject) => {
        const image = new Image();
        const objectUrl = URL.createObjectURL(file);

        image.onload = function () {
            URL.revokeObjectURL(objectUrl);
            resolve(image);
        };

        image.onerror = function (event) {
            URL.revokeObjectURL(objectUrl);
            reject(event);
        };

        image.src = objectUrl;
    });
}

export function qrScanCreateCanvasFromImage(image: HTMLImageElement): HTMLCanvasElement {
    const canvas = document.createElement("canvas");
    canvas.width = image.naturalWidth || image.width;
    canvas.height = image.naturalHeight || image.height;

    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для подготовки изображения");
    }

    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return canvas;
}

export function qrScanScaleCanvas(sourceCanvas: HTMLCanvasElement, multiplier: number): HTMLCanvasElement {
    const safeMultiplier = multiplier > 0 ? multiplier : 1;
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(sourceCanvas.width * safeMultiplier));
    canvas.height = Math.max(1, Math.round(sourceCanvas.height * safeMultiplier));

    const context = canvas.getContext("2d");
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для масштабирования");
    }

    context.imageSmoothingEnabled = false;
    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(sourceCanvas, 0, 0, canvas.width, canvas.height);
    return canvas;
}

export function qrScanCreateThresholdCanvas(sourceCanvas: HTMLCanvasElement, threshold: number): HTMLCanvasElement {
    const canvas = document.createElement("canvas");
    canvas.width = sourceCanvas.width;
    canvas.height = sourceCanvas.height;

    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для бинаризации");
    }

    context.drawImage(sourceCanvas, 0, 0);
    const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
    const pixels = imageData.data;

    for (let i = 0; i < pixels.length; i += 4) {
        const luminance = (0.299 * pixels[i]) + (0.587 * pixels[i + 1]) + (0.114 * pixels[i + 2]);
        const value = luminance < threshold ? 0 : 255;
        pixels[i] = value;
        pixels[i + 1] = value;
        pixels[i + 2] = value;
        pixels[i + 3] = 255;
    }

    context.putImageData(imageData, 0, 0);
    return canvas;
}

export function qrScanDetectQrCropArea(sourceCanvas: HTMLCanvasElement): ImageCropArea {
    const ANALYSIS_MAX_SIDE = 512;
    const DARK_THRESHOLD = 210;
    const MIN_COMPONENT_AREA = 16;
    const analysisScale = Math.min(1, ANALYSIS_MAX_SIDE / Math.max(sourceCanvas.width, sourceCanvas.height));
    const analysisWidth = Math.max(1, Math.round(sourceCanvas.width * analysisScale));
    const analysisHeight = Math.max(1, Math.round(sourceCanvas.height * analysisScale));
    const analysisCanvas = document.createElement("canvas");
    analysisCanvas.width = analysisWidth;
    analysisCanvas.height = analysisHeight;

    const analysisContext = analysisCanvas.getContext("2d", { willReadFrequently: true });
    if (!analysisContext) {
        return {
            x: 0,
            y: 0,
            width: sourceCanvas.width,
            height: sourceCanvas.height
        };
    }

    analysisContext.fillStyle = "#ffffff";
    analysisContext.fillRect(0, 0, analysisWidth, analysisHeight);
    analysisContext.drawImage(sourceCanvas, 0, 0, analysisWidth, analysisHeight);

    const imageData = analysisContext.getImageData(0, 0, analysisWidth, analysisHeight);
    const pixels = imageData.data;
    const mask = new Uint8Array(analysisWidth * analysisHeight);

    for (let y = 0; y < analysisHeight; y++) {
        for (let x = 0; x < analysisWidth; x++) {
            const pixelIndex = ((y * analysisWidth) + x) * 4;
            const luminance = (0.299 * pixels[pixelIndex]) + (0.587 * pixels[pixelIndex + 1]) + (0.114 * pixels[pixelIndex + 2]);
            if (luminance < DARK_THRESHOLD) {
                mask[(y * analysisWidth) + x] = 1;
            }
        }
    }

    const visited = new Uint8Array(mask.length);
    const queue = new Int32Array(mask.length);
    const centerX = analysisWidth / 2;
    const centerY = analysisHeight / 2;
    let bestComponent: { minX: number; minY: number; width: number; height: number; score: number } | null = null;

    for (let startIndex = 0; startIndex < mask.length; startIndex++) {
        if (mask[startIndex] === 0 || visited[startIndex] === 1) {
            continue;
        }

        let queueStart = 0;
        let queueEnd = 0;
        let area = 0;
        let minX = analysisWidth;
        let minY = analysisHeight;
        let maxX = 0;
        let maxY = 0;

        queue[queueEnd++] = startIndex;
        visited[startIndex] = 1;

        while (queueStart < queueEnd) {
            const current = queue[queueStart++];
            const currentX = current % analysisWidth;
            const currentY = Math.floor(current / analysisWidth);
            area++;

            if (currentX < minX) {
                minX = currentX;
            }

            if (currentY < minY) {
                minY = currentY;
            }

            if (currentX > maxX) {
                maxX = currentX;
            }

            if (currentY > maxY) {
                maxY = currentY;
            }

            if (currentX > 0) {
                const leftIndex = current - 1;
                if (mask[leftIndex] === 1 && visited[leftIndex] === 0) {
                    visited[leftIndex] = 1;
                    queue[queueEnd++] = leftIndex;
                }
            }

            if (currentX + 1 < analysisWidth) {
                const rightIndex = current + 1;
                if (mask[rightIndex] === 1 && visited[rightIndex] === 0) {
                    visited[rightIndex] = 1;
                    queue[queueEnd++] = rightIndex;
                }
            }

            if (currentY > 0) {
                const topIndex = current - analysisWidth;
                if (mask[topIndex] === 1 && visited[topIndex] === 0) {
                    visited[topIndex] = 1;
                    queue[queueEnd++] = topIndex;
                }
            }

            if (currentY + 1 < analysisHeight) {
                const bottomIndex = current + analysisWidth;
                if (mask[bottomIndex] === 1 && visited[bottomIndex] === 0) {
                    visited[bottomIndex] = 1;
                    queue[queueEnd++] = bottomIndex;
                }
            }
        }

        if (area < MIN_COMPONENT_AREA) {
            continue;
        }

        const width = maxX - minX + 1;
        const height = maxY - minY + 1;
        const maxSide = Math.max(width, height);
        const minSide = Math.max(1, Math.min(width, height));
        const aspectPenalty = maxSide / minSide;
        const fillRatio = area / (width * height);
        const componentCenterX = minX + (width / 2);
        const componentCenterY = minY + (height / 2);
        const distanceX = (componentCenterX - centerX) / analysisWidth;
        const distanceY = (componentCenterY - centerY) / analysisHeight;
        const centerPenalty = Math.sqrt((distanceX * distanceX) + (distanceY * distanceY));
        const score = area * (1 / aspectPenalty) * Math.max(fillRatio, 0.15) * (1 / (1 + (centerPenalty * 3)));

        if (!bestComponent || score > bestComponent.score) {
            bestComponent = {
                minX,
                minY,
                width,
                height,
                score
            };
        }
    }

    if (!bestComponent) {
        return {
            x: 0,
            y: 0,
            width: sourceCanvas.width,
            height: sourceCanvas.height
        };
    }

    const sourceX = bestComponent.minX / analysisScale;
    const sourceY = bestComponent.minY / analysisScale;
    const sourceWidth = bestComponent.width / analysisScale;
    const sourceHeight = bestComponent.height / analysisScale;
    const padding = Math.max(sourceWidth, sourceHeight) * 0.35;
    const cropX = Math.max(0, Math.floor(sourceX - padding));
    const cropY = Math.max(0, Math.floor(sourceY - padding));
    const cropRight = Math.min(sourceCanvas.width, Math.ceil(sourceX + sourceWidth + padding));
    const cropBottom = Math.min(sourceCanvas.height, Math.ceil(sourceY + sourceHeight + padding));

    return {
        x: cropX,
        y: cropY,
        width: Math.max(1, cropRight - cropX),
        height: Math.max(1, cropBottom - cropY)
    };
}

export function qrScanCropCanvas(sourceCanvas: HTMLCanvasElement, cropArea: ImageCropArea): HTMLCanvasElement {
    const canvas = document.createElement("canvas");
    canvas.width = cropArea.width;
    canvas.height = cropArea.height;

    const context = canvas.getContext("2d");
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для обрезки");
    }

    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(
        sourceCanvas,
        cropArea.x,
        cropArea.y,
        cropArea.width,
        cropArea.height,
        0,
        0,
        cropArea.width,
        cropArea.height
    );

    return canvas;
}
