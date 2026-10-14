import type { ManualDraft, ReceiptStorage } from "./types.js";
import { getReceiptStorageKeys } from "./constants.js";

// No legacy reads/migration, TTL or logout deletion. Retention remains unchanged.
export function createReceiptStorage(userId: string, storage: Storage = localStorage): ReceiptStorage {
    const keys = getReceiptStorageKeys(userId);
    return {
        readAccount() {
            return storage.getItem(keys.account);
        },
        writeAccount(value) {
            storage.setItem(keys.account, value);
        },
        readDraft() {
            const raw = storage.getItem(keys.draft);
            if (raw === null) {
                return null;
            }
            const value: unknown = JSON.parse(raw);
            if (!value || typeof value !== "object") {
                throw new Error("Повреждён локальный черновик.");
            }
            const draft = value as Record<string, unknown>;
            if (!["fn", "fd", "fp", "sum", "date", "type"].every(field => typeof draft[field] === "string")) {
                throw new Error("Повреждён локальный черновик.");
            }
            return { fn: draft.fn as string, fd: draft.fd as string, fp: draft.fp as string,
                sum: draft.sum as string, date: draft.date as string, type: draft.type as string };
        },
        writeDraft(draft) {
            storage.setItem(keys.draft, JSON.stringify({ ...draft }));
        },
        clearDraft() {
            storage.removeItem(keys.draft);
        }
    };
}
