import { requireElementById, requireInputById, requireSelectById } from "../../shared/dom.js";
import type { ManualDraft, ManualReceiptPayload } from "./types.js";

export function createManualForm() {
    const root = requireElementById("manual-request-root");
    const button = requireElementById("manual-check-btn", HTMLButtonElement);
    const message = requireElementById("manual-result-message");
    const fn = requireInputById("manual-fn");
    const fd = requireInputById("manual-fd");
    const fp = requireInputById("manual-fp");
    const sum = requireInputById("manual-sum");
    const date = requireInputById("manual-date");
    const type = requireSelectById("manual-type");
    const fields = [fn, fd, fp, sum, date, type];

    function capture(): ManualDraft {
        return { fn: fn.value, fd: fd.value, fp: fp.value, sum: sum.value, date: date.value, type: type.value };
    }
    function restore(draft: ManualDraft): void {
        fn.value = draft.fn;
        fd.value = draft.fd;
        fp.value = draft.fp;
        sum.value = draft.sum;
        date.value = draft.date;
        type.value = draft.type;
    }
    return {
        button, date, capture, restore,
        validate: () => validateManualFields(root, fn, fd, fp, sum, date, type, message),
        payload(accountId: string): ManualReceiptPayload {
            return {
                accountId,
                receipt: {
                    fiscalDriveNumber: fn.value.replace(/\D/g, ""),
                    fiscalDocumentNumber: fd.value.replace(/\D/g, ""),
                    fiscalSign: fp.value.replace(/\D/g, ""),
                    sum: parseFloat(sum.value.trim().replace(",", ".")),
                    dateTime: date.value.trim(),
                    operationType: parseInt(type.value, 10)
                }
            };
        },
        showMessage(text: string, success: boolean): void {
            message.textContent = text;
            message.classList.toggle("text-danger", !success);
            message.classList.toggle("text-success", success);
        },
        clearIfUnchanged(draft: ManualDraft): boolean {
            if (JSON.stringify(capture()) !== JSON.stringify(draft)) {
                return false;
            }
            restore({ fn: "", fd: "", fp: "", sum: "", date: "", type: "1" });
            return true;
        },
        bindDraft(onChange: () => void): () => void {
            for (const field of fields) {
                field.addEventListener("input", onChange);
                field.addEventListener("change", onChange);
            }
            return () => {
                for (const field of fields) {
                    field.removeEventListener("input", onChange);
                    field.removeEventListener("change", onChange);
                }
            };
        }
    };
}

export function validateManualFields(root: HTMLElement, fnInput: HTMLInputElement, fdInput: HTMLInputElement, fpInput: HTMLInputElement, sumInput: HTMLInputElement, dateInput: HTMLInputElement, typeSelect: HTMLSelectElement, resultMessage: HTMLElement): boolean {
    clearManualFieldError(root, fnInput);
    clearManualFieldError(root, fdInput);
    clearManualFieldError(root, fpInput);
    clearManualFieldError(root, sumInput);
    clearManualFieldError(root, dateInput);
    clearManualFieldError(root, typeSelect);

    if (resultMessage) {
        resultMessage.textContent = "";
        resultMessage.classList.remove("text-danger", "text-success");
    }

    let hasError = false;

    const fnDigits = (fnInput?.value || "").replace(/\D/g, "");
    const fdDigits = (fdInput?.value || "").replace(/\D/g, "");
    const fpDigits = (fpInput?.value || "").replace(/\D/g, "");
    const sumRaw = (sumInput?.value || "").trim();
    const dateValue = (dateInput?.value || "").trim();
    const typeValue = (typeSelect?.value || "").trim();

    // ФН: строго 16 цифр
    if (!fnDigits || fnDigits.length !== 16) {
        setManualFieldError(root, fnInput, "ФН должен содержать 16 цифр");
        hasError = true;
    }

    // ФД: обязательно, 1–10 цифр
    if (!fdDigits || fdDigits.length > 10) {
        setManualFieldError(root, fdInput, "ФД должен содержать от 4 до 10 цифр");
        hasError = true;
    }

    // ФПД: обязательно, 8–10 цифр
    if (!fpDigits || fpDigits.length < 8 || fpDigits.length > 10) {
        setManualFieldError(root, fpInput, "ФПД должен содержать от 8 до 10 цифр");
        hasError = true;
    }

    // Сумма: обязательно, число (0 допустим)
    if (sumRaw === "" || Number.isNaN(parseFloat(sumRaw))) {
        setManualFieldError(root, sumInput, "Укажите сумму");
        hasError = true;
    }

    // Дата: обязательно
    if (!dateValue) {
        setManualFieldError(root, dateInput, "Укажите дату и время");
        hasError = true;
    }

    // Тип чека: обязательно
    // если добавишь в select пустую опцию с value="" или "0", это тоже отловится
    if (!typeValue || typeValue === "0") {
        setManualFieldError(root, typeSelect, "Выберите тип чека");
        hasError = true;
    }

    if (hasError) {
        return false;
    }

    return true;
}

export function setManualFieldError(root: HTMLElement, input: HTMLInputElement | HTMLSelectElement, message: string): void {
    if (!root || !input) {
        return;
    }

    const row = input.closest(".manual-row");
    if (!row) {
        return;
    }

    const label = row.querySelector("label");
    if (label) {
        label.classList.add("text-danger");
    }

    input.classList.add("is-invalid");

    let errorSpan = row.querySelector<HTMLElement>(".manual-field-error");
    if (!errorSpan) {
        errorSpan = document.createElement("div");
        errorSpan.className = "manual-field-error invalid-feedback d-block";
        row.appendChild(errorSpan);
    }

    errorSpan.textContent = message;
}

export function clearManualFieldError(root: HTMLElement, input: HTMLInputElement | HTMLSelectElement): void {
    if (!root || !input) {
        return;
    }

    const row = input.closest(".manual-row");
    if (!row) {
        return;
    }

    const label = row.querySelector("label");
    if (label) {
        label.classList.remove("text-danger");
    }

    input.classList.remove("is-invalid");

    const errorSpan = row.querySelector(".manual-field-error");
    if (errorSpan) {
        errorSpan.textContent = "";
    }
}

// ===================== Доп. функции =====================
