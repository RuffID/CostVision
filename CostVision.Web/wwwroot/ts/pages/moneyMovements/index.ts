import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { BootstrapModal, createBootstrapModal } from "../../shared/bootstrap.js";
import { formatMoneyRub, formatRuNumber, normalizeSingleLineTextValue } from "../../shared/formatters.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { createMoneyMovement, deleteMoneyMovement, linkMoneyMovementReceipt, loadAccounts, loadImportBanks, loadLinkedReceipts, loadMoneyMovements, loadReceiptCandidates, moveMoneyMovementToAccount, openReceipt, unlinkMoneyMovementReceipt, updateMoneyMovementComment } from "./api.js";
import { formatDateForQuery, getDateRangeByPeriodPreset } from "./dateRange.js";
import { initMoneyMovementHelpTooltips } from "./helpTooltips.js";
import { initMoneyMovementImportModalController, type MoneyMovementImportModalController } from "./importModal.js";
import { renderAccountOptions, renderImportBankOptions, renderLinkedReceipts, renderMoneyMovementList, renderReceiptCandidates, renderSummary } from "./render.js";
import { state } from "./state.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type CreateMoneyMovementRequest, type GetMoneyMovementReceiptCandidatesRequest, type MoneyMovementDto, type MoneyMovementType, type UserAccountViewModel } from "./types.js";
import { getMoneyMovementsUi, type MoneyMovementsUi } from "./ui.js";
import { renderReceiptDetails as renderReceiptDetailsModal } from "../reports/receipts/ui/receiptDetailsModal.js";

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
    ui.receiptFilterSelect.addEventListener("change", renderMovements);
    ui.list.addEventListener("click", handleMoneyMovementListClick);
    ui.moveTargetAccountSelect.addEventListener("change", updateMoveActionState);
    ui.confirmMoveAccountButton.addEventListener("click", handleConfirmMoveAccountClick);
    ui.removeFromAccountButton.addEventListener("click", handleRemoveFromAccountClick);
    ui.confirmDeleteButton.addEventListener("click", handleConfirmDeleteClick);
    ui.receiptsReloadCandidatesButton.addEventListener("click", handleReloadReceiptCandidatesClick);
    ui.receiptsUseTimeWindowInput.addEventListener("change", updateReceiptFilterState);
    ui.receiptsUseAmountFilterInput.addEventListener("change", updateReceiptFilterState);
    ui.linkedReceipts.addEventListener("click", handleLinkedReceiptsClick);
    ui.receiptCandidates.addEventListener("click", handleReceiptCandidatesClick);

    ui.moveAccountModal.addEventListener("hidden.bs.modal", resetMoveModal);
    ui.deleteModal.addEventListener("hidden.bs.modal", resetDeleteModal);
    ui.receiptsModal.addEventListener("hidden.bs.modal", resetReceiptsModal);
}

async function loadInitialData(): Promise<void> {
    try {
        hideAlertMessage(ui.alert);
        state.accounts = await loadAccounts(forgeryToken);
        state.importBanks = await loadImportBanks(forgeryToken);
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
        showAlertMessage(ui.alert, getErrorMessage(error));
    }
}

function resetReceiptsModal(): void {
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

    try {
        hideAlertMessage(ui.alert);
        ui.createButton.disabled = true;
        const request = readCreateRequest();
        await createMoneyMovement(forgeryToken, request);
        ui.amountInput.value = "";
        ui.commentInput.value = "";
        updateMovementTypeByAmount();
        await reloadMovements();
    }
    catch (error) {
        showAlertMessage(ui.alert, getErrorMessage(error));
    }
    finally {
        ui.createButton.disabled = false;
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
    state.movements = await loadMoneyMovements(forgeryToken, state.dateFrom, state.dateTo, state.selectedAccountId);
    state.editingCommentMovementId = null;
    renderMovements();
}

function renderMovements(): void {
    const filteredMovements = applySearchFilter(state.movements);
    renderMoneyMovementList(ui.list, filteredMovements, state.accounts, state.editingCommentMovementId);
    renderSummary(ui.count, ui.incomeSum, ui.expenseSum, filteredMovements);
}

function applySearchFilter(movements: MoneyMovementDto[]): MoneyMovementDto[] {
    const query = normalizeSearchText(state.searchQuery).toLowerCase();
    let result = movements;

    if (ui.receiptFilterSelect.value === "withoutReceipts") {
        result = result.filter(movement => movement.linkedReceiptCount === 0);
    } else if (ui.receiptFilterSelect.value === "withReceipts") {
        result = result.filter(movement => movement.linkedReceiptCount > 0);
    } else if (ui.receiptFilterSelect.value === "amountMismatch") {
        result = result.filter(movement => movement.linkedReceiptCount > 0 && Math.abs(movement.amount - movement.linkedReceiptsTotalSum) >= 0.01);
    }

    if (!query) {
        return result;
    }

    return result.filter(movement => {
        const comment = normalizeSearchText(movement.comment).toLowerCase();
        const importComment = normalizeSearchText(movement.importComment).toLowerCase();
        return comment.includes(query) || importComment.includes(query);
    });
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
    if (!moneyMovementId || !accountId) {
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

    await runReceiptButtonAction(button, async () => {
        await unlinkMoneyMovementReceipt(forgeryToken, {
            moneyMovementId: state.selectedReceiptsMoneyMovementId!,
            receiptId: receiptId
        });
        await reloadReceiptDetails();
        await reloadMovements();
    });
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

    await runReceiptButtonAction(button, async () => {
        await linkMoneyMovementReceipt(forgeryToken, {
            moneyMovementId: state.selectedReceiptsMoneyMovementId!,
            receiptId: receiptId
        });
        await reloadReceiptDetails();
        await reloadMovements();
    });
}

async function handleOpenReceiptDetailsClick(button: HTMLButtonElement): Promise<void> {
    const receiptId = button.getAttribute("data-receipt-id");
    if (!receiptId) {
        return;
    }

    await runReceiptButtonAction(button, async () => {
        const receipt = await openReceipt(forgeryToken, receiptId);
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
    });
}

async function runReceiptButtonAction(button: HTMLButtonElement, action: () => Promise<void>): Promise<void> {
    const originalText = button.textContent;
    button.disabled = true;
    button.textContent = "...";

    try {
        hideReceiptsAlert();
        await action();
    }
    catch (error) {
        showReceiptsAlert(getErrorMessage(error));
    }
    finally {
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

    state.linkedReceipts = await loadLinkedReceipts(forgeryToken, state.selectedReceiptsMoneyMovementId);
    renderLinkedReceipts(ui.linkedReceipts, state.linkedReceipts);
}

async function reloadReceiptCandidates(): Promise<void> {
    if (!state.selectedReceiptsMoneyMovementId) {
        return;
    }

    state.receiptCandidates = await loadReceiptCandidates(forgeryToken, readReceiptCandidatesRequest(state.selectedReceiptsMoneyMovementId));
    renderReceiptCandidates(ui.receiptCandidates, state.receiptCandidates);
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

        movement.comment = comment;
        state.editingCommentMovementId = null;
        renderMovements();
    }
    finally {
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

    const sourceAccount = findAccount(sourceAccountId);
    if (!sourceAccount || !canEditAccount(sourceAccount)) {
        showAlertMessage(ui.alert, "Недостаточно прав для изменения операции в выбранном счёте.");
        return;
    }

    state.pendingAccountAction = {
        moneyMovementId: moneyMovementId,
        sourceAccountId: sourceAccountId
    };

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
    ui.removeFromAccountButton.disabled = state.pendingAccountAction === null;

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
    if (!state.pendingAccountAction) {
        return;
    }

    const selectedOption = getSelectedMoveTargetOption();
    if (!selectedOption || selectedOption.disabled) {
        updateMoveActionState();
        return;
    }

    const originalText = ui.confirmMoveAccountButton.textContent;
    ui.confirmMoveAccountButton.disabled = true;
    ui.confirmMoveAccountButton.textContent = "Перенос...";

    try {
        await moveMoneyMovementToAccount(forgeryToken, {
            moneyMovementId: state.pendingAccountAction.moneyMovementId,
            sourceAccountId: state.pendingAccountAction.sourceAccountId,
            targetAccountId: selectedOption.value
        });

        state.pendingAccountAction = null;
        moveAccountModal.hide();
        await reloadMovements();
    }
    catch (error) {
        showMoveAccountAlert(getErrorMessage(error));
    }
    finally {
        ui.confirmMoveAccountButton.textContent = originalText;
        updateMoveActionState();
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
    deleteModal.show();
}

async function handleConfirmDeleteClick(): Promise<void> {
    if (!state.pendingAccountAction) {
        return;
    }

    const originalText = ui.confirmDeleteButton.textContent;
    ui.confirmDeleteButton.disabled = true;
    ui.confirmDeleteButton.textContent = "Удаление...";

    try {
        await deleteMoneyMovement(forgeryToken, {
            moneyMovementId: state.pendingAccountAction.moneyMovementId,
            accountId: state.pendingAccountAction.sourceAccountId
        });

        deleteModal.hide();
        state.pendingAccountAction = null;
        await reloadMovements();
    }
    catch (error) {
        showAlertMessage(ui.alert, getErrorMessage(error));
    }
    finally {
        ui.confirmDeleteButton.disabled = false;
        ui.confirmDeleteButton.textContent = originalText;
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
    return `${movement.comment || "Без комментария"} · ${movement.accountName || "Счёт"} · ${sign}${movement.amount}`;
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
    applySelectedPeriodPreset(new Date());
}

function applySelectedPeriodPreset(now: Date): void {
    const range = getDateRangeByPeriodPreset(ui.periodPresetSelect.value || "currentMonth", now);
    ui.dateFromInput.value = formatDateForQuery(range.dateFrom);
    ui.dateToInput.value = formatDateForQuery(range.dateTo);
}

function handleSearchInput(): void {
    state.searchQuery = ui.searchInput.value.trim();

    if (state.searchDebounceTimerId) {
        clearTimeout(state.searchDebounceTimerId);
    }

    state.searchDebounceTimerId = window.setTimeout(renderMovements, 250);
}

function normalizeSearchText(value: unknown): string {
    return normalizeSingleLineTextValue(value);
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
