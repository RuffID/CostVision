import { renderStoreReceipts, renderStoreReceiptsPagination } from "./receiptView.js";
import { createReceiptWorkflow } from "../../shared/receipts/workflow.js";
import { isIntentionalAbort } from "../../shared/requestCoordinator.js";

import { clearElement, requireElementById } from "../../shared/dom.js";

import { renderReceiptDetails as renderReceiptDetailsModal } from "../reports/receipts/ui/receiptDetailsModal.js";
import { updateCardFromDto } from "../reports/receipts/ui/receiptCards.js";
import type { ReceiptDto } from "../reports/receipts/types.js";
import { getStoreReceipts, openStoreReceipt, refreshStoreReceipt } from "./api.js";

import { formatReportTotals } from "../reports/receipts/render.js";

import type { StoreListItem } from "./types.js";
import type { StoresSession } from "./state.js";
import type { StoresBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<StoresSession,
        "detailRequests" | "receiptWrites" | "selectedStoreReceipts" | "selectedStoreReceiptsGroupKey" | "selectedStoreReceiptsPage" | "selectedStoreReceiptsStoreId" | "storeContext" | "storeReceiptRequests">;
    bindings: Pick<StoresBindings,
        "receiptDetailsHeaderElement" | "receiptDetailsListElement" | "receiptDetailsModal" | "receiptDetailsTotalElement" | "receiptMoneyMovementsModal" | "storeReceiptsAlertElement" | "storeReceiptsListElement" | "storeReceiptsModal" | "storeReceiptsPageSizeSelect" | "storeReceiptsPaginationElement" | "storeReceiptsTitleElement">;
    getStoreByGroupKey: (storeGroupKey: string) => StoreListItem;
    getStoreById: (storeId: string) => StoreListItem;
    formatCurrency: (value: number) => string;
    getErrorMessage: (error: unknown) => string;
    openMoveReceiptAccountModal: (receiptId: string, sourceAccountId: string) => Promise<void>;
    formatNumber: (value: number) => string;
}

export function createStoreReceiptsController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;
    const workflow = createReceiptWorkflow({
        detailRequests: session.detailRequests,
        writes: session.receiptWrites,
        captureReadContext: () => session.storeContext.capture(),
        captureWriteContext: () => {
            const matchesStore = session.storeContext.capture();
            const matchesPage = session.storeReceiptRequests.capture();
            return () => matchesStore() && matchesPage();
        },
        open: (id, signal) => openStoreReceipt(id, signal),
        refresh: id => refreshStoreReceipt(id),
        showDetails: receipt => renderReceiptDetailsModal(receipt, {
            detailsList: bindings.receiptDetailsListElement,
            modalHeader: bindings.receiptDetailsHeaderElement,
            modalTotal: bindings.receiptDetailsTotalElement,
            bootstrapModal: bindings.receiptDetailsModal
        }, dependencies.formatNumber, dependencies.formatCurrency),
        applyRefresh: (card, receipt) => {
            updateCardFromDto(card, receipt, dependencies.formatCurrency);
            replaceSelectedReceipt(receipt);
        },
        showError: showStoreReceiptsAlert
    });

    async function openStoreReceiptsModal(button: HTMLButtonElement): Promise<void> {
        const storeId = button.dataset.storeGroupKey ? null : button.dataset.storeId ?? null;
        const groupKey = button.dataset.storeGroupKey ?? null;
        const store = groupKey ? dependencies.getStoreByGroupKey(groupKey) : dependencies.getStoreById(button.dataset.storeId ?? "");

        session.storeContext.invalidate();
        session.storeReceiptRequests.invalidate();
        session.detailRequests.invalidate();
        session.selectedStoreReceipts = [];
        session.selectedStoreReceiptsStoreId = storeId;
        session.selectedStoreReceiptsGroupKey = groupKey;
        session.selectedStoreReceiptsPage = 1;
        bindings.storeReceiptsTitleElement.textContent = "Чеки магазина - " + (store.displayName || store.name || "Магазин");
        requireElementById<HTMLElement>("storeReceiptsSummary").textContent = "Итоги ещё не загружены";
        hideStoreReceiptsAlert();
        clearElement(bindings.storeReceiptsListElement);
        clearElement(bindings.storeReceiptsPaginationElement);
        bindings.storeReceiptsModal.show();

        await loadSelectedStoreReceiptsPage(1);
    }

    async function loadSelectedStoreReceiptsPage(page: number): Promise<void> {
        if (!session.selectedStoreReceiptsStoreId && !session.selectedStoreReceiptsGroupKey) {
            throw new Error("Не выбран магазин для просмотра чеков.");
        }

        const request = session.storeReceiptRequests.begin({ storeId: session.selectedStoreReceiptsStoreId,
            groupKey: session.selectedStoreReceiptsGroupKey, page, pageSize: Number(bindings.storeReceiptsPageSizeSelect.value) });
        const p = request.parameters;
        try {
            hideStoreReceiptsAlert();
            const result = await getStoreReceipts(p.storeId, p.groupKey, p.page, p.pageSize, request.signal);
            if (!request.isCurrent()) { return; }
            session.selectedStoreReceipts = result.items;
            session.selectedStoreReceiptsPage = result.page;
            requireElementById<HTMLElement>("storeReceiptsSummary").textContent = formatReportTotals(result.totalSum, result.incomeSum, dependencies.formatCurrency);
            renderStoreReceipts(bindings.storeReceiptsListElement, result.items, dependencies.formatCurrency);
            renderStoreReceiptsPagination(bindings.storeReceiptsPaginationElement, requireElementById("storeReceiptsPageSizeContainer"), result, page => { void loadSelectedStoreReceiptsPage(page); });
        }
        catch (error) {
            if (!request.isCurrent() || isIntentionalAbort(error)) { return; }
            showStoreReceiptsAlert(dependencies.getErrorMessage(error));
        }
    }

    async function handleStoreReceiptClick(event: MouseEvent): Promise<void> {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const accountButton = target.closest<HTMLButtonElement>('[data-action="edit-account-link"]');
        const assignAccountButton = target.closest<HTMLButtonElement>('[data-action="assign-account-link"]');
        if (accountButton) {
            if (accountButton.disabled) {
                return;
            }

            const receiptId = accountButton.dataset.accountReceiptId;
            const accountId = accountButton.dataset.accountId;
            if (receiptId && accountId) {
                await dependencies.openMoveReceiptAccountModal(receiptId, accountId);
            }
            return;
        }

        if (assignAccountButton) {
            const receiptId = assignAccountButton.closest<HTMLElement>(".card")?.dataset.receiptId;
            if (receiptId) {
                await dependencies.openMoveReceiptAccountModal(receiptId, "");
            }
            return;
        }

        const moneyMovementsButton = target.closest<HTMLButtonElement>('[data-action="open-money-movements"]');
        if (moneyMovementsButton) {
            const receiptId = moneyMovementsButton.dataset.receiptId;
            if (receiptId) {
                await bindings.receiptMoneyMovementsModal.open(receiptId);
            }
            return;
        }

        const openButton = target.closest<HTMLButtonElement>('[data-action="open"]');
        if (openButton) {
            const receiptId = getReceiptCardId(openButton);
            if (receiptId) {
                await openReceipt(receiptId);
            }
            return;
        }

        const refreshButton = target.closest<HTMLButtonElement>('[data-action="refresh"]');
        if (refreshButton) {
            const card = refreshButton.closest<HTMLElement>(".card");
            const receiptId = card?.dataset.receiptId;
            if (card && receiptId) {
                await refreshReceipt(receiptId, card, refreshButton);
            }
        }
    }

    function getReceiptCardId(element: HTMLElement): string | null {
        return element.closest<HTMLElement>(".card")?.dataset.receiptId ?? null;
    }

    function openReceipt(receiptId: string): Promise<void> {
        return workflow.open(receiptId);
    }

    function refreshReceipt(receiptId: string, card: HTMLElement, button: HTMLButtonElement): Promise<void> {
        return workflow.refresh(receiptId, card, button);
    }

    function replaceSelectedReceipt(receipt: ReceiptDto): void {
        session.selectedStoreReceipts = session.selectedStoreReceipts.map(item => item.id === receipt.id ? receipt : item);
    }

    function clearSelectedStoreReceipts(): void {
        session.storeContext.invalidate();
        session.storeReceiptRequests.invalidate();
        session.detailRequests.invalidate();
        session.selectedStoreReceiptsStoreId = null;
        session.selectedStoreReceiptsGroupKey = null;
        session.selectedStoreReceiptsPage = 1;
        session.selectedStoreReceipts = [];
        hideStoreReceiptsAlert();
    }

    function showStoreReceiptsAlert(message: string): void {
        bindings.storeReceiptsAlertElement.textContent = message;
        bindings.storeReceiptsAlertElement.classList.toggle("d-none", !message);
    }

    function hideStoreReceiptsAlert(): void {
        showStoreReceiptsAlert("");
    }

    return { loadSelectedStoreReceiptsPage, handleStoreReceiptClick, clearSelectedStoreReceipts, openStoreReceiptsModal, showStoreReceiptsAlert };
}
