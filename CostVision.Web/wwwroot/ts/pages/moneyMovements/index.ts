import { createRequestCoordinator, getWriteErrorMessage, isIntentionalAbort } from "../../shared/requestCoordinator.js";
import { createPageNavigation } from "../../shared/paging.js";
import type { MoneyMovementList } from "./types.js";
import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { BootstrapModal, createBootstrapModal } from "../../shared/bootstrap.js";
import { formatMoneyRub, formatRuNumber } from "../../shared/formatters.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { createMoneyMovement, deleteMoneyMovement, linkMoneyMovementReceipt, loadAccounts, loadImportBanks, loadLinkedReceipts, loadMoneyMovements, loadReceiptCandidates, moveMoneyMovementToAccount, openReceipt, unlinkMoneyMovementReceipt, updateMoneyMovementComment } from "./api.js";
import { formatDateForQuery, getDateRangeByPeriodPreset } from "./dateRange.js";
import { initMoneyMovementHelpTooltips } from "./helpTooltips.js";
import { initMoneyMovementImportModalController, type MoneyMovementImportModalController } from "./importModal.js";
import { MONEY_MOVEMENT_ACCOUNT_FILTER_WITHOUT_ACCOUNT, renderAccountOptions, renderImportBankOptions, renderLinkedReceipts, renderMoneyMovementList, renderReceiptCandidates, renderSummary } from "./render.js";
import { state } from "./state.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type CreateMoneyMovementRequest, type GetMoneyMovementReceiptCandidatesRequest, type MoneyMovementDto, type MoneyMovementType, type UserAccountViewModel } from "./types.js";
import { getMoneyMovementsUi, type MoneyMovementsUi } from "./ui.js";
import { renderReceiptDetails as renderReceiptDetailsModal } from "../reports/receipts/ui/receiptDetailsModal.js";

let movementPage = 1;
let candidatePage = 1;
let movementFilterKey = "";
let candidateFilterKey = "";
const movementRequests = createRequestCoordinator();
const initialRequests = createRequestCoordinator();
const pageContext = createRequestCoordinator();
const linkedRequests = createRequestCoordinator();
const candidateRequests = createRequestCoordinator();
const detailRequests = createRequestCoordinator();
let receiptsRevision = 0;
let creating = false;
const commands = new Set<string>();
window.addEventListener("pagehide", () => {
    initialRequests.invalidate(); pageContext.invalidate(); movementRequests.invalidate(); linkedRequests.invalidate(); candidateRequests.invalidate(); detailRequests.invalidate(); receiptsRevision++;
    if (state.searchDebounceTimerId !== null) { window.clearTimeout(state.searchDebounceTimerId); state.searchDebounceTimerId = null; }
});
let movementTotals: MoneyMovementList | null = null;
let movementNavigation: ReturnType<typeof createPageNavigation>;
let candidateNavigation: ReturnType<typeof createPageNavigation>;
let ui: MoneyMovementsUi;
let forgeryToken: string | null;
let moveAccountModal: BootstrapModal;
let deleteModal: BootstrapModal;
let importModal: BootstrapModal;
let receiptsModal: BootstrapModal;
let receiptDetailsModal: BootstrapModal;
let importController: MoneyMovementImportModalController;
document.addEventListener("DOMContentLoaded", () => {
    initMoneyMovementsPage();
});

async function initMoneyMovementsPage(): Promise<void> {
    ui = getMoneyMovementsUi();
    forgeryToken = getRequestVerificationToken();
    movementNavigation = createPageNavigation(ui.list, async page => {
        movementPage = page;
        await reloadMovements();
    }, error => showAlertMessage(ui.alert, getErrorMessage(error)));
    candidateNavigation = createPageNavigation(ui.receiptCandidates, async page => {
        candidatePage = page;
        await reloadReceiptCandidates();
    }, error => showReceiptsAlert(getErrorMessage(error)), "Документов");
    moveAccountModal = createBootstrapModal(ui.moveAccountModal);
    deleteModal = createBootstrapModal(ui.deleteModal);
    importModal = createBootstrapModal(ui.importModal);
    receiptsModal = createBootstrapModal(ui.receiptsModal);
    receiptDetailsModal = createBootstrapModal(ui.receiptDetailsModal);
    importController = initMoneyMovementImportModalController({
        ui: ui,
        state: state,
        importModal: importModal,
        getForgeryToken: () => forgeryToken,
        reloadMovements: reloadMovements
    });
    initMoneyMovementHelpTooltips(ui);
    initDefaultDates();
    bindEvents();
    await loadInitialData();
}

function bindEvents(): void {
    ui.createForm.addEventListener("submit", handleCreateSubmit);
    ui.amountInput.addEventListener("input", handleAmountInput);
    ui.applyFilterButton.addEventListener("click", handleApplyFilterClick);
    ui.periodPresetSelect.addEventListener("change", handlePeriodPresetChange);
    ui.searchInput.addEventListener("input", handleSearchInput);
    ui.receiptFilterSelect.addEventListener("change", () => { void handleApplyFilterClick(); });
    ui.list.addEventListener("click", handleMoneyMovementListClick);
    ui.moveTargetAccountSelect.addEventListener("change", updateMoveActionState);
    ui.confirmMoveAccountButton.addEventListener("click", handleConfirmMoveAccountClick);
    ui.removeFromAccountButton.addEventListener("click", handleRemoveFromAccountClick);
    ui.confirmDeleteButton.addEventListener("click", handleConfirmDeleteClick);
    ui.receiptsReloadCandidatesButton.addEventListener("click", handleReloadReceiptCandidatesClick);
    ui.receiptsUseTimeWindowInput.addEventListener("change", updateReceiptFilterState);
    ui.receiptsUseAmountFilterInput.addEventListener("change", updateReceiptFilterState);
    ui.linkedReceipts.addEventListener("click", handleLinkedReceiptsClick);
    for (const input of [ui.receiptsDateFromInput, ui.receiptsDateToInput, ui.receiptsAmountToleranceInput,
        ui.receiptsTimeWindowHoursInput, ui.receiptsUseTimeWindowInput, ui.receiptsUseAmountFilterInput, ui.receiptsExcludeLinkedInput]) {
        input.addEventListener("change", invalidateCandidates);
        input.addEventListener("input", invalidateCandidates);
    }
    for (const input of [ui.dateFromInput, ui.dateToInput, ui.filterAccountSelect]) {
        input.addEventListener("change", () => movementRequests.invalidate());
    }
    ui.receiptCandidates.addEventListener("click", handleReceiptCandidatesClick);

    ui.moveAccountModal.addEventListener("hide.bs.modal", resetMoveModal);
    ui.deleteModal.addEventListener("hide.bs.modal", () => { state.pendingAccountAction = null; resetDeleteModal(); });
    ui.receiptsModal.addEventListener("hide.bs.modal", resetReceiptsModal);
    ui.receiptDetailsModal.addEventListener("hide.bs.modal", () => detailRequests.invalidate());
}

async function loadInitialData(): Promise<void> {
    const request = initialRequests.begin(forgeryToken);
    try {
        hideAlertMessage(ui.alert);
        const accounts = await loadAccounts(request.parameters, request.signal);
        if (!request.isCurrent()) { return; }
        const banks = await loadImportBanks(request.parameters, request.signal);
        if (!request.isCurrent()) { return; }
        state.accounts = accounts;
        state.importBanks = banks;
        renderAccountOptions(ui.accountSelect, state.accounts, false);
        renderAccountOptions(ui.filterAccountSelect, state.accounts, true);
        renderAccountOptions(ui.importAccountSelect, state.accounts, false);
        renderImportBankOptions(ui.importBankSelect, state.importBanks);

        if (state.accounts.length > 0) {
            ui.accountSelect.value = state.accounts[0].id;
            ui.importAccountSelect.value = state.accounts[0].id;
        }

        importController.renderState();
        await reloadMovements();
    }
    catch (error) {
        if (request.isCurrent() && !isIntentionalAbort(error)) { showAlertMessage(ui.alert, getErrorMessage(error)); }
    }
}

function resetReceiptsModal(): void {
    receiptsRevision++;
    linkedRequests.invalidate();
    candidateRequests.invalidate();
    detailRequests.invalidate();
    candidateNavigation.clear();
    state.selectedReceiptsMoneyMovementId = null;
    state.linkedReceipts = [];
    state.receiptCandidates = [];
    ui.receiptsInfo.textContent = "";
    hideReceiptsAlert();
    renderLinkedReceipts(ui.linkedReceipts, state.linkedReceipts);
    renderReceiptCandidates(ui.receiptCandidates, state.receiptCandidates);
}

async function handleCreateSubmit(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    if (creating) { return; }
    creating = true;
    const matchesList = movementRequests.capture();
    const matchesPage = pageContext.capture();
    let draft: string;

    try {
        hideAlertMessage(ui.alert);
        ui.createButton.disabled = true;
        const request = readCreateRequest();
        draft = JSON.stringify([ui.accountSelect.value, ui.amountInput.value, ui.occurredAtInput.value, ui.typeSelect.value, ui.commentInput.value]);
        await createMoneyMovement(forgeryToken, request);
        if (!matchesPage()) { return; }
        if (draft === JSON.stringify([ui.accountSelect.value, ui.amountInput.value, ui.occurredAtInput.value, ui.typeSelect.value, ui.commentInput.value])) {
            ui.amountInput.value = "";
            ui.commentInput.value = "";
            updateMovementTypeByAmount();
        }
        if (matchesList()) { await reloadMovements(); }
    }
    catch (error) {
        if (matchesList()) { showAlertMessage(ui.alert, getWriteErrorMessage(error)); }
    }
    finally {
        creating = false;
        if (matchesPage()) { ui.createButton.disabled = false; }
    }
}

async function handleApplyFilterClick(): Promise<void> {
    try {
        hideAlertMessage(ui.alert);
        await reloadMovements();
    }
    catch (error) {
        showAlertMessage(ui.alert, getErrorMessage(error));
    }
}

async function reloadMovements(): Promise<void> {
    state.dateFrom = ui.dateFromInput.value;
    state.dateTo = ui.dateToInput.value;
    state.selectedAccountId = ui.filterAccountSelect.value;
    const accountId = state.selectedAccountId === MONEY_MOVEMENT_ACCOUNT_FILTER_WITHOUT_ACCOUNT
        ? "00000000-0000-0000-0000-000000000000"
        : state.selectedAccountId;
    const key = JSON.stringify([state.dateFrom, state.dateTo, accountId, state.searchQuery, ui.receiptFilterSelect.value]);
    if (key !== movementFilterKey) {
        movementPage = 1;
        movementFilterKey = key;
    }
    const request = movementRequests.begin({ dateFrom: state.dateFrom, dateTo: state.dateTo, accountId,
        page: movementPage, search: state.searchQuery, receiptFilter: ui.receiptFilterSelect.value });
    const p = request.parameters;
    const matchesFilters = () => key === JSON.stringify([ui.dateFromInput.value, ui.dateToInput.value,
        ui.filterAccountSelect.value === MONEY_MOVEMENT_ACCOUNT_FILTER_WITHOUT_ACCOUNT ? "00000000-0000-0000-0000-000000000000" : ui.filterAccountSelect.value,
        state.searchQuery, ui.receiptFilterSelect.value]);
    movementTotals = null;
    state.movements = [];
    movementNavigation.clear();
    renderMovements();
    try {
        const response = await loadMoneyMovements(forgeryToken, p.dateFrom, p.dateTo, p.accountId,
            p.page, p.search, p.receiptFilter, request.signal);
        if (!request.isCurrent() || !matchesFilters()) {
            return;
        }
        movementTotals = response;
        movementPage = response.page;
        state.movements = response.items;
        movementNavigation.render(response);
        state.editingCommentMovementId = null;
        renderMovements();
    } catch (error) {
        if (request.isCurrent() && matchesFilters() && !isIntentionalAbort(error)) { showAlertMessage(ui.alert, getErrorMessage(error)); }
    }
}

function renderMovements(): void {
    renderMoneyMovementList(ui.list, state.movements, state.accounts, state.editingCommentMovementId);
    renderSummary(ui.count, ui.incomeSum, ui.expenseSum, movementTotals);
}

function handleMoneyMovementListClick(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const editCommentButton = target.closest<HTMLButtonElement>('[data-action="edit-comment"]');
    if (editCommentButton) {
        startCommentEdit(editCommentButton.getAttribute("data-money-movement-id"));
        return;
    }

    const cancelCommentButton = target.closest<HTMLButtonElement>('[data-action="cancel-comment"]');
    if (cancelCommentButton) {
        cancelCommentEdit();
        return;
    }

    const saveCommentButton = target.closest<HTMLButtonElement>('[data-action="save-comment"]');
    if (saveCommentButton) {
        handleSaveCommentClick(saveCommentButton).catch(error => showAlertMessage(ui.alert, getErrorMessage(error)));
        return;
    }

    const accountButton = target.closest<HTMLButtonElement>('[data-action="edit-account-link"]');
    const receiptsButton = target.closest<HTMLButtonElement>('[data-action="open-receipts"]');
    if (receiptsButton) {
        handleOpenReceiptsClick(receiptsButton).catch(error => showAlertMessage(ui.alert, getErrorMessage(error)));
        return;
    }

    if (!accountButton || accountButton.disabled) {
        return;
    }

    const moneyMovementId = accountButton.getAttribute("data-money-movement-id");
    const accountId = accountButton.getAttribute("data-account-id");
    if (!moneyMovementId || accountId === null) {
        return;
    }

    openMoveAccountModal(moneyMovementId, accountId);
}

async function handleOpenReceiptsClick(button: HTMLButtonElement): Promise<void> {
    const moneyMovementId = button.getAttribute("data-money-movement-id");
    if (!moneyMovementId) {
        return;
    }

    const movement = findMovement(moneyMovementId);
    if (!movement) {
        showAlertMessage(ui.alert, "Операция не найдена в текущем списке.");
        return;
    }

    resetReceiptsModal();
    candidatePage = 1;
    candidateNavigation.clear();
    state.selectedReceiptsMoneyMovementId = moneyMovementId;
    ui.receiptsInfo.textContent = getMovementInfoText(movement);
    initReceiptsFilters(movement);
    updateReceiptFilterState();
    hideReceiptsAlert();
    receiptsModal.show();
    await reloadReceiptDetails();
}

async function handleReloadReceiptCandidatesClick(): Promise<void> {
    try {
        hideReceiptsAlert();
        await reloadReceiptDetails();
    }
    catch (error) {
        showReceiptsAlert(getErrorMessage(error));
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
    if (!state.selectedReceiptsMoneyMovementId || !receiptId) {
        return;
    }

    const moneyMovementId = state.selectedReceiptsMoneyMovementId;
    const revision = receiptsRevision;
    const matchesList = movementRequests.capture();
    const isCurrent = () => revision === receiptsRevision && state.selectedReceiptsMoneyMovementId === moneyMovementId;
    const key = moneyMovementId + ":" + receiptId;
    if (commands.has(key) || button.disabled) { return; }
    commands.add(key);
    try {
    await runReceiptButtonAction(button, async () => {
        await unlinkMoneyMovementReceipt(forgeryToken, {
            moneyMovementId: moneyMovementId,
            receiptId: receiptId
        });
        if (!isCurrent()) { return; }
        await reloadReceiptDetails();
        if (isCurrent() && matchesList()) { await reloadMovements(); }
    }, isCurrent);
    } finally { commands.delete(key); }
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
    if (!state.selectedReceiptsMoneyMovementId || !receiptId) {
        return;
    }

    const moneyMovementId = state.selectedReceiptsMoneyMovementId;
    const revision = receiptsRevision;
    const matchesList = movementRequests.capture();
    const isCurrent = () => revision === receiptsRevision && state.selectedReceiptsMoneyMovementId === moneyMovementId;
    const key = moneyMovementId + ":" + receiptId;
    if (commands.has(key) || button.disabled) { return; }
    commands.add(key);
    try {
    await runReceiptButtonAction(button, async () => {
        await linkMoneyMovementReceipt(forgeryToken, {
            moneyMovementId: moneyMovementId,
            receiptId: receiptId
        });
        if (!isCurrent()) { return; }
        await reloadReceiptDetails();
        if (isCurrent() && matchesList()) { await reloadMovements(); }
    }, isCurrent);
    } finally { commands.delete(key); }
}

async function handleOpenReceiptDetailsClick(button: HTMLButtonElement): Promise<void> {
    const receiptId = button.getAttribute("data-receipt-id");
    if (!receiptId) {
        return;
    }

    const ticket = detailRequests.begin(receiptId);
    const revision = receiptsRevision;
    const isCurrent = () => ticket.isCurrent() && revision === receiptsRevision;
    await runReceiptButtonAction(button, async () => {
        const receipt = await openReceipt(forgeryToken, ticket.parameters, ticket.signal);
        if (!isCurrent()) { return; }
        renderReceiptDetailsModal(
            receipt,
            {
                detailsList: ui.receiptDetailsList,
                modalHeader: ui.receiptDetailsHeader,
                modalTotal: ui.receiptDetailsTotal,
                bootstrapModal: receiptDetailsModal
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
        if (isCurrent() && (write || !isIntentionalAbort(error))) { showReceiptsAlert(write ? getWriteErrorMessage(error) : getErrorMessage(error)); }
    }
    finally {
        if (!isCurrent()) { return; }
        button.disabled = false;
        button.textContent = originalText;
    }
}

async function reloadReceiptDetails(): Promise<void> {
    if (!state.selectedReceiptsMoneyMovementId) {
        return;
    }

    await Promise.all([
        reloadLinkedReceipts(),
        reloadReceiptCandidates()
    ]);
}

async function reloadLinkedReceipts(): Promise<void> {
    if (!state.selectedReceiptsMoneyMovementId) {
        return;
    }

    const selectedId = state.selectedReceiptsMoneyMovementId;
    const ticket = linkedRequests.begin({ selectedId, revision: receiptsRevision });
    try {
        const receipts = await loadLinkedReceipts(forgeryToken, ticket.parameters.selectedId, ticket.signal);
        if (!ticket.isCurrent() || ticket.parameters.revision !== receiptsRevision || selectedId !== state.selectedReceiptsMoneyMovementId) {
            return;
        }
        state.linkedReceipts = receipts;
        renderLinkedReceipts(ui.linkedReceipts, state.linkedReceipts);
    } catch (error) {
        if (ticket.isCurrent() && ticket.parameters.revision === receiptsRevision && !isIntentionalAbort(error)) { showReceiptsAlert(getErrorMessage(error)); }
    }
}

function invalidateCandidates(): void {
    candidateRequests.invalidate();
    candidateNavigation.clear();
    state.receiptCandidates = [];
    renderReceiptCandidates(ui.receiptCandidates, []);
}

async function reloadReceiptCandidates(): Promise<void> {
    if (!state.selectedReceiptsMoneyMovementId) {
        return;
    }

    const request = readReceiptCandidatesRequest(state.selectedReceiptsMoneyMovementId);
    const key = JSON.stringify(request);
    if (key !== candidateFilterKey) {
        candidatePage = 1;
        candidateFilterKey = key;
    }
    const selectedId = state.selectedReceiptsMoneyMovementId;
    const ticket = candidateRequests.begin({ ...request, page: candidatePage, pageSize: 20 });
    const revision = receiptsRevision;
    candidateNavigation.clear();
    try {
        const response = await loadReceiptCandidates(forgeryToken, ticket.parameters, ticket.signal);
        if (selectedId !== state.selectedReceiptsMoneyMovementId || !ticket.isCurrent() || revision !== receiptsRevision
            || key !== JSON.stringify(readReceiptCandidatesRequest(selectedId))) {
            return;
        }
        candidatePage = response.page;
        state.receiptCandidates = response.items;
        candidateNavigation.render(response);
        renderReceiptCandidates(ui.receiptCandidates, state.receiptCandidates);
    } catch (error) {
        if (ticket.isCurrent() && revision === receiptsRevision
            && key === JSON.stringify(readReceiptCandidatesRequest(selectedId)) && !isIntentionalAbort(error)) { showReceiptsAlert(getErrorMessage(error)); }
    }
}

function readReceiptCandidatesRequest(moneyMovementId: string): GetMoneyMovementReceiptCandidatesRequest {
    const amountTolerance = Number(ui.receiptsAmountToleranceInput.value);
    const timeWindowHours = Number(ui.receiptsTimeWindowHoursInput.value);

    return {
        moneyMovementId: moneyMovementId,
        useTimeWindow: ui.receiptsUseTimeWindowInput.checked,
        timeWindowHours: Number.isFinite(timeWindowHours) ? timeWindowHours : null,
        dateFrom: ui.receiptsDateFromInput.value || null,
        dateTo: ui.receiptsDateToInput.value || null,
        useAmountFilter: ui.receiptsUseAmountFilterInput.checked,
        amountTolerance: Number.isFinite(amountTolerance) ? amountTolerance : null,
        excludeLinkedReceipts: ui.receiptsExcludeLinkedInput.checked
    };
}

function initReceiptsFilters(movement: MoneyMovementDto): void {
    const occurredAt = new Date(movement.occurredAt);
    const date = Number.isNaN(occurredAt.getTime()) ? new Date() : occurredAt;
    const dateFrom = new Date(date);
    const dateTo = new Date(date);
    dateFrom.setDate(dateFrom.getDate() - 1);
    dateTo.setDate(dateTo.getDate() + 1);

    ui.receiptsDateFromInput.value = formatDateInput(dateFrom);
    ui.receiptsDateToInput.value = formatDateInput(dateTo);
    ui.receiptsUseTimeWindowInput.checked = false;
    ui.receiptsUseAmountFilterInput.checked = true;
    ui.receiptsExcludeLinkedInput.checked = true;
    ui.receiptsAmountToleranceInput.value = "1";
    ui.receiptsTimeWindowHoursInput.value = "1";
}

function updateReceiptFilterState(): void {
    const useTimeWindow = ui.receiptsUseTimeWindowInput.checked;
    ui.receiptsDateFromInput.disabled = useTimeWindow;
    ui.receiptsDateToInput.disabled = useTimeWindow;
    ui.receiptsTimeWindowHoursInput.disabled = !useTimeWindow;
    ui.receiptsAmountToleranceInput.disabled = !ui.receiptsUseAmountFilterInput.checked;
}

function startCommentEdit(moneyMovementId: string | null): void {
    if (!moneyMovementId || !findMovement(moneyMovementId)) {
        return;
    }

    state.editingCommentMovementId = moneyMovementId;
    renderMovements();
    const input = ui.list.querySelector<HTMLTextAreaElement>(`[data-action="edit-comment-input"][data-money-movement-id="${moneyMovementId}"]`);
    input?.focus();
}

function cancelCommentEdit(): void {
    state.editingCommentMovementId = null;
    renderMovements();
}

async function handleSaveCommentClick(button: HTMLButtonElement): Promise<void> {
    if (button.disabled) { return; }
    const moneyMovementId = button.getAttribute("data-money-movement-id");
    if (!moneyMovementId) {
        return;
    }

    const movement = findMovement(moneyMovementId);
    if (!movement) {
        showAlertMessage(ui.alert, "Операция не найдена в текущем списке.");
        return;
    }

    const input = ui.list.querySelector<HTMLTextAreaElement>(`[data-action="edit-comment-input"][data-money-movement-id="${moneyMovementId}"]`);
    if (!input) {
        showAlertMessage(ui.alert, "Поле комментария не найдено.");
        return;
    }

    if (commands.has(moneyMovementId)) { return; }
    commands.add(moneyMovementId);
    const matchesList = movementRequests.capture();
    const originalValue = input.value;
    const matchesEditor = () => matchesList() && state.editingCommentMovementId === moneyMovementId
        && ui.list.querySelector(`[data-action="edit-comment-input"][data-money-movement-id="${moneyMovementId}"]`) === input;
    const isCurrent = () => matchesEditor() && input.value === originalValue;
    const comment = input.value.trim() || null;
    const originalText = button.textContent;
    button.disabled = true;
    button.textContent = "...";

    try {
        hideAlertMessage(ui.alert);
        await updateMoneyMovementComment(forgeryToken, {
            moneyMovementId: movement.id,
            accountId: movement.accountId,
            comment: comment
        });

        if (!isCurrent()) { return; }
        movement.comment = comment;
        state.editingCommentMovementId = null;
        renderMovements();
    }
    catch (error) {
        if (matchesEditor()) { showAlertMessage(ui.alert, getWriteErrorMessage(error)); }
    }
    finally {
        commands.delete(moneyMovementId);
        if (!matchesEditor()) { return; }
        button.disabled = false;
        button.textContent = originalText;
    }
}

function openMoveAccountModal(moneyMovementId: string, sourceAccountId: string): void {
    const movement = findMovement(moneyMovementId);
    if (!movement) {
        showAlertMessage(ui.alert, "Операция не найдена в текущем списке.");
        return;
    }

    const sourceAccount = sourceAccountId ? findAccount(sourceAccountId) : null;
    if (!sourceAccount || !canEditAccount(sourceAccount)) {
        if (!sourceAccountId) {
            state.pendingAccountAction = {
                moneyMovementId: moneyMovementId,
                sourceAccountId: sourceAccountId
            };

            ui.moveSourceAccount.textContent = "Без счёта";
            ui.moveSourceAccount.style.border = "2px solid #dee2e6";
            hideMoveAccountAlert();
            renderMoveAccountOptions(sourceAccountId);
            updateMoveActionState();
            moveAccountModal.show();
            return;
        }

        showAlertMessage(ui.alert, "Недостаточно прав для изменения операции в выбранном счёте.");
        return;
    }

    state.pendingAccountAction = {
        moneyMovementId: moneyMovementId,
        sourceAccountId: sourceAccountId
    };

    ui.confirmMoveAccountButton.textContent = "Перенести";
    ui.moveSourceAccount.textContent = movement.accountName || "Счёт";
    ui.moveSourceAccount.style.border = "2px solid " + movement.accountColorHex;
    hideMoveAccountAlert();
    renderMoveAccountOptions(sourceAccountId);
    updateMoveActionState();
    moveAccountModal.show();
}

function renderMoveAccountOptions(sourceAccountId: string): void {
    ui.moveTargetAccountSelect.replaceChildren();

    for (const account of state.accounts) {
        if (account.id === sourceAccountId) {
            continue;
        }

        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;

        if (!canEditAccount(account)) {
            option.disabled = true;
            option.setAttribute("data-reason", "Недостаточно прав для изменения операций в этом счёте.");
        }

        ui.moveTargetAccountSelect.append(option);
    }

    const firstEnabledOption = Array.from(ui.moveTargetAccountSelect.options).find(option => option.value && !option.disabled);
    ui.moveTargetAccountSelect.value = firstEnabledOption ? firstEnabledOption.value : "";
    ui.moveTargetAccountSelect.disabled = ui.moveTargetAccountSelect.options.length === 0;
}

function updateMoveActionState(): void {
    const selectedOption = getSelectedMoveTargetOption();
    const canMove = !!selectedOption && !selectedOption.disabled;

    ui.confirmMoveAccountButton.disabled = !canMove;
    ui.removeFromAccountButton.disabled = state.pendingAccountAction === null || !state.pendingAccountAction.sourceAccountId;

    if (!selectedOption) {
        showMoveAccountAlert(getUnavailableMoveReason());
        return;
    }

    const reason = selectedOption.getAttribute("data-reason") || "";
    if (selectedOption.disabled && reason) {
        showMoveAccountAlert(reason);
        return;
    }

    hideMoveAccountAlert();
}

function getSelectedMoveTargetOption(): HTMLOptionElement | null {
    const selectedIndex = ui.moveTargetAccountSelect.selectedIndex;
    if (selectedIndex < 0) {
        return null;
    }

    const option = ui.moveTargetAccountSelect.options[selectedIndex];
    return option && option.value ? option : null;
}

function getUnavailableMoveReason(): string {
    if (ui.moveTargetAccountSelect.options.length === 0) {
        return "Нет других доступных счетов для переноса операции.";
    }

    const disabledOption = Array.from(ui.moveTargetAccountSelect.options).find(option => option.disabled && option.getAttribute("data-reason"));
    return disabledOption?.getAttribute("data-reason") || "Нет доступных счетов для переноса операции.";
}

async function handleConfirmMoveAccountClick(): Promise<void> {
    const action = state.pendingAccountAction;
    if (!action || commands.has(action.moneyMovementId)) { return; }
    const selectedOption = getSelectedMoveTargetOption();
    if (!selectedOption || selectedOption.disabled) { updateMoveActionState(); return; }
    const targetAccountId = selectedOption.value;
    const matchesList = movementRequests.capture();
    const isCurrent = () => state.pendingAccountAction === action && matchesList();
    const originalText = ui.confirmMoveAccountButton.textContent;
    ui.confirmMoveAccountButton.disabled = true;
    ui.confirmMoveAccountButton.textContent = "...";
    commands.add(action.moneyMovementId);
    try {
        await moveMoneyMovementToAccount(forgeryToken, { moneyMovementId: action.moneyMovementId, sourceAccountId: action.sourceAccountId, targetAccountId });
        if (!isCurrent()) { return; }
        state.pendingAccountAction = null;
        moveAccountModal.hide();
        await reloadMovements();
    } catch (error) {
        if (isCurrent()) { showMoveAccountAlert(getWriteErrorMessage(error)); }
    } finally {
        commands.delete(action.moneyMovementId);
        if (state.pendingAccountAction === action) {
            ui.confirmMoveAccountButton.textContent = originalText;
            updateMoveActionState();
        }
    }
}

function handleRemoveFromAccountClick(): void {
    if (!state.pendingAccountAction) {
        return;
    }

    const movement = findMovement(state.pendingAccountAction.moneyMovementId);
    ui.deleteInfo.textContent = movement ? getMovementInfoText(movement) : "";
    state.preserveMoveModalPendingOnHide = true;
    moveAccountModal.hide();
    ui.confirmDeleteButton.textContent = "Удалить";
    ui.confirmDeleteButton.disabled = false;
    deleteModal.show();
}

async function handleConfirmDeleteClick(): Promise<void> {
    const action = state.pendingAccountAction;
    if (!action || commands.has(action.moneyMovementId)) { return; }
    const matchesList = movementRequests.capture();
    const isCurrent = () => state.pendingAccountAction === action && matchesList();
    const originalText = ui.confirmDeleteButton.textContent;
    ui.confirmDeleteButton.disabled = true;
    ui.confirmDeleteButton.textContent = "...";
    commands.add(action.moneyMovementId);
    try {
        await deleteMoneyMovement(forgeryToken, { moneyMovementId: action.moneyMovementId, accountId: action.sourceAccountId });
        if (!isCurrent()) { return; }
        state.pendingAccountAction = null;
        deleteModal.hide();
        await reloadMovements();
    } catch (error) {
        if (isCurrent()) { showAlertMessage(ui.alert, getWriteErrorMessage(error)); }
    } finally {
        commands.delete(action.moneyMovementId);
        if (state.pendingAccountAction === action) {
            ui.confirmDeleteButton.textContent = originalText;
            ui.confirmDeleteButton.disabled = false;
        }
    }
}

function resetMoveModal(): void {
    if (state.preserveMoveModalPendingOnHide) {
        state.preserveMoveModalPendingOnHide = false;
    } else {
        state.pendingAccountAction = null;
    }

    ui.moveTargetAccountSelect.replaceChildren();
    ui.moveTargetAccountSelect.disabled = false;
    ui.moveSourceAccount.textContent = "";
    ui.moveSourceAccount.style.border = "";
    hideMoveAccountAlert();
}

function resetDeleteModal(): void {
    ui.deleteInfo.textContent = "";
    ui.confirmDeleteButton.disabled = false;
}

function findMovement(moneyMovementId: string): MoneyMovementDto | undefined {
    return state.movements.find(movement => movement.id === moneyMovementId);
}

function findAccount(accountId: string): UserAccountViewModel | undefined {
    return state.accounts.find(account => account.id === accountId);
}

function canEditAccount(account: UserAccountViewModel): boolean {
    return account.canManage === true || account.accessRole === 1 || account.accessRole === 2;
}

function getMovementInfoText(movement: MoneyMovementDto): string {
    const sign = movement.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "-" : "+";
    return `${movement.comment || "Без комментария"} · ${movement.accountName || "Без счёта"} · ${sign}${movement.amount}`;
}

function showMoveAccountAlert(message: string): void {
    ui.moveAccountAlert.textContent = message;
    ui.moveAccountAlert.classList.toggle("d-none", !message);
}

function hideMoveAccountAlert(): void {
    showMoveAccountAlert("");
}

function showImportAlert(message: string): void {
    ui.importAlert.textContent = message;
    ui.importAlert.classList.toggle("d-none", !message);
}

function hideImportAlert(): void {
    showImportAlert("");
}

function showReceiptsAlert(message: string): void {
    ui.receiptsAlert.textContent = message;
    ui.receiptsAlert.classList.toggle("d-none", !message);
}

function hideReceiptsAlert(): void {
    showReceiptsAlert("");
}

function readCreateRequest(): CreateMoneyMovementRequest {
    const amount = Number(ui.amountInput.value);

    if (!Number.isFinite(amount) || amount === 0) {
        throw new Error("Введите сумму операции.");
    }

    if (!ui.accountSelect.value) {
        throw new Error("Выберите счёт.");
    }

    updateMovementTypeByAmount();

    return {
        accountId: ui.accountSelect.value,
        amount: amount,
        type: readMovementType(),
        occurredAt: ui.occurredAtInput.value,
        comment: ui.commentInput.value.trim() || null
    };
}

function handleAmountInput(): void {
    updateMovementTypeByAmount();
}

function readMovementType(): MoneyMovementType | null {
    if (ui.typeSelect.value === "Expense") {
        return MONEY_MOVEMENT_TYPE_EXPENSE;
    }

    if (ui.typeSelect.value === "Income") {
        return MONEY_MOVEMENT_TYPE_INCOME;
    }

    return null;
}

function updateMovementTypeByAmount(): void {
    const amount = Number(ui.amountInput.value);
    const expenseOption = getTypeOption("Expense");
    const incomeOption = getTypeOption("Income");

    expenseOption.disabled = false;
    incomeOption.disabled = false;

    if (!Number.isFinite(amount) || amount === 0) {
        return;
    }

    if (amount < 0) {
        ui.typeSelect.value = "Expense";
        incomeOption.disabled = true;
        return;
    }

    ui.typeSelect.value = "Income";
    expenseOption.disabled = true;
}

function getTypeOption(value: string): HTMLOptionElement {
    const option = Array.from(ui.typeSelect.options).find(item => item.value === value);

    if (!option) {
        throw new Error(`Не найден тип операции: ${value}.`);
    }

    return option;
}

function initDefaultDates(): void {
    const now = new Date();
    ui.periodPresetSelect.value = "currentMonth";
    applySelectedPeriodPreset(now);
    ui.occurredAtInput.value = formatDateTimeLocalInput(now);
}

function handlePeriodPresetChange(): void {
    movementRequests.invalidate();
    applySelectedPeriodPreset(new Date());
}

function applySelectedPeriodPreset(now: Date): void {
    const range = getDateRangeByPeriodPreset(ui.periodPresetSelect.value || "currentMonth", now);
    ui.dateFromInput.value = formatDateForQuery(range.dateFrom);
    ui.dateToInput.value = formatDateForQuery(range.dateTo);
}

function handleSearchInput(): void {
    movementRequests.invalidate();
    state.searchQuery = ui.searchInput.value.trim();

    if (state.searchDebounceTimerId) {
        clearTimeout(state.searchDebounceTimerId);
    }

    state.searchDebounceTimerId = window.setTimeout(() => { void handleApplyFilterClick(); }, 250);
}

function formatDateInput(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function formatDateTimeLocalInput(date: Date): string {
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");
    return `${formatDateInput(date)}T${hours}:${minutes}`;
}

function getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : "Не удалось выполнить операцию.";
}
