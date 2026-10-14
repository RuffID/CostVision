import { getImageSizeFromFile, qrScanDecodeFile, qrScanGetPhotoDateFromFile, throwIfScanCancelled } from "./fileDecoding.js";
import type { QrScanResult } from "./types.js";

// The caller owns the packet, scanner and cancellation; no page state or POST here.
export async function decodeFiles(files: readonly File[], scanner: Pick<Html5QrcodeInstance, "scanFileV2">, signal: AbortSignal): Promise<QrScanResult[]> {
    const results: QrScanResult[] = [];
    for (const file of files) {
        throwIfScanCancelled(signal);
        const debugInfo = { fileName: file.name, contentType: file.type, fileSizeBytes: file.size, width: 0, height: 0 };
        try {
            const size = await getImageSizeFromFile(file, signal);
            throwIfScanCancelled(signal);
            debugInfo.width = size.width;
            debugInfo.height = size.height;
        } catch (error) {
            throwIfScanCancelled(signal);
            // Dimensions are optional diagnostics, not the decoder's result.
            console.warn("Не удалось прочитать размер изображения.", error);
        }
        let photoDateTime: string | null = null;
        try {
            photoDateTime = await qrScanGetPhotoDateFromFile(file, signal);
            throwIfScanCancelled(signal);
        } catch (error) {
            throwIfScanCancelled(signal);
            console.warn("Не удалось прочитать EXIF изображения.", error);
        }
        if (!photoDateTime && Number.isFinite(file.lastModified)) {
            photoDateTime = new Date(file.lastModified).toISOString();
        }
        try {
            const decoded = await qrScanDecodeFile(file, scanner, signal);
            throwIfScanCancelled(signal);
            results.push({ fileName: file.name, decodedText: decoded.decodedText, errorMessage: null, debugInfo, photoDateTime });
        } catch (error) {
            throwIfScanCancelled(signal);
            results.push({ fileName: file.name, decodedText: "", errorMessage: "Не удалось распознать QR-код", debugInfo, photoDateTime });
        }
    }
    return results;
}
