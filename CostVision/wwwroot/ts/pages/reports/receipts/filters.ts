import { normalizeSingleLineTextValue } from "../../../shared/formatters.js";
import { ReceiptDto } from "./types.js";

export function applyReceiptFilters(list: ReceiptDto[], query: string, mode: string, selectedAccountId: string): ReceiptDto[] {
    let result = list.slice();

    if (selectedAccountId) {
        result = result.filter(function (receipt) {
            return receipt.accounts.some(function (account) {
                return account.id === selectedAccountId;
            });
        });
    }

    const lowered = normalizeSingleLineText(query).toLowerCase();
    if (lowered) {
        result = result.filter(function (receipt) {
            return receiptMatchesQuery(receipt, lowered, mode);
        });
    }

    return result;
}

function normalizeSingleLineText(value: string | null | undefined): string {
    return normalizeSingleLineTextValue(value);
}

function receiptMatchesQuery(receipt: ReceiptDto, loweredQuery: string, mode: string): boolean {
    const shop = normalizeSingleLineText(receipt.retailPlace).toLowerCase();
    const account = getReceiptAccountNamesText(receipt).toLowerCase();
    const fn = normalizeSingleLineText(receipt.fiscalDriveNumber).toLowerCase();
    const fd = normalizeSingleLineText(receipt.fiscalDocumentNumber).toLowerCase();
    const fp = normalizeSingleLineText(receipt.fiscalSign).toLowerCase();

    if (mode === "sum") {
        return sumMatchesQuery(receipt.totalSum, loweredQuery);
    }

    if (mode === "shop") {
        return shop.includes(loweredQuery) || account.includes(loweredQuery);
    }

    if (mode === "fn") {
        return fn.includes(loweredQuery);
    }

    if (mode === "fd") {
        return fd.includes(loweredQuery);
    }

    if (mode === "fp") {
        return fp.includes(loweredQuery);
    }

    const sumText = typeof receipt.totalSum === "number" ? receipt.totalSum.toString() : "";
    return shop.includes(loweredQuery)
        || account.includes(loweredQuery)
        || fn.includes(loweredQuery)
        || fd.includes(loweredQuery)
        || fp.includes(loweredQuery)
        || sumText.toLowerCase().includes(loweredQuery);
}

function getReceiptAccountNamesText(receipt: ReceiptDto): string {
    const names = receipt.accounts
        .map(function (account) { return account.name; })
        .filter(function (name, index, items) {
            return !!name && items.indexOf(name) === index;
        });

    return names.join(", ");
}

function sumMatchesQuery(totalSum: number, loweredQuery: string): boolean {
    const normalized = loweredQuery.replace(",", ".").replace(/\s+/g, "");

    const parsed = Number(normalized);
    if (!Number.isFinite(parsed)) {
        return totalSum.toString().includes(normalized);
    }

    const diff = Math.abs(totalSum - parsed);
    return diff < 0.01;
}
