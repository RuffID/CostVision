import { loadReceiptAccountsAsync } from "./accountsApi.js";
import type { ReceiptAccountDto } from "./types.js";

// Account data is supplied to the view; caller owns its read generation.
export function renderAvailableAccounts(select: HTMLSelectElement, accounts: ReceiptAccountDto[]): void {
    select.replaceChildren();
    for (const account of accounts) {
        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;
        select.appendChild(option);
    }
}

export function getSelectedAccountId(select: HTMLSelectElement): string {
    if (select.disabled || !select.value) {
        throw new Error("Счёт для сохранения чека не выбран.");
    }
    return select.value;
}

export async function loadAvailableAccounts(token: string | null, signal: AbortSignal): Promise<ReceiptAccountDto[]> {
    const items = await loadReceiptAccountsAsync(token, signal);
    if (items.length === 0) {
        throw new Error("Нет доступных счетов для сохранения чека.");
    }
    return items;
}
