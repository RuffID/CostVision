export interface ReceiptAccountDto {
    id: string;
    name: string;
}

export interface ImageDebugInfo {
    fileName: string;
    contentType: string;
    fileSizeBytes: number;
    width: number;
    height: number;
}

export interface QrParsed {
    dateTime: string | null;
    sum: number | null;
    fiscalDriveNumber: string;
    fiscalDocumentNumber: string;
    fiscalSign: string;
    operationType: number | null;
}

export interface QrScanResult {
    fileName: string;
    decodedText: string | null;
    errorMessage: string | null;
    parsed?: QrParsed | null;
    debugInfo?: ImageDebugInfo | null;
    photoDateTime?: string | null;
}

export interface QrScanPayload {
    accountId: string;
    results: QrScanResult[];
}

export interface QrScanServerResponse {
    scannedCount: number;
    addedToDbCount: number;
    errorCount: number;
    errorMessage?: string | null;
    results: QrScanResult[];
}

export interface ManualReceiptPayload {
    receipt: {
        fiscalDriveNumber: string;
        fiscalDocumentNumber: string;
        fiscalSign: string;
        sum: number;
        dateTime: string;
        operationType: number;
    };
    accountId: string;
}

export interface ManualReceiptResponse {
    isCreated: boolean;
    message?: string | null;
    receipt?: unknown;
}

export interface DecodedQrFileResult {
    decodedText: string;
}

export interface ImageSize {
    width: number;
    height: number;
}
