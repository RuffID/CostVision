import type { BootstrapModal } from "../../shared/bootstrap.js";
import { initFileDropzone } from "../../shared/dropzone.js";
import { importMoneyMovements, previewBankStatementImport } from "./api.js";
import { renderImportErrors, renderImportPreview, renderImportSummary } from "./render.js";
import type { MoneyMovementsState } from "./state.js";
import type { BankStatementImportPreviewRowDto, BankStatementImportRowRequest, SaveBankStatementImportRequest } from "./types.js";
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
    const { ui, state, importModal, getForgeryToken, reloadMovements } = options;

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
        state.selectedImportFile = file;
        ui.importFileName.textContent = file.name;
        state.importRows = [];
        state.importErrors = [];
        renderState();
        hideImportAlert();
    }

    async function handleImportPreviewClick(): Promise<void> {
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

            ui.importPreviewButton.disabled = true;
            ui.importPreviewButton.textContent = "Распознавание...";

            const preview = await previewBankStatementImport(getForgeryToken(), ui.importBankSelect.value, ui.importAccountSelect.value, state.selectedImportFile);
            state.importRows = preview.rows.map(row => ({
                ...row,
                replaceDuplicate: false
            }));
            state.importErrors = preview.errors;
            renderState();
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

            const unresolvedDuplicate = getRowsForImport().find(row => row.isDuplicate && row.replaceDuplicate !== true);
            if (unresolvedDuplicate) {
                throw new Error("Удалите повторяющиеся операции из импорта или отметьте замену существующих.");
            }

            const request = readImportRequest();
            ui.importSaveButton.disabled = true;
            ui.importSaveButton.textContent = "Импорт...";

            await importMoneyMovements(getForgeryToken(), request);
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
        renderState();
    }

    function handleToggleDuplicateReplacementsChange(): void {
        for (const row of state.importRows) {
            if (row.isDuplicate) {
                row.replaceDuplicate = ui.importToggleDuplicateReplacementsInput.checked;
            }
        }

        renderState();
    }

    function handleSkipDuplicatesChange(): void {
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
        state.hideDuplicateImportRows = !state.hideDuplicateImportRows;
        renderState();
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

    function renderState(): void {
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
        state.selectedImportFile = null;
        ui.importFileInput.value = "";
        ui.importSkipDuplicatesInput.checked = false;
        ui.importToggleDuplicateReplacementsInput.checked = false;
        ui.importToggleDuplicateReplacementsInput.indeterminate = false;
        ui.importFileName.textContent = "";
        state.importRows = [];
        state.importErrors = [];
        state.hideDuplicateImportRows = false;
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
