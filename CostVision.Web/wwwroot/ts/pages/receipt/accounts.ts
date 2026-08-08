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
import * as initialization from "./initialization.js";
import { ACCOUNT_STORAGE_KEY } from "./constants.js";
import type { ReceiptAccountDto } from "./types.js";

export async function loadAvailableAccountsAsync(): Promise<void> {
    if (!receiptPageState.accountSelect) {
        throw new Error("Не найден список счетов.");
    }

    receiptPageState.accountSelect.disabled = true;
    renderAvailableAccounts([]);

    try {
        const accountItems = (await loadReceiptAccountsAsync(receiptPageState.antiForgeryToken)).map(normalizeReceiptAccountDto);

        if (accountItems.length === 0) {
            throw new Error("Нет доступных счетов для сохранения чека.");
        }

        renderAvailableAccounts(accountItems);
        restoreSelectedAccount(accountItems);
        receiptPageState.accountSelect.disabled = false;
        hideAccountSelectError();
    } catch (error) {
        renderAccountSelectError(error);
        throw error;
    }
}

export function renderAvailableAccounts(accounts: ReceiptAccountDto[]): void {
    if (!receiptPageState.accountSelect) {
        return;
    }

    receiptPageState.accountSelect.replaceChildren();

    if (!Array.isArray(accounts) || accounts.length === 0) {
        const loadingOption = document.createElement("option");
        loadingOption.value = "";
        loadingOption.textContent = "Загрузка счетов...";
        receiptPageState.accountSelect.appendChild(loadingOption);
        return;
    }

    accounts.forEach(function (account) {
        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;
        receiptPageState.accountSelect.appendChild(option);
    });
}

export function restoreSelectedAccount(accounts: ReceiptAccountDto[]): void {
    if (!receiptPageState.accountSelect || !Array.isArray(accounts) || accounts.length === 0) {
        return;
    }

    const savedId = localStorage.getItem(ACCOUNT_STORAGE_KEY);
    if (savedId && initialization.hasOption(receiptPageState.accountSelect, savedId)) {
        receiptPageState.accountSelect.value = savedId;
        return;
    }

    receiptPageState.accountSelect.value = accounts[0].id;
    localStorage.setItem(ACCOUNT_STORAGE_KEY, receiptPageState.accountSelect.value);
}

export function normalizeReceiptAccountDto(dto: ReceiptAccountDto): ReceiptAccountDto {
    if (!dto) {
        throw new Error("Счёт недоступен.");
    }

    const id = dto.id || "";
    const name = dto.name || "";

    if (!id || !name) {
        throw new Error("Счёт получен без обязательных полей.");
    }

    return {
        id: id,
        name: name
    };
}

export function renderAccountSelectError(error: unknown): void {
    if (receiptPageState.accountSelect) {
        receiptPageState.accountSelect.disabled = true;
        receiptPageState.accountSelect.replaceChildren();

        const errorOption = document.createElement("option");
        errorOption.value = "";
        errorOption.textContent = "Не удалось загрузить счета";
        receiptPageState.accountSelect.appendChild(errorOption);
    }

    if (!receiptPageState.accountSelectError) {
        return;
    }

    const message = error instanceof Error ? error.message : "Не удалось загрузить счета.";
    receiptPageState.accountSelectError.textContent = message;
    receiptPageState.accountSelectError.classList.remove("d-none");
}

export function hideAccountSelectError(): void {
    if (!receiptPageState.accountSelectError) {
        return;
    }

    receiptPageState.accountSelectError.textContent = "";
    receiptPageState.accountSelectError.classList.add("d-none");
}
