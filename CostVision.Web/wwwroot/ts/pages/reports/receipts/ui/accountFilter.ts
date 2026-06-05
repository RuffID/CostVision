import { AvailableAccountDto } from "../types.js";

export const RECEIPT_ACCOUNT_FILTER_WITHOUT_ACCOUNT = "__without_account__";

export interface AccountFilterElements {
    select: HTMLSelectElement;
    error: HTMLElement;
}

export function renderReceiptAccountFilterLoading(select: HTMLSelectElement): void {
    select.replaceChildren();

    const option = document.createElement("option");
    option.value = "";
    option.textContent = "Загрузка счетов...";
    select.appendChild(option);
}

export function renderReceiptAccountFilterOptions(select: HTMLSelectElement, accounts: AvailableAccountDto[]): void {
    const currentValue = select.value || "";
    select.replaceChildren();

    const allOption = document.createElement("option");
    allOption.value = "";
    allOption.textContent = "Все счета";
    select.appendChild(allOption);

    const withoutAccountOption = document.createElement("option");
    withoutAccountOption.value = RECEIPT_ACCOUNT_FILTER_WITHOUT_ACCOUNT;
    withoutAccountOption.textContent = "Без счёта";
    select.appendChild(withoutAccountOption);

    for (const account of accounts) {
        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;
        select.appendChild(option);
    }

    if (currentValue === RECEIPT_ACCOUNT_FILTER_WITHOUT_ACCOUNT || accounts.some(function (account) { return account.id === currentValue; })) {
        select.value = currentValue;
    }
}

export function renderReceiptAccountFilterError(elements: AccountFilterElements, error: Error): void {
    renderReceiptAccountFilterOptions(elements.select, []);

    elements.select.disabled = true;
    elements.error.textContent = error && error.message ? error.message : "Не удалось загрузить список счетов.";
    elements.error.classList.remove("d-none");
}

export function hideReceiptAccountFilterError(errorElement: HTMLElement): void {
    errorElement.textContent = "";
    errorElement.classList.add("d-none");
}
