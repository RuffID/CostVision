import { AccountDto, AccountViewModel } from "./models.js";

export function replaceAccount(accounts: AccountViewModel[], account: AccountViewModel): void {
    const idx = accounts.findIndex(a => a.id === account.id);
    if (idx < 0) {
        throw new Error("Счёт не найден в локальном списке.");
    }

    accounts[idx] = account;
}

export function requireAccountById(accounts: AccountViewModel[], accountId: string): AccountViewModel {
    const account = accounts.find(a => a.id === accountId);
    if (account === undefined) {
        throw new Error("Счёт не найден в локальном списке.");
    }

    return account;
}

export function normalizeAccountDto(dto: AccountDto): AccountViewModel {
    return {
        ...dto,
        description: dto.description ?? "",
        colorHex: normalizeColorHex(dto.colorHex)
    };
}

export function normalizeColorHex(colorHex: string | null | undefined): string {
    const value = (colorHex || "").trim().toUpperCase();
    if (!/^#[0-9A-F]{6}$/.test(value)) {
        throw new Error("Некорректный HEX-цвет счёта.");
    }

    return value;
}
