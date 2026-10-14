import { getWriteErrorMessage, isIntentionalAbort } from "../../shared/requestCoordinator.js";

import { showAlertMessage } from "../../shared/alerts.js";

import { formatMoneyRub, formatRuNumber } from "../../shared/formatters.js";

import { linkMoneyMovementReceipt, loadLinkedReceipts, loadReceiptCandidates, openReceipt, unlinkMoneyMovementReceipt } from "./api.js";

import { renderLinkedReceipts, renderReceiptCandidates } from "./render.js";

import { type GetMoneyMovementReceiptCandidatesRequest, type MoneyMovementDto } from "./types.js";

import { renderReceiptDetails as renderReceiptDetailsModal } from "../reports/receipts/ui/receiptDetailsModal.js";
import type { MoneyMovementsSession } from "./state.js";
import type { MoneyMovementsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<MoneyMovementsSession,
        "candidateFilterKey" | "candidatePage" | "candidateRequests" | "commands" | "data" | "detailRequests" | "forgeryToken" | "linkedRequests" | "movementRequests" | "receiptsRevision">;
    bindings: Pick<MoneyMovementsBindings,
        "candidateNavigation" | "receiptDetailsModal" | "receiptsModal" | "ui">;
    findMovement: (moneyMovementId: string) => MoneyMovementDto | undefined;
    getMovementInfoText: (movement: MoneyMovementDto) => string;
    getErrorMessage: (error: unknown) => string;
    reloadMovements: () => Promise<void>;
    formatDateInput: (date: Date) => string;
}

export function createMovementReceiptLinksController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function resetReceiptsModal(): void {
        session.receiptsRevision++;
        session.linkedRequests.invalidate();
        session.candidateRequests.invalidate();
        session.detailRequests.invalidate();
        bindings.candidateNavigation.clear();
        session.data.selectedReceiptsMoneyMovementId = null;
        session.data.linkedReceipts = [];
        session.data.receiptCandidates = [];
        bindings.ui.receiptsInfo.textContent = "";
        hideReceiptsAlert();
        renderLinkedReceipts(bindings.ui.linkedReceipts, session.data.linkedReceipts);
        renderReceiptCandidates(bindings.ui.receiptCandidates, session.data.receiptCandidates);
    }

    async function handleOpenReceiptsClick(button: HTMLButtonElement): Promise<void> {
        const moneyMovementId = button.getAttribute("data-money-movement-id");
        if (!moneyMovementId) {
            return;
        }

        const movement = dependencies.findMovement(moneyMovementId);
        if (!movement) {
            showAlertMessage(bindings.ui.alert, "Операция не найдена в текущем списке.");
            return;
        }

        resetReceiptsModal();
        session.candidatePage = 1;
        bindings.candidateNavigation.clear();
        session.data.selectedReceiptsMoneyMovementId = moneyMovementId;
        bindings.ui.receiptsInfo.textContent = dependencies.getMovementInfoText(movement);
        initReceiptsFilters(movement);
        updateReceiptFilterState();
        hideReceiptsAlert();
        bindings.receiptsModal.show();
        await reloadReceiptDetails();
    }

    async function handleReloadReceiptCandidatesClick(): Promise<void> {
        try {
            hideReceiptsAlert();
            await reloadReceiptDetails();
        }
        catch (error) {
            showReceiptsAlert(dependencies.getErrorMessage(error));
        }
    }

    async function handleLinkedReceiptsClick(event: MouseEvent): Promise<void> {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const openButton = target.closest<HTMLButtonElement>('[data-action="open-receipt"]');
        if (openButton) {
            await handleOpenReceiptDetailsClick(openButton);
            return;
        }

        const button = target.closest<HTMLButtonElement>('[data-action="unlink-receipt"]');
        if (!button) {
            return;
        }

        const receiptId = button.getAttribute("data-receipt-id");
        if (!session.data.selectedReceiptsMoneyMovementId || !receiptId) {
            return;
        }

        const moneyMovementId = session.data.selectedReceiptsMoneyMovementId;
        const revision = session.receiptsRevision;
        const matchesList = session.movementRequests.capture();
        const isCurrent = () => revision === session.receiptsRevision && session.data.selectedReceiptsMoneyMovementId === moneyMovementId;
        const key = moneyMovementId + ":" + receiptId;
        if (session.commands.has(key) || button.disabled) { return; }
        session.commands.add(key);
        try {
        await runReceiptButtonAction(button, async () => {
            await unlinkMoneyMovementReceipt(session.forgeryToken, {
                moneyMovementId: moneyMovementId,
                receiptId: receiptId
            });
            if (!isCurrent()) { return; }
            await reloadReceiptDetails();
            if (isCurrent() && matchesList()) { await dependencies.reloadMovements(); }
        }, isCurrent);
        } finally { session.commands.delete(key); }
    }

    async function handleReceiptCandidatesClick(event: MouseEvent): Promise<void> {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const openButton = target.closest<HTMLButtonElement>('[data-action="open-receipt"]');
        if (openButton) {
            await handleOpenReceiptDetailsClick(openButton);
            return;
        }

        const button = target.closest<HTMLButtonElement>('[data-action="link-receipt"]');
        if (!button) {
            return;
        }

        const receiptId = button.getAttribute("data-receipt-id");
        if (!session.data.selectedReceiptsMoneyMovementId || !receiptId) {
            return;
        }

        const moneyMovementId = session.data.selectedReceiptsMoneyMovementId;
        const revision = session.receiptsRevision;
        const matchesList = session.movementRequests.capture();
        const isCurrent = () => revision === session.receiptsRevision && session.data.selectedReceiptsMoneyMovementId === moneyMovementId;
        const key = moneyMovementId + ":" + receiptId;
        if (session.commands.has(key) || button.disabled) { return; }
        session.commands.add(key);
        try {
        await runReceiptButtonAction(button, async () => {
            await linkMoneyMovementReceipt(session.forgeryToken, {
                moneyMovementId: moneyMovementId,
                receiptId: receiptId
            });
            if (!isCurrent()) { return; }
            await reloadReceiptDetails();
            if (isCurrent() && matchesList()) { await dependencies.reloadMovements(); }
        }, isCurrent);
        } finally { session.commands.delete(key); }
    }

    async function handleOpenReceiptDetailsClick(button: HTMLButtonElement): Promise<void> {
        const receiptId = button.getAttribute("data-receipt-id");
        if (!receiptId) {
            return;
        }

        const ticket = session.detailRequests.begin(receiptId);
        const revision = session.receiptsRevision;
        const isCurrent = () => ticket.isCurrent() && revision === session.receiptsRevision;
        await runReceiptButtonAction(button, async () => {
            const receipt = await openReceipt(session.forgeryToken, ticket.parameters, ticket.signal);
            if (!isCurrent()) { return; }
            renderReceiptDetailsModal(
                receipt,
                {
                    detailsList: bindings.ui.receiptDetailsList,
                    modalHeader: bindings.ui.receiptDetailsHeader,
                    modalTotal: bindings.ui.receiptDetailsTotal,
                    bootstrapModal: bindings.receiptDetailsModal
                },
                formatRuNumber,
                formatMoneyRub
            );
        }, isCurrent, false);
    }

    async function runReceiptButtonAction(button: HTMLButtonElement, action: () => Promise<void>, isCurrent: () => boolean, write = true): Promise<void> {
        if (button.disabled) { return; }
        const originalText = button.textContent;
        button.disabled = true;
        button.textContent = "...";

        try {
            hideReceiptsAlert();
            await action();
        }
        catch (error) {
            if (isCurrent() && (write || !isIntentionalAbort(error))) { showReceiptsAlert(write ? getWriteErrorMessage(error) : dependencies.getErrorMessage(error)); }
        }
        finally {
            if (!isCurrent()) { return; }
            button.disabled = false;
            button.textContent = originalText;
        }
    }

    async function reloadReceiptDetails(): Promise<void> {
        if (!session.data.selectedReceiptsMoneyMovementId) {
            return;
        }

        await Promise.all([
            reloadLinkedReceipts(),
            reloadReceiptCandidates()
        ]);
    }

    async function reloadLinkedReceipts(): Promise<void> {
        if (!session.data.selectedReceiptsMoneyMovementId) {
            return;
        }

        const selectedId = session.data.selectedReceiptsMoneyMovementId;
        const ticket = session.linkedRequests.begin({ selectedId, revision: session.receiptsRevision });
        try {
            const receipts = await loadLinkedReceipts(session.forgeryToken, ticket.parameters.selectedId, ticket.signal);
            if (!ticket.isCurrent() || ticket.parameters.revision !== session.receiptsRevision || selectedId !== session.data.selectedReceiptsMoneyMovementId) {
                return;
            }
            session.data.linkedReceipts = receipts;
            renderLinkedReceipts(bindings.ui.linkedReceipts, session.data.linkedReceipts);
        } catch (error) {
            if (ticket.isCurrent() && ticket.parameters.revision === session.receiptsRevision && !isIntentionalAbort(error)) { showReceiptsAlert(dependencies.getErrorMessage(error)); }
        }
    }

    function invalidateCandidates(): void {
        session.candidateRequests.invalidate();
        bindings.candidateNavigation.clear();
        session.data.receiptCandidates = [];
        renderReceiptCandidates(bindings.ui.receiptCandidates, []);
    }

    async function reloadReceiptCandidates(): Promise<void> {
        if (!session.data.selectedReceiptsMoneyMovementId) {
            return;
        }

        const request = readReceiptCandidatesRequest(session.data.selectedReceiptsMoneyMovementId);
        const key = JSON.stringify(request);
        if (key !== session.candidateFilterKey) {
            session.candidatePage = 1;
            session.candidateFilterKey = key;
        }
        const selectedId = session.data.selectedReceiptsMoneyMovementId;
        const ticket = session.candidateRequests.begin({ ...request, page: session.candidatePage, pageSize: 20 });
        const revision = session.receiptsRevision;
        bindings.candidateNavigation.clear();
        try {
            const response = await loadReceiptCandidates(session.forgeryToken, ticket.parameters, ticket.signal);
            if (selectedId !== session.data.selectedReceiptsMoneyMovementId || !ticket.isCurrent() || revision !== session.receiptsRevision
                || key !== JSON.stringify(readReceiptCandidatesRequest(selectedId))) {
                return;
            }
            session.candidatePage = response.page;
            session.data.receiptCandidates = response.items;
            bindings.candidateNavigation.render(response);
            renderReceiptCandidates(bindings.ui.receiptCandidates, session.data.receiptCandidates);
        } catch (error) {
            if (ticket.isCurrent() && revision === session.receiptsRevision
                && key === JSON.stringify(readReceiptCandidatesRequest(selectedId)) && !isIntentionalAbort(error)) { showReceiptsAlert(dependencies.getErrorMessage(error)); }
        }
    }

    function readReceiptCandidatesRequest(moneyMovementId: string): GetMoneyMovementReceiptCandidatesRequest {
        const amountTolerance = Number(bindings.ui.receiptsAmountToleranceInput.value);
        const timeWindowHours = Number(bindings.ui.receiptsTimeWindowHoursInput.value);

        return {
            moneyMovementId: moneyMovementId,
            useTimeWindow: bindings.ui.receiptsUseTimeWindowInput.checked,
            timeWindowHours: Number.isFinite(timeWindowHours) ? timeWindowHours : null,
            dateFrom: bindings.ui.receiptsDateFromInput.value || null,
            dateTo: bindings.ui.receiptsDateToInput.value || null,
            useAmountFilter: bindings.ui.receiptsUseAmountFilterInput.checked,
            amountTolerance: Number.isFinite(amountTolerance) ? amountTolerance : null,
            excludeLinkedReceipts: bindings.ui.receiptsExcludeLinkedInput.checked
        };
    }

    function initReceiptsFilters(movement: MoneyMovementDto): void {
        const occurredAt = new Date(movement.occurredAt);
        const date = Number.isNaN(occurredAt.getTime()) ? new Date() : occurredAt;
        const dateFrom = new Date(date);
        const dateTo = new Date(date);
        dateFrom.setDate(dateFrom.getDate() - 1);
        dateTo.setDate(dateTo.getDate() + 1);

        bindings.ui.receiptsDateFromInput.value = dependencies.formatDateInput(dateFrom);
        bindings.ui.receiptsDateToInput.value = dependencies.formatDateInput(dateTo);
        bindings.ui.receiptsUseTimeWindowInput.checked = false;
        bindings.ui.receiptsUseAmountFilterInput.checked = true;
        bindings.ui.receiptsExcludeLinkedInput.checked = true;
        bindings.ui.receiptsAmountToleranceInput.value = "1";
        bindings.ui.receiptsTimeWindowHoursInput.value = "1";
    }

    function updateReceiptFilterState(): void {
        const useTimeWindow = bindings.ui.receiptsUseTimeWindowInput.checked;
        bindings.ui.receiptsDateFromInput.disabled = useTimeWindow;
        bindings.ui.receiptsDateToInput.disabled = useTimeWindow;
        bindings.ui.receiptsTimeWindowHoursInput.disabled = !useTimeWindow;
        bindings.ui.receiptsAmountToleranceInput.disabled = !bindings.ui.receiptsUseAmountFilterInput.checked;
    }

    function showReceiptsAlert(message: string): void {
        bindings.ui.receiptsAlert.textContent = message;
        bindings.ui.receiptsAlert.classList.toggle("d-none", !message);
    }

    function hideReceiptsAlert(): void {
        showReceiptsAlert("");
    }

    return { reloadReceiptCandidates, showReceiptsAlert, handleReloadReceiptCandidatesClick, updateReceiptFilterState, handleLinkedReceiptsClick, invalidateCandidates, handleReceiptCandidatesClick, resetReceiptsModal, handleOpenReceiptsClick };
}
