import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { BootstrapModal, createBootstrapModal } from "../../shared/bootstrap.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { createMoneyMovement, deleteMoneyMovement, loadAccounts, loadMoneyMovements, moveMoneyMovementToAccount } from "./api.js";
import { renderAccountOptions, renderMoneyMovementList, renderSummary } from "./render.js";
import { state } from "./state.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type CreateMoneyMovementRequest, type MoneyMovementDto, type MoneyMovementType, type PendingMoneyMovementAccountAction, type UserAccountViewModel } from "./types.js";
import { getMoneyMovementsUi, type MoneyMovementsUi } from "./ui.js";

let ui: MoneyMovementsUi;
let forgeryToken: string | null;
let moveAccountModal: BootstrapModal;
let deleteModal: BootstrapModal;
let pendingAccountAction: PendingMoneyMovementAccountAction | null = null;
let preserveMoveModalPendingOnHide = false;

document.addEventListener("DOMContentLoaded", () => {
    initMoneyMovementsPage();
});

async function initMoneyMovementsPage(): Promise<void> {
    ui = getMoneyMovementsUi();
    forgeryToken = getRequestVerificationToken();
    moveAccountModal = createBootstrapModal(ui.moveAccountModal);
    deleteModal = createBootstrapModal(ui.deleteModal);
    initDefaultDates();
    bindEvents();
    await loadInitialData();
}

function bindEvents(): void {
    ui.createForm.addEventListener("submit", handleCreateSubmit);
    ui.amountInput.addEventListener("input", handleAmountInput);
    ui.applyFilterButton.addEventListener("click", handleApplyFilterClick);
    ui.list.addEventListener("click", handleMoneyMovementListClick);
    ui.moveTargetAccountSelect.addEventListener("change", updateMoveActionState);
    ui.confirmMoveAccountButton.addEventListener("click", handleConfirmMoveAccountClick);
    ui.removeFromAccountButton.addEventListener("click", handleRemoveFromAccountClick);
    ui.confirmDeleteButton.addEventListener("click", handleConfirmDeleteClick);

    ui.moveAccountModal.addEventListener("hidden.bs.modal", resetMoveModal);
    ui.deleteModal.addEventListener("hidden.bs.modal", resetDeleteModal);
}

async function loadInitialData(): Promise<void> {
    try {
        hideAlertMessage(ui.alert);
        state.accounts = await loadAccounts(forgeryToken);
        renderAccountOptions(ui.accountSelect, state.accounts, false);
        renderAccountOptions(ui.filterAccountSelect, state.accounts, true);

        if (state.accounts.length > 0) {
            ui.accountSelect.value = state.accounts[0].id;
        }

        await reloadMovements();
    }
    catch (error) {
        showAlertMessage(ui.alert, getErrorMessage(error));
    }
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
    renderMoneyMovementList(ui.list, state.movements, state.accounts);
    renderSummary(ui.count, ui.incomeSum, ui.expenseSum, state.movements);
}

function handleMoneyMovementListClick(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const accountButton = target.closest<HTMLButtonElement>('[data-action="edit-account-link"]');
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
    const monthStart = new Date(now.getFullYear(), now.getMonth(), 1);

    ui.dateFromInput.value = formatDateInput(monthStart);
    ui.dateToInput.value = formatDateInput(now);
    ui.occurredAtInput.value = formatDateTimeLocalInput(now);
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
