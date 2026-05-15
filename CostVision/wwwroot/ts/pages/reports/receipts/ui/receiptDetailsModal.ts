import { BootstrapModal } from "../../../../shared/bootstrap.js";
import { clearElement } from "../../../../shared/dom.js";
import { getReceiptAccountNamesText } from "../state/accountModel.js";
import { ReceiptDto } from "../types.js";

export interface ReceiptDetailsModalElements {
    detailsList: HTMLElement;
    modalHeader: HTMLElement;
    modalTotal: HTMLElement;
    bootstrapModal: BootstrapModal;
}

export function renderReceiptDetails(data: ReceiptDto, elements: ReceiptDetailsModalElements, formatNumber: (value: number) => string, formatCurrency: (value: number) => string): void {
    clearElement(elements.detailsList);

    if (Array.isArray(data.items)) {
        data.items.forEach(function (item) {
            const row = document.createElement("div");
            row.classList.add("border", "rounded", "p-2", "d-flex", "justify-content-between", "align-items-center");

            const left = document.createElement("div");
            left.classList.add("me-3");

            const nameDiv = document.createElement("div");
            nameDiv.classList.add("fw-semibold");
            nameDiv.textContent = item.name;

            const metaDiv = document.createElement("div");
            metaDiv.classList.add("text-muted", "small");
            metaDiv.textContent = formatNumber(item.quantity) + " × " + formatCurrency(item.price);

            left.appendChild(nameDiv);
            left.appendChild(metaDiv);

            const right = document.createElement("div");
            right.classList.add("text-end", "fw-bold");
            right.textContent = formatCurrency(item.sum);

            row.appendChild(left);
            row.appendChild(right);

            elements.detailsList.appendChild(row);
        });
    }

    const headerParts: { label: string; value: string }[] = [];
    if (data.dateTime) {
        const date = new Date(data.dateTime);
        headerParts.push({ label: "Время чека:", value: date.toLocaleString("ru-RU") });
    }
    if (data.retailPlace) {
        headerParts.push({ label: "Магазин:", value: data.retailPlace });
    }
    if (data.retailPlaceAddress) {
        headerParts.push({ label: "Адрес:", value: data.retailPlaceAddress });
    }
    const accountNamesText = getReceiptAccountNamesText(data);
    if (accountNamesText) {
        headerParts.push({ label: "Счета:", value: accountNamesText });
    }

    clearElement(elements.modalHeader);
    headerParts.forEach(function (part) {
        appendReceiptHeaderPart(elements.modalHeader, part.label, part.value);
    });

    if (data.fiscalDriveNumber) {
        appendReceiptHeaderPart(elements.modalHeader, "ФН:", data.fiscalDriveNumber);
    }
    if (data.fiscalDocumentNumber) {
        appendReceiptHeaderPart(elements.modalHeader, "ФД:", data.fiscalDocumentNumber);
    }
    if (data.fiscalSign) {
        appendReceiptHeaderPart(elements.modalHeader, "ФП:", data.fiscalSign);
    }

    if (typeof data.totalSum === "number") {
        elements.modalTotal.textContent = "Итого: " + formatCurrency(data.totalSum);
    } else {
        elements.modalTotal.textContent = "";
    }

    if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }
    elements.bootstrapModal.show();
}

function appendReceiptHeaderPart(container: HTMLElement, label: string, value: string): void {
    const row = document.createElement("div");
    const labelElement = document.createElement("strong");
    labelElement.textContent = label;
    row.append(labelElement, document.createTextNode(" " + value));
    container.appendChild(row);
}
