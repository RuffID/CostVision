import { AvailableAccountDto } from "../types.js";

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

    for (const account of accounts) {
        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;
        select.appendChild(option);
    }

    if (currentValue && accounts.some(function (account) { return account.id === currentValue; })) {
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
