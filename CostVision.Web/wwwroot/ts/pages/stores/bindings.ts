

import { type BootstrapModal } from "../../shared/bootstrap.js";

import { type TableLoadingIndicator } from "../../shared/tableLoadingIndicator.js";

import { type ReceiptMoneyMovementsModalController } from "../reports/receipts/modals/receiptMoneyMovementsModal.js";

import { type StoresUi } from "./render.js";

export class StoresBindings {
    searchInput!: HTMLInputElement;
    showOriginalNamesInput!: HTMLInputElement;
    groupByNameInput!: HTMLInputElement;
    pageSizeInputs!: HTMLSelectElement[];
    alertElement!: HTMLElement;
    ui!: StoresUi;
    tableLoadingIndicator!: TableLoadingIndicator;
    adaptiveNameModal!: BootstrapModal;
    originalNameInput!: HTMLInputElement;
    adaptiveNameInput!: HTMLInputElement;
    clearAdaptiveNameButton!: HTMLButtonElement;
    saveAdaptiveNameButton!: HTMLButtonElement;
    storeReceiptsModalElement!: HTMLElement;
    storeReceiptsModal!: BootstrapModal;
    storeReceiptsTitleElement!: HTMLElement;
    storeReceiptsAlertElement!: HTMLElement;
    storeReceiptsListElement!: HTMLElement;
    storeReceiptsPaginationElement!: HTMLElement;
    storeReceiptsPageSizeSelect!: HTMLSelectElement;
    receiptDetailsModal!: BootstrapModal;
    receiptDetailsListElement!: HTMLElement;
    receiptDetailsHeaderElement!: HTMLElement;
    receiptDetailsTotalElement!: HTMLElement;
    receiptMoneyMovementsModal!: ReceiptMoneyMovementsModalController;
    moveReceiptAccountModalElement!: HTMLElement;
    moveReceiptAccountModal!: BootstrapModal;
    moveReceiptSourceAccountElement!: HTMLElement;
    moveReceiptTargetAccountSelect!: HTMLSelectElement;
    moveReceiptAccountAlertElement!: HTMLElement;
    confirmMoveReceiptAccountButton!: HTMLButtonElement;
    removeReceiptFromAccountButton!: HTMLButtonElement;
    removeReceiptAccountModal!: BootstrapModal;
    removeReceiptAccountModalTitleElement!: HTMLElement;
    removeReceiptAccountModalMessageElement!: HTMLElement;
    removeReceiptAccountModalReceiptInfoElement!: HTMLElement;
    confirmRemoveReceiptAccountButton!: HTMLButtonElement;
    accountFilterSelect!: HTMLSelectElement;
    storesCountElement!: HTMLElement;
    storesSumElement!: HTMLElement;
}
