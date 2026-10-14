import { isIntentionalAbort } from "../../shared/requestCoordinator.js";
import { createPageNavigation } from "../../shared/paging.js";

import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { createBootstrapModal } from "../../shared/bootstrap.js";

import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { loadAccounts, loadImportBanks, loadMoneyMovements } from "./api.js";
import { formatDateForQuery, getDateRangeByPeriodPreset } from "./dateRange.js";
import { initMoneyMovementHelpTooltips } from "./helpTooltips.js";
import { initMoneyMovementImportModalController } from "./importModal.js";
import { MONEY_MOVEMENT_ACCOUNT_FILTER_WITHOUT_ACCOUNT, renderAccountOptions, renderImportBankOptions, renderMoneyMovementList, renderSummary } from "./render.js";
import { createMoneyMovementsSession } from "./state.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, type MoneyMovementDto, type UserAccountViewModel } from "./types.js";
import { getMoneyMovementsUi } from "./ui.js";

import { MoneyMovementsBindings } from "./bindings.js";
import { createMovementCreateCommand } from "./createCommand.js";
import { createMovementReceiptLinksController } from "./receiptLinksController.js";
import { createMovementCommentCommands } from "./commentCommands.js";
import { createMovementAccountActionsController } from "./accountActionsController.js";

export function createMoneyMovementsPage() {
    const session = createMoneyMovementsSession();
    const bindings = new MoneyMovementsBindings();
    window.addEventListener("pagehide", () => {
        session.initialRequests.invalidate();
        session.pageContext.invalidate();
        session.movementRequests.invalidate();
        session.linkedRequests.invalidate();
        session.candidateRequests.invalidate();
        session.detailRequests.invalidate();
        session.receiptsRevision++;
        if (session.data.searchDebounceTimerId !== null) {
            window.clearTimeout(session.data.searchDebounceTimerId);
            session.data.searchDebounceTimerId = null;
        }
    });
    const createCommand = createMovementCreateCommand({ session, bindings,
        reloadMovements: (...args) => reloadMovements(...args),
    });
    const receiptLinks = createMovementReceiptLinksController({ session, bindings,
        findMovement: (...args) => findMovement(...args),
        getMovementInfoText: (...args) => getMovementInfoText(...args),
        getErrorMessage: (...args) => getErrorMessage(...args),
        reloadMovements: (...args) => reloadMovements(...args),
        formatDateInput: (...args) => formatDateInput(...args),
    });
    const commentCommands = createMovementCommentCommands({ session, bindings,
        findMovement: (...args) => findMovement(...args),
        renderMovements: (...args) => renderMovements(...args),
    });
    const accountActions = createMovementAccountActionsController({ session, bindings,
        findMovement: (...args) => findMovement(...args),
        findAccount: (...args) => findAccount(...args),
        canEditAccount: (...args) => canEditAccount(...args),
        reloadMovements: (...args) => reloadMovements(...args),
        getMovementInfoText: (...args) => getMovementInfoText(...args),
    });

    async function initMoneyMovementsPage(): Promise<void> {
        bindings.ui = getMoneyMovementsUi();
        session.forgeryToken = getRequestVerificationToken();
        bindings.movementNavigation = createPageNavigation(bindings.ui.list, async page => {
            session.movementPage = page;
            await reloadMovements();
        }, error => showAlertMessage(bindings.ui.alert, getErrorMessage(error)));
        bindings.candidateNavigation = createPageNavigation(bindings.ui.receiptCandidates, async page => {
            session.candidatePage = page;
            await receiptLinks.reloadReceiptCandidates();
        }, error => receiptLinks.showReceiptsAlert(getErrorMessage(error)), "Документов");
        bindings.moveAccountModal = createBootstrapModal(bindings.ui.moveAccountModal);
        bindings.deleteModal = createBootstrapModal(bindings.ui.deleteModal);
        bindings.importModal = createBootstrapModal(bindings.ui.importModal);
        bindings.receiptsModal = createBootstrapModal(bindings.ui.receiptsModal);
        bindings.receiptDetailsModal = createBootstrapModal(bindings.ui.receiptDetailsModal);
        bindings.importController = initMoneyMovementImportModalController({
            ui: bindings.ui,
            state: session.data,
            importModal: bindings.importModal,
            getForgeryToken: () => session.forgeryToken,
            reloadMovements: reloadMovements
        });
        initMoneyMovementHelpTooltips(bindings.ui);
        initDefaultDates();
        bindEvents();
        await loadInitialData();
    }

    function bindEvents(): void {
        bindings.ui.createForm.addEventListener("submit", createCommand.handleCreateSubmit);
        bindings.ui.amountInput.addEventListener("input", createCommand.handleAmountInput);
        bindings.ui.applyFilterButton.addEventListener("click", handleApplyFilterClick);
        bindings.ui.periodPresetSelect.addEventListener("change", handlePeriodPresetChange);
        bindings.ui.searchInput.addEventListener("input", handleSearchInput);
        bindings.ui.receiptFilterSelect.addEventListener("change", () => { void handleApplyFilterClick(); });
        bindings.ui.list.addEventListener("click", handleMoneyMovementListClick);
        bindings.ui.moveTargetAccountSelect.addEventListener("change", accountActions.updateMoveActionState);
        bindings.ui.confirmMoveAccountButton.addEventListener("click", accountActions.handleConfirmMoveAccountClick);
        bindings.ui.removeFromAccountButton.addEventListener("click", accountActions.handleRemoveFromAccountClick);
        bindings.ui.confirmDeleteButton.addEventListener("click", accountActions.handleConfirmDeleteClick);
        bindings.ui.receiptsReloadCandidatesButton.addEventListener("click", receiptLinks.handleReloadReceiptCandidatesClick);
        bindings.ui.receiptsUseTimeWindowInput.addEventListener("change", receiptLinks.updateReceiptFilterState);
        bindings.ui.receiptsUseAmountFilterInput.addEventListener("change", receiptLinks.updateReceiptFilterState);
        bindings.ui.linkedReceipts.addEventListener("click", receiptLinks.handleLinkedReceiptsClick);
        for (const input of [bindings.ui.receiptsDateFromInput, bindings.ui.receiptsDateToInput, bindings.ui.receiptsAmountToleranceInput,
            bindings.ui.receiptsTimeWindowHoursInput, bindings.ui.receiptsUseTimeWindowInput, bindings.ui.receiptsUseAmountFilterInput, bindings.ui.receiptsExcludeLinkedInput]) {
            input.addEventListener("change", receiptLinks.invalidateCandidates);
            input.addEventListener("input", receiptLinks.invalidateCandidates);
        }
        for (const input of [bindings.ui.dateFromInput, bindings.ui.dateToInput, bindings.ui.filterAccountSelect]) {
            input.addEventListener("change", () => session.movementRequests.invalidate());
        }
        bindings.ui.receiptCandidates.addEventListener("click", receiptLinks.handleReceiptCandidatesClick);

        bindings.ui.moveAccountModal.addEventListener("hide.bs.modal", accountActions.resetMoveModal);
        bindings.ui.deleteModal.addEventListener("hide.bs.modal", () => { session.data.pendingAccountAction = null; accountActions.resetDeleteModal(); });
        bindings.ui.receiptsModal.addEventListener("hide.bs.modal", receiptLinks.resetReceiptsModal);
        bindings.ui.receiptDetailsModal.addEventListener("hide.bs.modal", () => session.detailRequests.invalidate());
    }

    async function loadInitialData(): Promise<void> {
        const request = session.initialRequests.begin(session.forgeryToken);
        try {
            hideAlertMessage(bindings.ui.alert);
            const accounts = await loadAccounts(request.parameters, request.signal);
            if (!request.isCurrent()) { return; }
            const banks = await loadImportBanks(request.parameters, request.signal);
            if (!request.isCurrent()) { return; }
            session.data.accounts = accounts;
            session.data.importBanks = banks;
            renderAccountOptions(bindings.ui.accountSelect, session.data.accounts, false);
            renderAccountOptions(bindings.ui.filterAccountSelect, session.data.accounts, true);
            renderAccountOptions(bindings.ui.importAccountSelect, session.data.accounts, false);
            renderImportBankOptions(bindings.ui.importBankSelect, session.data.importBanks);

            if (session.data.accounts.length > 0) {
                bindings.ui.accountSelect.value = session.data.accounts[0].id;
                bindings.ui.importAccountSelect.value = session.data.accounts[0].id;
            }

            bindings.importController.renderState();
            await reloadMovements();
        }
        catch (error) {
            if (request.isCurrent() && !isIntentionalAbort(error)) { showAlertMessage(bindings.ui.alert, getErrorMessage(error)); }
        }
    }

    async function handleApplyFilterClick(): Promise<void> {
        try {
            hideAlertMessage(bindings.ui.alert);
            await reloadMovements();
        }
        catch (error) {
            showAlertMessage(bindings.ui.alert, getErrorMessage(error));
        }
    }

    async function reloadMovements(): Promise<void> {
        session.data.dateFrom = bindings.ui.dateFromInput.value;
        session.data.dateTo = bindings.ui.dateToInput.value;
        session.data.selectedAccountId = bindings.ui.filterAccountSelect.value;
        const accountId = session.data.selectedAccountId === MONEY_MOVEMENT_ACCOUNT_FILTER_WITHOUT_ACCOUNT
            ? "00000000-0000-0000-0000-000000000000"
            : session.data.selectedAccountId;
        const key = JSON.stringify([session.data.dateFrom, session.data.dateTo, accountId, session.data.searchQuery, bindings.ui.receiptFilterSelect.value]);
        if (key !== session.movementFilterKey) {
            session.movementPage = 1;
            session.movementFilterKey = key;
        }
        const request = session.movementRequests.begin({ dateFrom: session.data.dateFrom, dateTo: session.data.dateTo, accountId,
            page: session.movementPage, search: session.data.searchQuery, receiptFilter: bindings.ui.receiptFilterSelect.value });
        const p = request.parameters;
        const matchesFilters = () => key === JSON.stringify([bindings.ui.dateFromInput.value, bindings.ui.dateToInput.value,
            bindings.ui.filterAccountSelect.value === MONEY_MOVEMENT_ACCOUNT_FILTER_WITHOUT_ACCOUNT ? "00000000-0000-0000-0000-000000000000" : bindings.ui.filterAccountSelect.value,
            session.data.searchQuery, bindings.ui.receiptFilterSelect.value]);
        session.movementTotals = null;
        session.data.movements = [];
        bindings.movementNavigation.clear();
        renderMovements();
        try {
            const response = await loadMoneyMovements(session.forgeryToken, p.dateFrom, p.dateTo, p.accountId,
                p.page, p.search, p.receiptFilter, request.signal);
            if (!request.isCurrent() || !matchesFilters()) {
                return;
            }
            session.movementTotals = response;
            session.movementPage = response.page;
            session.data.movements = response.items;
            bindings.movementNavigation.render(response);
            session.data.editingCommentMovementId = null;
            renderMovements();
        } catch (error) {
            if (request.isCurrent() && matchesFilters() && !isIntentionalAbort(error)) { showAlertMessage(bindings.ui.alert, getErrorMessage(error)); }
        }
    }

    function renderMovements(): void {
        renderMoneyMovementList(bindings.ui.list, session.data.movements, session.data.accounts, session.data.editingCommentMovementId);
        renderSummary(bindings.ui.count, bindings.ui.incomeSum, bindings.ui.expenseSum, session.movementTotals);
    }

    function handleMoneyMovementListClick(event: MouseEvent): void {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const editCommentButton = target.closest<HTMLButtonElement>('[data-action="edit-comment"]');
        if (editCommentButton) {
            commentCommands.startCommentEdit(editCommentButton.getAttribute("data-money-movement-id"));
            return;
        }

        const cancelCommentButton = target.closest<HTMLButtonElement>('[data-action="cancel-comment"]');
        if (cancelCommentButton) {
            commentCommands.cancelCommentEdit();
            return;
        }

        const saveCommentButton = target.closest<HTMLButtonElement>('[data-action="save-comment"]');
        if (saveCommentButton) {
            commentCommands.handleSaveCommentClick(saveCommentButton).catch(error => showAlertMessage(bindings.ui.alert, getErrorMessage(error)));
            return;
        }

        const accountButton = target.closest<HTMLButtonElement>('[data-action="edit-account-link"]');
        const receiptsButton = target.closest<HTMLButtonElement>('[data-action="open-receipts"]');
        if (receiptsButton) {
            receiptLinks.handleOpenReceiptsClick(receiptsButton).catch(error => showAlertMessage(bindings.ui.alert, getErrorMessage(error)));
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

        accountActions.openMoveAccountModal(moneyMovementId, accountId);
    }

    function findMovement(moneyMovementId: string): MoneyMovementDto | undefined {
        return session.data.movements.find(movement => movement.id === moneyMovementId);
    }

    function findAccount(accountId: string): UserAccountViewModel | undefined {
        return session.data.accounts.find(account => account.id === accountId);
    }

    function canEditAccount(account: UserAccountViewModel): boolean {
        return account.canManage === true || account.accessRole === 1 || account.accessRole === 2;
    }

    function getMovementInfoText(movement: MoneyMovementDto): string {
        const sign = movement.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "-" : "+";
        return `${movement.comment || "Без комментария"} · ${movement.accountName || "Без счёта"} · ${sign}${movement.amount}`;
    }

    function showImportAlert(message: string): void {
        bindings.ui.importAlert.textContent = message;
        bindings.ui.importAlert.classList.toggle("d-none", !message);
    }

    function hideImportAlert(): void {
        showImportAlert("");
    }

    function initDefaultDates(): void {
        const now = new Date();
        bindings.ui.periodPresetSelect.value = "currentMonth";
        applySelectedPeriodPreset(now);
        bindings.ui.occurredAtInput.value = formatDateTimeLocalInput(now);
    }

    function handlePeriodPresetChange(): void {
        session.movementRequests.invalidate();
        applySelectedPeriodPreset(new Date());
    }

    function applySelectedPeriodPreset(now: Date): void {
        const range = getDateRangeByPeriodPreset(bindings.ui.periodPresetSelect.value || "currentMonth", now);
        bindings.ui.dateFromInput.value = formatDateForQuery(range.dateFrom);
        bindings.ui.dateToInput.value = formatDateForQuery(range.dateTo);
    }

    function handleSearchInput(): void {
        session.movementRequests.invalidate();
        session.data.searchQuery = bindings.ui.searchInput.value.trim();

        if (session.data.searchDebounceTimerId) {
            clearTimeout(session.data.searchDebounceTimerId);
        }

        session.data.searchDebounceTimerId = window.setTimeout(() => { void handleApplyFilterClick(); }, 250);
    }

    function formatDateInput(date: Date): string {
        return formatDateForQuery(date);
    }

    function formatDateTimeLocalInput(date: Date): string {
        const hours = String(date.getHours()).padStart(2, "0");
        const minutes = String(date.getMinutes()).padStart(2, "0");
        return `${formatDateInput(date)}T${hours}:${minutes}`;
    }

    function getErrorMessage(error: unknown): string {
        return error instanceof Error ? error.message : "Не удалось выполнить операцию.";
    }

    return { init: initMoneyMovementsPage };
}
