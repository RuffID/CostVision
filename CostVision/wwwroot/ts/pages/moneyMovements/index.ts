import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";
import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { createMoneyMovement, loadAccounts, loadMoneyMovements } from "./api.js";
import { renderAccountOptions, renderMoneyMovementList, renderSummary } from "./render.js";
import { state } from "./state.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type CreateMoneyMovementRequest, type MoneyMovementType } from "./types.js";
import { getMoneyMovementsUi, type MoneyMovementsUi } from "./ui.js";

let ui: MoneyMovementsUi;
let forgeryToken: string | null;

document.addEventListener("DOMContentLoaded", () => {
    initMoneyMovementsPage();
});

async function initMoneyMovementsPage(): Promise<void> {
    ui = getMoneyMovementsUi();
    forgeryToken = getRequestVerificationToken();
    initDefaultDates();
    bindEvents();
    await loadInitialData();
}

function bindEvents(): void {
    ui.createForm.addEventListener("submit", handleCreateSubmit);
    ui.applyFilterButton.addEventListener("click", handleApplyFilterClick);
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
    renderMoneyMovementList(ui.list, state.movements);
    renderSummary(ui.count, ui.incomeSum, ui.expenseSum, state.movements);
}

function readCreateRequest(): CreateMoneyMovementRequest {
    const amount = Number(ui.amountInput.value);

    if (!Number.isFinite(amount) || amount === 0) {
        throw new Error("Введите сумму операции.");
    }

    if (!ui.accountSelect.value) {
        throw new Error("Выберите счёт.");
    }

    return {
        accountId: ui.accountSelect.value,
        amount: amount,
        type: readMovementType(),
        occurredAt: ui.occurredAtInput.value,
        comment: ui.commentInput.value.trim() || null
    };
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
