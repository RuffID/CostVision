import { createRequestCoordinator, getWriteErrorMessage, isIntentionalAbort, isUnknownWriteOutcome } from "../../shared/requestCoordinator.js";
import type { BootstrapModal } from "../../shared/bootstrap.js";
import { initFileDropzone } from "../../shared/dropzone.js";
import { importMoneyMovements, previewBankStatementImport } from "./api.js";
import { renderImportErrors, renderImportPreview, renderImportSummary } from "./render.js";
import type { MoneyMovementsState } from "./state.js";
import type { BankStatementImportLineErrorDto, BankStatementImportPreviewRowDto, BankStatementImportResultDto, BankStatementImportRowRequest, SaveBankStatementImportRequest } from "./types.js";
import type { MoneyMovementsUi } from "./ui.js";

export interface MoneyMovementImportModalController {
    renderState(): void;
}

export interface MoneyMovementImportModalOptions {
    ui: MoneyMovementsUi;
    state: MoneyMovementsState;
    importModal: BootstrapModal;
    getForgeryToken: () => string | null;
    reloadMovements: () => Promise<void>;
}

export function initMoneyMovementImportModalController(options: MoneyMovementImportModalOptions): MoneyMovementImportModalController {
    const { ui, state, getForgeryToken, reloadMovements } = options;
    let saving = false;
    let pageActive = true;
    let previewing = false;
    let previewOwner: symbol | null = null;
    let lastResult: BankStatementImportResultDto | null = null;
    const previewRequests = createRequestCoordinator();
    const modalContext = createRequestCoordinator();
    let previewIdentity: { file: File; bankId: string; accountId: string } | null = null;
    let unknownOutcome = false;
    window.addEventListener("pagehide", () => { pageActive = false; previewOwner = null; previewRequests.invalidate(); modalContext.invalidate(); });
    let recognitionErrors: BankStatementImportLineErrorDto[] = [];

    // Окно остаётся открытым, чтобы итог был виден и исправлялись только непринятые строки.
    ui.importModal.addEventListener("hide.bs.modal", event => {
        if (saving || previewing) {
            event.preventDefault();
        }
    });

    initFileDropzone({
        dropzone: ui.importDropzone,
        fileInput: ui.importFileInput,
        multiple: false,
        onFilesSelected: handleImportFilesSelected
    });

    ui.importPreviewButton.addEventListener("click", handleImportPreviewClick);
    ui.importBankSelect.addEventListener("change", invalidatePreview);
    ui.importAccountSelect.addEventListener("change", invalidatePreview);
    ui.importSaveButton.addEventListener("click", handleImportSaveClick);
    ui.importToggleDuplicateReplacementsInput.addEventListener("change", handleToggleDuplicateReplacementsChange);
    ui.importSkipDuplicatesInput.addEventListener("change", handleSkipDuplicatesChange);
    ui.importToggleDuplicatesButton.addEventListener("click", handleToggleDuplicatesClick);
    ui.importPreview.addEventListener("input", handleImportPreviewInput);
    ui.importPreview.addEventListener("change", handleImportPreviewChange);
    ui.importPreview.addEventListener("click", handleImportPreviewContainerClick);
    ui.importModal.addEventListener("hidden.bs.modal", resetImportModal);

    return {
        renderState
    };

    function handleImportFilesSelected(files: File[]): void {
        const file = files[0];
        if (!file) {
            return;
        }

        setImportFile(file);
    }

    function setImportFile(file: File): void {
        if (saving || previewing) {
            return;
        }

        state.selectedImportFile = file;
        ui.importFileName.textContent = file.name;
        state.importRows = [];
        state.importErrors = [];
        lastResult = null;
        previewIdentity = null;
        previewRequests.invalidate();
        modalContext.invalidate();
        recognitionErrors = [];
        renderState();
        hideImportAlert();
    }

    function invalidatePreview(): void {
        previewRequests.invalidate();
        previewIdentity = null;
        updateImportSaveState();
        showImportAlert("Реквизиты изменены. Выполните новый предпросмотр.");
    }

    async function handleImportPreviewClick(): Promise<void> {
        if (saving || previewing) {
            return;
        }

        const owner = Symbol();
        previewOwner = owner;
        const ticket = previewRequests.begin({ file: state.selectedImportFile,
            bankId: ui.importBankSelect.value, accountId: ui.importAccountSelect.value });
        try {
            hideImportAlert();

            if (!state.selectedImportFile) {
                throw new Error("Выберите файл выписки.");
            }

            if (!ui.importBankSelect.value) {
                throw new Error("Выберите банк.");
            }

            if (!ui.importAccountSelect.value) {
                throw new Error("Выберите счёт для импорта.");
            }

            previewing = true;
            updateImportBusyState();
            ui.importPreviewButton.textContent = "Распознавание...";

            const identity = ticket.parameters;
            const preview = await previewBankStatementImport(getForgeryToken(), identity.bankId, identity.accountId, identity.file!, ticket.signal);
            if (!ticket.isCurrent() || !matchesIdentity(identity)) { return; }
            if (preview.accountId !== identity.accountId || preview.bankId !== identity.bankId) {
                throw new Error("Предпросмотр не соответствует выбранному банку и счёту.");
            }
            previewIdentity = { file: identity.file!, bankId: identity.bankId, accountId: identity.accountId };
            unknownOutcome = false;
            lastResult = null;
            state.importRows = preview.rows.map(row => ({
                ...row,
                replaceDuplicate: false
            }));
            state.importErrors = preview.errors;
            recognitionErrors = preview.errors;
            renderState();
        }
        catch (error) {
            if (ticket.isCurrent() && matchesIdentity(ticket.parameters) && !isIntentionalAbort(error)) {
                showImportAlert(getErrorMessage(error));
            }
        }
        finally {
            if (previewOwner !== owner) { return; }
            previewOwner = null;
            previewing = false;
            updateImportBusyState();
            ui.importPreviewButton.textContent = "Предпросмотр";
        }
    }

    function matchesIdentity(identity: { file: File | null; bankId: string; accountId: string }): boolean {
        return identity.file === state.selectedImportFile && identity.bankId === ui.importBankSelect.value
            && identity.accountId === ui.importAccountSelect.value;
    }

    async function handleImportSaveClick(): Promise<void> {
        if (saving || previewing || ui.importSaveButton.disabled) {
            return;
        }

        const isCurrent = modalContext.capture();
        let writeConfirmed = false;
        try {
            hideImportAlert();

            const unresolvedDuplicate = getRowsForImport().find(row => row.isDuplicate && row.replaceDuplicate !== true);
            if (unresolvedDuplicate) {
                throw new Error("Удалите повторяющиеся операции из импорта или отметьте замену существующих.");
            }

            const request = readImportRequest();
            const submittedRows = getRowsForImport().map(row => ({ ...row }));
            saving = true;
            updateImportBusyState();
            ui.importSaveButton.textContent = "Импорт...";

            const result = await importMoneyMovements(getForgeryToken(), request);
            writeConfirmed = true;
            if (!isCurrent()) { return; }
            // Номера ошибок относятся к отправленному пакету, а не к исходному PDF.
            const rejected = new Set(result.errors.map(error => error.lineNumber - 1));
            const acceptedIds = new Set(submittedRows.filter((_, index) => !rejected.has(index)).map(row => row.clientRowId));
            state.importRows = state.importRows.filter(row => !acceptedIds.has(row.clientRowId));
            for (const error of result.errors) {
                if (error.canReplaceDuplicate === true) {
                    const failedRow = findImportRow(submittedRows[error.lineNumber - 1].clientRowId);
                    if (failedRow) {
                        failedRow.isDuplicate = true;
                        failedRow.duplicateMoneyMovementId = error.duplicateMoneyMovementId;
                        failedRow.replaceDuplicate = false;
                    }
                }
            }
            state.importErrors = [...recognitionErrors, ...result.errors.map(error => ({
                ...error,
                lineNumber: submittedRows[error.lineNumber - 1].sourceLineNumber
            }))];
            state.hideDuplicateImportRows = false;
            lastResult = result;
            renderState();
            await reloadMovements();
        }
        catch (error) {
            if (!isCurrent()) { return; }
            if (!writeConfirmed && isUnknownWriteOutcome(error)) {
                unknownOutcome = true;
                showImportAlert("Результат импорта неизвестен. Обновите список операций и сверьте выписку; затем выполните новый предпросмотр перед повтором.");
            } else {
                showImportAlert(writeConfirmed ? getErrorMessage(error) : getWriteErrorMessage(error));
            }
            updateImportSaveState();
        }
        finally {
            // До конца POST ни новый preview, ни save не стартуют, даже после принудительного сброса окна.
            saving = false;
            if (!pageActive) { return; }
            updateImportBusyState();
            updateImportSaveState();
            ui.importSaveButton.textContent = "Импортировать";
        }
    }

    function handleImportPreviewInput(event: Event): void {
        if (saving || previewing) {
            return;
        }
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
        if (saving || previewing) {
            return;
        }
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
        if (saving || previewing) {
            return;
        }
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
        renderState();
    }

    function handleToggleDuplicateReplacementsChange(): void {
        if (saving || previewing) {
            return;
        }
        for (const row of state.importRows) {
            if (row.isDuplicate) {
                row.replaceDuplicate = ui.importToggleDuplicateReplacementsInput.checked;
            }
        }

        renderState();
    }

    function handleSkipDuplicatesChange(): void {
        if (saving || previewing) {
            return;
        }
        if (ui.importSkipDuplicatesInput.checked) {
            for (const row of state.importRows) {
                if (row.isDuplicate) {
                    row.replaceDuplicate = false;
                }
            }
        }

        renderState();
    }

    function handleToggleDuplicatesClick(): void {
        if (saving || previewing) {
            return;
        }
        state.hideDuplicateImportRows = !state.hideDuplicateImportRows;
        renderState();
    }

    function readImportRequest(): SaveBankStatementImportRequest {
        if (!ui.importAccountSelect.value) {
            throw new Error("Выберите счёт для импорта.");
        }

        if (unknownOutcome) {
            throw new Error("Сначала сверьте операции и выполните новый предпросмотр.");
        }
        if (!previewIdentity || !matchesIdentity(previewIdentity)) {
            throw new Error("Файл, банк или счёт изменён после предпросмотра. Выполните новый предпросмотр.");
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

    function renderState(): void {
        const visibleRows = state.hideDuplicateImportRows
            ? state.importRows.filter(row => !row.isDuplicate)
            : state.importRows;

        renderImportPreview(ui.importPreview, visibleRows);
        renderImportErrors(ui.importErrors, state.importErrors);
        renderImportSummary(ui.importSummary, state.importRows);
        if (lastResult !== null) {
            ui.importSummary.textContent = `Создано: ${lastResult.createdCount}. Обновлено: ${lastResult.updatedCount}. Ошибок: ${lastResult.errorCount}. Осталось строк: ${state.importRows.length}.`;
        }
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
        if (saving || previewing) {
            ui.importSkipDuplicatesInput.disabled = true;
            ui.importToggleDuplicateReplacementsInput.disabled = true;
            ui.importToggleDuplicatesButton.disabled = true;
        }
    }

    function updateImportSaveState(): void {
        const rowsForImport = getRowsForImport();
        const hasRows = rowsForImport.length > 0;
        const hasUnresolvedDuplicates = !ui.importSkipDuplicatesInput.checked && rowsForImport.some(row => row.isDuplicate && row.replaceDuplicate !== true);
        ui.importSaveButton.disabled = saving || previewing || unknownOutcome || !hasRows || hasUnresolvedDuplicates
            || !previewIdentity || !matchesIdentity(previewIdentity);
    }

    function updateImportBusyState(): void {
        const busy = saving || previewing;
        ui.importPreviewButton.disabled = busy;
        ui.importAccountSelect.disabled = busy;
        ui.importBankSelect.disabled = busy;
        ui.importFileInput.disabled = busy;
        ui.importToggleDuplicateReplacementsInput.disabled = busy;
        ui.importSkipDuplicatesInput.disabled = busy;
        ui.importToggleDuplicatesButton.disabled = busy;
        if (!busy) {
            updateImportPreviewControls();
        }
        updateImportSaveState();
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
        previewOwner = null;
        previewing = false;
        previewRequests.invalidate();
        modalContext.invalidate();
        state.selectedImportFile = null;
        ui.importFileInput.value = "";
        ui.importSkipDuplicatesInput.checked = false;
        ui.importToggleDuplicateReplacementsInput.checked = false;
        ui.importToggleDuplicateReplacementsInput.indeterminate = false;
        ui.importFileName.textContent = "";
        state.importRows = [];
        state.importErrors = [];
        state.hideDuplicateImportRows = false;
        lastResult = null;
        previewIdentity = null;
        unknownOutcome = false;
        recognitionErrors = [];
        hideImportAlert();
        renderState();
    }

    function showImportAlert(message: string): void {
        ui.importAlert.textContent = message;
        ui.importAlert.classList.toggle("d-none", !message);
    }

    function hideImportAlert(): void {
        showImportAlert("");
    }
}

function getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : "Не удалось выполнить операцию.";
}
