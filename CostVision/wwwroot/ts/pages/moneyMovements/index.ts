import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { BootstrapModal, createBootstrapModal } from "../../shared/bootstrap.js";
import { initFileDropzone } from "../../shared/dropzone.js";
import { normalizeSingleLineTextValue } from "../../shared/formatters.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { createMoneyMovement, deleteMoneyMovement, importMoneyMovements, linkMoneyMovementReceipt, loadAccounts, loadImportBanks, loadLinkedReceipts, loadMoneyMovements, loadReceiptCandidates, moveMoneyMovementToAccount, previewBankStatementImport, unlinkMoneyMovementReceipt, updateMoneyMovementComment } from "./api.js";
import { formatDateForQuery, getDateRangeByPeriodPreset } from "./dateRange.js";
import { renderAccountOptions, renderImportBankOptions, renderImportErrors, renderImportPreview, renderImportSummary, renderLinkedReceipts, renderMoneyMovementList, renderReceiptCandidates, renderSummary } from "./render.js";
import { state } from "./state.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type BankStatementImportPreviewRowDto, type BankStatementImportRowRequest, type CreateMoneyMovementRequest, type GetMoneyMovementReceiptCandidatesRequest, type MoneyMovementDto, type MoneyMovementType, type PendingMoneyMovementAccountAction, type SaveBankStatementImportRequest, type UserAccountViewModel } from "./types.js";
import { getMoneyMovementsUi, type MoneyMovementsUi } from "./ui.js";

let ui: MoneyMovementsUi;
let forgeryToken: string | null;
let moveAccountModal: BootstrapModal;
let deleteModal: BootstrapModal;
let importModal: BootstrapModal;
let receiptsModal: BootstrapModal;
let pendingAccountAction: PendingMoneyMovementAccountAction | null = null;
let selectedReceiptsMoneyMovementId: string | null = null;
let preserveMoveModalPendingOnHide = false;
let selectedImportFile: File | null = null;
let editingCommentMovementId: string | null = null;
let searchDebounceTimerId: number | null = null;

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
    initFileDropzone({
        dropzone: ui.importDropzone,
        fileInput: ui.importFileInput,
        multiple: false,
        onFilesSelected: handleImportFilesSelected
    });
    ui.importPreviewButton.addEventListener("click", handleImportPreviewClick);
    ui.importSaveButton.addEventListener("click", handleImportSaveClick);
    ui.importToggleDuplicateReplacementsInput.addEventListener("change", handleToggleDuplicateReplacementsChange);
    ui.importSkipDuplicatesInput.addEventListener("change", handleSkipDuplicatesChange);
    ui.importToggleDuplicatesButton.addEventListener("click", handleToggleDuplicatesClick);
    ui.importPreview.addEventListener("input", handleImportPreviewInput);
    ui.importPreview.addEventListener("change", handleImportPreviewChange);
    ui.importPreview.addEventListener("click", handleImportPreviewContainerClick);
    ui.receiptsReloadCandidatesButton.addEventListener("click", handleReloadReceiptCandidatesClick);
    ui.receiptsUseTimeWindowInput.addEventListener("change", updateReceiptFilterState);
    ui.receiptsUseAmountFilterInput.addEventListener("change", updateReceiptFilterState);
    ui.linkedReceipts.addEventListener("click", handleLinkedReceiptsClick);
    ui.receiptCandidates.addEventListener("click", handleReceiptCandidatesClick);

    ui.moveAccountModal.addEventListener("hidden.bs.modal", resetMoveModal);
    ui.deleteModal.addEventListener("hidden.bs.modal", resetDeleteModal);
    ui.importModal.addEventListener("hidden.bs.modal", resetImportModal);
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

        renderImportState();
        await reloadMovements();
    }
    catch (error) {
        showAlertMessage(ui.alert, getErrorMessage(error));
    }
}

function handleImportFilesSelected(files: File[]): void {
    const file = files[0];
    if (!file) {
        return;
    }

    setImportFile(file);
}

function setImportFile(file: File): void {
    selectedImportFile = file;
    ui.importFileName.textContent = file.name;
    state.importRows = [];
    state.importErrors = [];
    renderImportState();
    hideImportAlert();
}

async function handleImportPreviewClick(): Promise<void> {
    try {
        hideImportAlert();

        if (!selectedImportFile) {
            throw new Error("Выберите файл выписки.");
        }

        if (!ui.importBankSelect.value) {
            throw new Error("Выберите банк.");
        }

        if (!ui.importAccountSelect.value) {
            throw new Error("Выберите счёт для импорта.");
        }

        ui.importPreviewButton.disabled = true;
        ui.importPreviewButton.textContent = "Распознавание...";

        const preview = await previewBankStatementImport(forgeryToken, ui.importBankSelect.value, ui.importAccountSelect.value, selectedImportFile);
        state.importRows = preview.rows.map(row => ({
            ...row,
            replaceDuplicate: false
        }));
        state.importErrors = preview.errors;
        renderImportState();
    }
    catch (error) {
        showImportAlert(getErrorMessage(error));
    }
    finally {
        ui.importPreviewButton.disabled = false;
        ui.importPreviewButton.textContent = "Предпросмотр";
    }
}

async function handleImportSaveClick(): Promise<void> {
    try {
        hideImportAlert();

        const unresolvedDuplicate = state.importRows.find(row => row.isDuplicate && row.replaceDuplicate !== true);
        if (unresolvedDuplicate) {
            throw new Error("Удалите повторяющиеся операции из импорта или отметьте замену существующих.");
        }

        const request = readImportRequest();
        ui.importSaveButton.disabled = true;
        ui.importSaveButton.textContent = "Импорт...";

        await importMoneyMovements(forgeryToken, request);
        importModal.hide();
        await reloadMovements();
    }
    catch (error) {
        showImportAlert(getErrorMessage(error));
        updateImportSaveState();
    }
    finally {
        ui.importSaveButton.textContent = "Импортировать";
    }
}

function handleImportPreviewInput(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLTextAreaElement)) {
        return;
    }

    if (target.getAttribute("data-action") !== "change-import-comment") {
        return;
    }

    const row = findImportRow(target.getAttribute("data-import-row-id"));
    if (!row) {
        return;
    }

    row.comment = target.value;
}

function handleImportPreviewChange(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
        return;
    }

    if (target.getAttribute("data-action") !== "toggle-import-replace") {
        return;
    }

    const row = findImportRow(target.getAttribute("data-import-row-id"));
    if (!row) {
        return;
    }

    row.replaceDuplicate = target.checked;
    updateImportSaveState();
}

function handleImportPreviewContainerClick(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const removeButton = target.closest<HTMLButtonElement>('[data-action="remove-import-row"]');
    if (!removeButton) {
        return;
    }

    const rowId = removeButton.getAttribute("data-import-row-id");
    state.importRows = state.importRows.filter(row => row.clientRowId !== rowId);
    renderImportState();
}

function handleToggleDuplicateReplacementsChange(): void {
    for (const row of state.importRows) {
        if (row.isDuplicate) {
            row.replaceDuplicate = ui.importToggleDuplicateReplacementsInput.checked;
        }
    }

    renderImportState();
}

function handleSkipDuplicatesChange(): void {
    if (ui.importSkipDuplicatesInput.checked) {
        for (const row of state.importRows) {
            if (row.isDuplicate) {
                row.replaceDuplicate = false;
            }
        }
    }

    renderImportState();
}

function handleToggleDuplicatesClick(): void {
    state.hideDuplicateImportRows = !state.hideDuplicateImportRows;
    renderImportState();
}

function readImportRequest(): SaveBankStatementImportRequest {
    if (!ui.importAccountSelect.value) {
        throw new Error("Выберите счёт для импорта.");
    }

    const importRows = getRowsForImport();
    if (importRows.length === 0) {
        throw new Error("Нет строк для импорта.");
    }

    const rows: BankStatementImportRowRequest[] = importRows.map(row => ({
        occurredAt: row.occurredAt,
        amount: row.amount,
        type: row.type,
        comment: row.comment.trim() || null,
        importComment: row.importComment,
        duplicateMoneyMovementId: row.duplicateMoneyMovementId || null,
        replaceDuplicate: row.replaceDuplicate === true
    }));

    return {
        accountId: ui.importAccountSelect.value,
        rows: rows
    };
}

function renderImportState(): void {
    const visibleRows = state.hideDuplicateImportRows
        ? state.importRows.filter(row => !row.isDuplicate)
        : state.importRows;

    renderImportPreview(ui.importPreview, visibleRows);
    renderImportErrors(ui.importErrors, state.importErrors);
    renderImportSummary(ui.importSummary, state.importRows);
    updateImportPreviewControls();
    updateImportSaveState();
}

function updateImportPreviewControls(): void {
    const duplicateRows = state.importRows.filter(row => row.isDuplicate);
    const hasDuplicates = duplicateRows.length > 0;
    const checkedDuplicateCount = duplicateRows.filter(row => row.replaceDuplicate === true).length;

    ui.importSkipDuplicatesInput.disabled = !hasDuplicates;
    ui.importToggleDuplicateReplacementsInput.disabled = !hasDuplicates || ui.importSkipDuplicatesInput.checked;
    ui.importToggleDuplicateReplacementsInput.checked = !ui.importSkipDuplicatesInput.checked && hasDuplicates && checkedDuplicateCount === duplicateRows.length;
    ui.importToggleDuplicateReplacementsInput.indeterminate = !ui.importSkipDuplicatesInput.checked && hasDuplicates && checkedDuplicateCount > 0 && checkedDuplicateCount < duplicateRows.length;
    ui.importToggleDuplicatesButton.disabled = !hasDuplicates;
    ui.importToggleDuplicatesButton.textContent = state.hideDuplicateImportRows ? "Показать повторяющиеся" : "Скрыть повторяющиеся";
}

function updateImportSaveState(): void {
    const rowsForImport = getRowsForImport();
    const hasRows = rowsForImport.length > 0;
    const hasUnresolvedDuplicates = !ui.importSkipDuplicatesInput.checked && rowsForImport.some(row => row.isDuplicate && row.replaceDuplicate !== true);
    ui.importSaveButton.disabled = !hasRows || hasUnresolvedDuplicates;
}

function getRowsForImport(): BankStatementImportPreviewRowDto[] {
    if (!ui.importSkipDuplicatesInput.checked) {
        return state.importRows;
    }

    return state.importRows.filter(row => !row.isDuplicate);
}

function findImportRow(rowId: string | null): BankStatementImportPreviewRowDto | undefined {
    if (!rowId) {
        return undefined;
    }

    return state.importRows.find(row => row.clientRowId === rowId);
}

function resetImportModal(): void {
    selectedImportFile = null;
    ui.importFileInput.value = "";
    ui.importSkipDuplicatesInput.checked = false;
    ui.importToggleDuplicateReplacementsInput.checked = false;
    ui.importToggleDuplicateReplacementsInput.indeterminate = false;
    ui.importFileName.textContent = "";
    state.importRows = [];
    state.importErrors = [];
    state.hideDuplicateImportRows = false;
    hideImportAlert();
    renderImportState();
}

function resetReceiptsModal(): void {
    selectedReceiptsMoneyMovementId = null;
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
    editingCommentMovementId = null;
    renderMovements();
}

function renderMovements(): void {
    const filteredMovements = applySearchFilter(state.movements);
    renderMoneyMovementList(ui.list, filteredMovements, state.accounts, editingCommentMovementId);
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

    selectedReceiptsMoneyMovementId = moneyMovementId;
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
        await reloadReceiptCandidates();
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

    const button = target.closest<HTMLButtonElement>('[data-action="unlink-receipt"]');
    if (!button) {
        return;
    }

    const receiptId = button.getAttribute("data-receipt-id");
    if (!selectedReceiptsMoneyMovementId || !receiptId) {
        return;
    }

    await runReceiptButtonAction(button, async () => {
        await unlinkMoneyMovementReceipt(forgeryToken, {
            moneyMovementId: selectedReceiptsMoneyMovementId!,
            receiptId: receiptId
        });
        await reloadReceiptDetails();
    });
}

async function handleReceiptCandidatesClick(event: MouseEvent): Promise<void> {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const button = target.closest<HTMLButtonElement>('[data-action="link-receipt"]');
    if (!button) {
        return;
    }

    const receiptId = button.getAttribute("data-receipt-id");
    if (!selectedReceiptsMoneyMovementId || !receiptId) {
        return;
    }

    await runReceiptButtonAction(button, async () => {
        await linkMoneyMovementReceipt(forgeryToken, {
            moneyMovementId: selectedReceiptsMoneyMovementId!,
            receiptId: receiptId
        });
        await reloadReceiptDetails();
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
    if (!selectedReceiptsMoneyMovementId) {
        return;
    }

    await Promise.all([
        reloadLinkedReceipts(),
        reloadReceiptCandidates()
    ]);
}

async function reloadLinkedReceipts(): Promise<void> {
    if (!selectedReceiptsMoneyMovementId) {
        return;
    }

    state.linkedReceipts = await loadLinkedReceipts(forgeryToken, selectedReceiptsMoneyMovementId);
    renderLinkedReceipts(ui.linkedReceipts, state.linkedReceipts);
}

async function reloadReceiptCandidates(): Promise<void> {
    if (!selectedReceiptsMoneyMovementId) {
        return;
    }

    state.receiptCandidates = await loadReceiptCandidates(forgeryToken, readReceiptCandidatesRequest(selectedReceiptsMoneyMovementId));
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

    editingCommentMovementId = moneyMovementId;
    renderMovements();
    const input = ui.list.querySelector<HTMLTextAreaElement>(`[data-action="edit-comment-input"][data-money-movement-id="${moneyMovementId}"]`);
    input?.focus();
}

function cancelCommentEdit(): void {
    editingCommentMovementId = null;
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
        editingCommentMovementId = null;
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

    pendingAccountAction = {
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
    ui.removeFromAccountButton.disabled = pendingAccountAction === null;

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
    if (!pendingAccountAction) {
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
            moneyMovementId: pendingAccountAction.moneyMovementId,
            sourceAccountId: pendingAccountAction.sourceAccountId,
            targetAccountId: selectedOption.value
        });

        pendingAccountAction = null;
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
    if (!pendingAccountAction) {
        return;
    }

    const movement = findMovement(pendingAccountAction.moneyMovementId);
    ui.deleteInfo.textContent = movement ? getMovementInfoText(movement) : "";
    preserveMoveModalPendingOnHide = true;
    moveAccountModal.hide();
    deleteModal.show();
}

async function handleConfirmDeleteClick(): Promise<void> {
    if (!pendingAccountAction) {
        return;
    }

    const originalText = ui.confirmDeleteButton.textContent;
    ui.confirmDeleteButton.disabled = true;
    ui.confirmDeleteButton.textContent = "Удаление...";

    try {
        await deleteMoneyMovement(forgeryToken, {
            moneyMovementId: pendingAccountAction.moneyMovementId,
            accountId: pendingAccountAction.sourceAccountId
        });

        deleteModal.hide();
        pendingAccountAction = null;
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
    if (preserveMoveModalPendingOnHide) {
        preserveMoveModalPendingOnHide = false;
    } else {
        pendingAccountAction = null;
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

    if (searchDebounceTimerId) {
        clearTimeout(searchDebounceTimerId);
    }

    searchDebounceTimerId = window.setTimeout(renderMovements, 250);
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
