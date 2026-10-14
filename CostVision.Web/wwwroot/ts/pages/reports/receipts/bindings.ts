

import { BootstrapModal } from "../../../shared/bootstrap.js";

import { ReceiptMoneyMovementsModalController } from "./modals/receiptMoneyMovementsModal.js";

export class ReceiptsBindings {
    listContainer!: HTMLElement;
    modalElement!: HTMLElement;
    detailsList!: HTMLElement;
    modalHeader!: HTMLElement;
    modalTotal!: HTMLElement;
    bootstrapModal!: BootstrapModal;
    moveReceiptAccountModalElement!: HTMLElement;
    moveReceiptAccountBootstrapModal!: BootstrapModal;
    moveReceiptSourceAccountElement!: HTMLElement;
    moveReceiptTargetAccountSelect!: HTMLSelectElement;
    moveReceiptAccountAlertElement!: HTMLElement;
    confirmMoveReceiptAccountButton!: HTMLButtonElement;
    removeReceiptFromAccountButton!: HTMLButtonElement;
    dateFromInput!: HTMLInputElement;
    dateToInput!: HTMLInputElement;
    applyFilterButton!: HTMLButtonElement;
    receiptPeriodPresetSelect!: HTMLSelectElement;
    receiptsCountElement!: HTMLElement;
    receiptsSumElement!: HTMLElement;
    receiptPageSizeSelect!: HTMLSelectElement;
    receiptsPaginationElement!: HTMLElement;
    deleteModalElement!: HTMLElement;
    deleteBootstrapModal!: BootstrapModal;
    deleteConfirmButton!: HTMLButtonElement;
    deleteModalTitleElement!: HTMLElement;
    deleteModalMessageElement!: HTMLElement;
    deleteModalReceiptInfoElement!: HTMLElement;
    receiptSearchInput!: HTMLInputElement;
    receiptSearchModeSelect!: HTMLSelectElement;
    receiptAccountFilterSelect!: HTMLSelectElement;
    receiptAccountFilterError!: HTMLElement;
    receiptOperationFilterSelect!: HTMLSelectElement;
    receiptMoneyMovementsModal!: ReceiptMoneyMovementsModalController;
}
