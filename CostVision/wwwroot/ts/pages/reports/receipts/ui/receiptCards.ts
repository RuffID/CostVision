import { normalizeSingleLineTextValue } from "../../../../shared/formatters.js";
import { getReceiptAccounts } from "../state/accountModel.js";
import { ReceiptDto } from "../types.js";

const RETAIL_PLACE_DISPLAY_LIMIT = 100;

export function buildReceiptCard(receipt: ReceiptDto, formatCurrency: (value: number) => string): HTMLElement {
    const card = document.createElement("div");
    card.classList.add("card", "shadow-sm");
    card.setAttribute("data-receipt-id", receipt.id);

    const cardBody = document.createElement("div");
    cardBody.classList.add("card-body");

    const topRow = document.createElement("div");
    topRow.classList.add("d-flex", "flex-column", "flex-md-row", "align-items-start", "gap-2");

    const leftDiv = document.createElement("div");
    leftDiv.classList.add("me-md-3", "flex-grow-1");
    leftDiv.style.minWidth = "0";

    const titleDiv = document.createElement("div");
    titleDiv.classList.add("fw-semibold", "mb-1");
    titleDiv.textContent = buildReceiptTitleText(receipt);
    leftDiv.appendChild(titleDiv);

    const addrDiv = document.createElement("div");
    addrDiv.classList.add("text-muted", "small");

    if (receipt.retailPlaceAddress) {
        addrDiv.textContent = receipt.retailPlaceAddress;
    } else {
        addrDiv.textContent = "";
        addrDiv.style.display = "none";
    }

    leftDiv.appendChild(addrDiv);

    const accountDiv = document.createElement("div");
    accountDiv.classList.add("small", "mt-1", "d-flex", "align-items-start", "gap-2", "flex-wrap");
    accountDiv.setAttribute("data-role", "receipt-accounts");

    const receiptAccounts = getReceiptAccounts(receipt);

    if (receiptAccounts.length > 0) {
        renderReceiptAccountBadges(accountDiv, receipt);
    } else {
        accountDiv.style.display = "none";
    }

    leftDiv.appendChild(accountDiv);

    const rightDiv = document.createElement("div");
    rightDiv.classList.add("d-flex", "flex-column", "align-items-start", "align-items-md-end", "ms-md-auto", "gap-2");

    const totalDiv = document.createElement("div");
    totalDiv.classList.add("text-start", "text-md-end");

    const totalSpan = document.createElement("div");
    totalSpan.classList.add("fw-bold", "fs-5");
    totalSpan.textContent = formatCurrency(receipt.totalSum);

    totalDiv.appendChild(totalSpan);

    const btnGroup = document.createElement("div");
    btnGroup.classList.add("d-flex", "gap-1", "flex-wrap", "w-100", "justify-content-start", "justify-content-md-end");

    const moneyMovementsBtn = document.createElement("button");
    moneyMovementsBtn.type = "button";
    moneyMovementsBtn.classList.add("btn", "btn-sm", "btn-outline-secondary", "flex-grow-1", "flex-md-grow-0");
    moneyMovementsBtn.setAttribute("data-action", "open-money-movements");
    moneyMovementsBtn.setAttribute("data-receipt-id", receipt.id);
    moneyMovementsBtn.textContent = buildMoneyMovementsButtonText(receipt);

    const openBtn = document.createElement("button");
    openBtn.type = "button";
    openBtn.classList.add("btn", "btn-sm", "btn-outline-primary", "flex-grow-1", "flex-md-grow-0");
    openBtn.setAttribute("data-action", "open");
    openBtn.textContent = "Открыть";

    const refreshBtn = document.createElement("button");
    refreshBtn.type = "button";
    refreshBtn.classList.add("btn", "btn-sm", "btn-outline-secondary", "flex-grow-1", "flex-md-grow-0");
    refreshBtn.setAttribute("data-action", "refresh");
    refreshBtn.textContent = "Обновить данные";

    let deleteBtn: HTMLButtonElement | null = null;

    if (receiptAccounts.length === 0) {
        deleteBtn = document.createElement("button");
        deleteBtn.type = "button";
        deleteBtn.classList.add("btn", "btn-sm", "btn-outline-danger", "flex-grow-1", "flex-md-grow-0");
        deleteBtn.setAttribute("data-action", "delete");
        deleteBtn.textContent = "Удалить";
    }

    btnGroup.appendChild(moneyMovementsBtn);
    btnGroup.appendChild(openBtn);
    btnGroup.appendChild(refreshBtn);

    rightDiv.appendChild(totalDiv);
    rightDiv.appendChild(btnGroup);

    topRow.appendChild(leftDiv);
    topRow.appendChild(rightDiv);
    cardBody.appendChild(topRow);

    if (deleteBtn) {
        btnGroup.appendChild(deleteBtn);
    }

    card.appendChild(cardBody);

    return card;
}

export function updateCardFromDto(cardElement: HTMLElement, receipt: ReceiptDto, formatCurrency: (value: number) => string): void {
    cardElement.setAttribute("data-receipt-id", receipt.id);

    const bodyElement = cardElement.querySelector<HTMLElement>(".card-body");
    if (!bodyElement) {
        return;
    }

    const datePlaceElement = bodyElement.querySelector<HTMLElement>(".fw-semibold");
    const addressElement = bodyElement.querySelector<HTMLElement>(".text-muted.small");
    const accountElement = bodyElement.querySelector<HTMLElement>('[data-role="receipt-accounts"]');
    const totalElement = bodyElement.querySelector<HTMLElement>(".fw-bold");
    const moneyMovementsButton = bodyElement.querySelector<HTMLButtonElement>('[data-action="open-money-movements"]');

    if (datePlaceElement && receipt.dateTime) {
        datePlaceElement.textContent = buildReceiptTitleText(receipt);
    }

    if (addressElement) {
        if (receipt.retailPlaceAddress) {
            addressElement.textContent = receipt.retailPlaceAddress;
            addressElement.style.display = "";
        } else {
            addressElement.textContent = "";
            addressElement.style.display = "none";
        }
    }

    if (accountElement) {
        accountElement.replaceChildren();
        accountElement.className = "small mt-1 d-flex align-items-start gap-2 flex-wrap";

        if (getReceiptAccounts(receipt).length > 0) {
            renderReceiptAccountBadges(accountElement, receipt);
            accountElement.style.display = "";
        } else {
            accountElement.style.display = "none";
        }
    }

    if (totalElement && typeof receipt.totalSum === "number") {
        totalElement.textContent = formatCurrency(receipt.totalSum);
    }

    if (moneyMovementsButton) {
        moneyMovementsButton.textContent = buildMoneyMovementsButtonText(receipt);
    }
}

export function renderReceiptAccountBadges(container: HTMLElement, receipt: ReceiptDto): void {
    container.replaceChildren();

    const accounts = getReceiptAccounts(receipt);
    for (const account of accounts) {
        const badge = document.createElement("button");
        badge.type = "button";
        badge.className = "btn btn-sm px-2 py-1 rounded-pill";
        badge.setAttribute("data-action", "edit-account-link");
        badge.setAttribute("data-account-receipt-id", account.receiptId);
        badge.setAttribute("data-account-id", account.id);
        badge.textContent = account.name;
        badge.style.backgroundColor = "transparent";
        badge.style.color = "#212529";
        badge.style.border = "2px solid " + account.colorHex;
        badge.style.transition = "background-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease";
        badge.disabled = !account.canEditReceipt;

        if (account.canEditReceipt) {
            badge.addEventListener("mouseenter", function () {
                badge.style.backgroundColor = account.colorHex + "14";
                badge.style.boxShadow = "0 0 0 0.2rem " + account.colorHex + "22";
                badge.style.transform = "translateY(-1px)";
            });

            badge.addEventListener("mouseleave", function () {
                badge.style.backgroundColor = "transparent";
                badge.style.boxShadow = "none";
                badge.style.transform = "translateY(0)";
            });
        } else {
            badge.title = "Чужой расшаренный счет недоступен для изменения чека.";
            badge.style.opacity = "0.65";
            badge.style.cursor = "not-allowed";
        }

        container.appendChild(badge);
    }
}

function buildReceiptTitleText(receipt: ReceiptDto): string {
    let dateText = "";
    if (receipt.dateTime) {
        const date = new Date(receipt.dateTime);
        dateText =
            date.toLocaleDateString("ru-RU") + " " +
            date.toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" });
    }

    const place = truncateTextForDisplay(normalizeSingleLineTextValue(receipt.retailPlace), RETAIL_PLACE_DISPLAY_LIMIT);
    return dateText + " — " + place;
}

function buildMoneyMovementsButtonText(receipt: ReceiptDto): string {
    const totalCount = Number(receipt.moneyMovementCount || 0) + Number(receipt.availableMoneyMovementCount || 0);
    return totalCount > 0 ? `Операций: ${totalCount}` : "Операции";
}

function truncateTextForDisplay(value: string, maxLength: number): string {
    const chars = Array.from(value);
    if (chars.length <= maxLength) {
        return value;
    }

    return chars.slice(0, maxLength).join("") + "...";
}
