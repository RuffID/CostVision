import { canEditAccount } from "../accountActions.js";
import { AvailableAccountDto, MoveReceiptTargetAccount, ReceiptAccountDto, ReceiptDto } from "../types.js";

export function normalizeAvailableAccount(dto: AvailableAccountDto): AvailableAccountDto {
    if (!dto) {
        throw new Error("Счет недоступен.");
    }

    const id = dto.id || "";
    const name = dto.name || "";
    const colorHex = normalizeReceiptAccountColorHex(dto.colorHex);
    const accessRole = dto.accessRole ?? 0;
    const canManage = dto.canManage === true;

    if (!id || !name) {
        throw new Error("Счет получен без обязательных полей.");
    }

    return {
        id: id,
        name: name,
        colorHex: colorHex,
        accessRole: Number(accessRole),
        canManage: canManage
    };
}

export function getReceiptAccounts(receipt: ReceiptDto): ReceiptAccountDto[] {
    if (receipt && Array.isArray(receipt.accounts)) {
        return receipt.accounts.map(function (account) {
            if (!account) {
                throw new Error("Счет чека недоступен.");
            }

            const id = account.id || "";
            const receiptId = account.receiptId || "";
            const name = account.name || "";
            const colorHex = normalizeReceiptAccountColorHex(account.colorHex);
            const accessRole = account.accessRole ?? null;
            const canEditReceipt = account.canEditReceipt === true;

            if (!id || !name || !receiptId) {
                throw new Error("Счет чека получен без обязательных полей.");
            }

            return {
                id: id,
                receiptId: receiptId,
                name: name,
                colorHex: colorHex,
                accessRole: accessRole !== null ? Number(accessRole) : null,
                canEditReceipt: canEditReceipt
            };
        });
    }

    throw new Error("Чек получен без списка счетов.");
}

export function getReceiptAccountNamesText(receipt: ReceiptDto): string {
    const names = getReceiptAccounts(receipt)
        .map(function (account) { return account.name; })
        .filter(function (name, index, items) {
            return !!name && items.indexOf(name) === index;
        });

    return names.join(", ");
}

export function findAvailableAccountById(accounts: AvailableAccountDto[], accountId: string): AvailableAccountDto | null {
    if (!accountId) {
        return null;
    }

    for (const account of accounts) {
        if (account && account.id === accountId) {
            return account;
        }
    }

    return null;
}

export function findReceiptById(receipts: ReceiptDto[], receiptId: string): ReceiptDto | null {
    for (const receipt of receipts) {
        if (receipt && receipt.id === receiptId) {
            return receipt;
        }
    }

    return null;
}

export function findReceiptByAccountReceiptId(receipts: ReceiptDto[], receiptId: string, accountId: string): ReceiptDto | null {
    if (!receiptId || !accountId) {
        return null;
    }

    for (const receipt of receipts) {
        if (!receipt) {
            continue;
        }

        const account = findReceiptAccount(receipt, accountId, receiptId);
        if (account) {
            return receipt;
        }
    }

    return null;
}

export function findReceiptAccount(receipt: ReceiptDto, accountId: string, receiptId: string | null): ReceiptAccountDto | null {
    const accounts = getReceiptAccounts(receipt);

    for (const account of accounts) {
        if (!account || account.id !== accountId) {
            continue;
        }

        if (!receiptId || account.receiptId === receiptId) {
            return account;
        }
    }

    return null;
}

export function getAvailableTargetAccounts(availableAccounts: AvailableAccountDto[], receipt: ReceiptDto, sourceAccountId: string): MoveReceiptTargetAccount[] {
    return availableAccounts
        .filter(function (account) {
            return account && account.id !== sourceAccountId && canEditAccount(account);
        })
        .map(function (account) {
            const reason = getMoveReceiptTargetDisabledReason(receipt, account);
            return {
                id: account.id,
                name: account.name,
                isDisabled: !!reason,
                disabledReason: reason
            };
        })
        .sort(function (left, right) {
            if (left.isDisabled === right.isDisabled) {
                return left.name.localeCompare(right.name, "ru");
            }

            return left.isDisabled ? 1 : -1;
        });
}

export function replaceReceiptAccountLink(receipts: ReceiptDto[], availableAccounts: AvailableAccountDto[], receiptId: string, sourceAccountId: string, targetAccountId: string): void {
    const receipt = findReceiptByAccountReceiptId(receipts, receiptId, sourceAccountId);
    if (!receipt) {
        throw new Error("Не удалось обновить чек после переноса.");
    }

    const targetAccount = findAvailableAccountById(availableAccounts, targetAccountId);
    if (!targetAccount) {
        throw new Error("Счет назначения не найден в текущем списке.");
    }

    const accounts = getReceiptAccounts(receipt);
    const updatedAccounts: ReceiptAccountDto[] = [];

    for (const account of accounts) {
        if (!account || account.id === targetAccountId) {
            continue;
        }

        if (account.id === sourceAccountId) {
            updatedAccounts.push({
                id: targetAccount.id,
                receiptId: receiptId,
                name: targetAccount.name,
                colorHex: targetAccount.colorHex,
                accessRole: targetAccount.accessRole,
                canEditReceipt: true
            });
            continue;
        }

        updatedAccounts.push(account);
    }

    applyReceiptAccounts(receipt, updatedAccounts);
}

export function removeReceiptAccountLink(receipts: ReceiptDto[], receiptId: string, accountId: string): ReceiptDto[] {
    const receipt = findReceiptByAccountReceiptId(receipts, receiptId, accountId);
    if (!receipt) {
        throw new Error("Не удалось обновить чек после удаления связи.");
    }

    const updatedAccounts = getReceiptAccounts(receipt).filter(function (account) {
        return account && !(account.id === accountId && account.receiptId === receiptId);
    });

    if (updatedAccounts.length === 0) {
        return receipts.filter(function (item) {
            return item && item !== receipt;
        });
    }

    applyReceiptAccounts(receipt, updatedAccounts);
    return receipts;
}

export function receiptHasAccount(receipt: ReceiptDto, accountId: string): boolean {
    if (!accountId) {
        return true;
    }

    return getReceiptAccounts(receipt).some(function (account) {
        return account.id === accountId;
    });
}

export function normalizeReceiptAccountColorHex(colorHex: string | null | undefined): string {
    const value = (colorHex || "").toString().trim().toUpperCase();
    if (!/^#[0-9A-F]{6}$/.test(value)) {
        throw new Error("Некорректный HEX-цвет счета.");
    }

    return value;
}

function getMoveReceiptTargetDisabledReason(receipt: ReceiptDto, account: AvailableAccountDto): string {
    if (!account) {
        return "Счет назначения недоступен.";
    }

    if (receiptHasAccount(receipt, account.id)) {
        return "Этот чек уже привязан к выбранному счету.";
    }

    if (!canEditAccount(account)) {
        return "Недостаточно прав для переноса чека в выбранный счет.";
    }

    return "";
}

function applyReceiptAccounts(receipt: ReceiptDto, accounts: ReceiptAccountDto[]): void {
    const normalizedAccounts = accounts
        .filter(function (account) { return !!account; })
        .sort(function (left, right) { return left.name.localeCompare(right.name, "ru"); });

    receipt.accounts = normalizedAccounts.map(function (account) {
        return {
            id: account.id,
            receiptId: account.receiptId,
            name: account.name,
            colorHex: account.colorHex,
            accessRole: account.accessRole,
            canEditReceipt: account.canEditReceipt === true
        };
    });

    const firstAccount = normalizedAccounts.length > 0 ? normalizedAccounts[0] : null;
    const firstEditableAccount = normalizedAccounts.find(function (account) { return account.canEditReceipt === true; }) || firstAccount;
    receipt.id = firstEditableAccount ? firstEditableAccount.receiptId : receipt.id;
    receipt.accountId = firstAccount ? firstAccount.id : null;
    receipt.accountName = firstAccount ? firstAccount.name : "";
}
