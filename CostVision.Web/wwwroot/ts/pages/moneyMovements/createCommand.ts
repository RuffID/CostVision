import { getWriteErrorMessage } from "../../shared/requestCoordinator.js";

import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";

import { createMoneyMovement } from "./api.js";

import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type CreateMoneyMovementRequest, type MoneyMovementType } from "./types.js";

import type { MoneyMovementsSession } from "./state.js";
import type { MoneyMovementsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<MoneyMovementsSession,
        "creating" | "forgeryToken" | "movementRequests" | "pageContext">;
    bindings: Pick<MoneyMovementsBindings,
        "ui">;
    reloadMovements: () => Promise<void>;
}

export function createMovementCreateCommand(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    async function handleCreateSubmit(event: SubmitEvent): Promise<void> {
        event.preventDefault();
        if (session.creating) { return; }
        session.creating = true;
        const matchesList = session.movementRequests.capture();
        const matchesPage = session.pageContext.capture();
        let draft: string;

        try {
            hideAlertMessage(bindings.ui.alert);
            bindings.ui.createButton.disabled = true;
            const request = readCreateRequest();
            draft = JSON.stringify([bindings.ui.accountSelect.value, bindings.ui.amountInput.value, bindings.ui.occurredAtInput.value, bindings.ui.typeSelect.value, bindings.ui.commentInput.value]);
            await createMoneyMovement(session.forgeryToken, request);
            if (!matchesPage()) { return; }
            if (draft === JSON.stringify([bindings.ui.accountSelect.value, bindings.ui.amountInput.value, bindings.ui.occurredAtInput.value, bindings.ui.typeSelect.value, bindings.ui.commentInput.value])) {
                bindings.ui.amountInput.value = "";
                bindings.ui.commentInput.value = "";
                updateMovementTypeByAmount();
            }
            if (matchesList()) { await dependencies.reloadMovements(); }
        }
        catch (error) {
            if (matchesList()) { showAlertMessage(bindings.ui.alert, getWriteErrorMessage(error)); }
        }
        finally {
            session.creating = false;
            if (matchesPage()) { bindings.ui.createButton.disabled = false; }
        }
    }

    function readCreateRequest(): CreateMoneyMovementRequest {
        const amount = Number(bindings.ui.amountInput.value);

        if (!Number.isFinite(amount) || amount === 0) {
            throw new Error("Введите сумму операции.");
        }

        if (!bindings.ui.accountSelect.value) {
            throw new Error("Выберите счёт.");
        }

        updateMovementTypeByAmount();

        return {
            accountId: bindings.ui.accountSelect.value,
            amount: amount,
            type: readMovementType(),
            occurredAt: bindings.ui.occurredAtInput.value,
            comment: bindings.ui.commentInput.value.trim() || null
        };
    }

    function handleAmountInput(): void {
        updateMovementTypeByAmount();
    }

    function readMovementType(): MoneyMovementType | null {
        if (bindings.ui.typeSelect.value === "Expense") {
            return MONEY_MOVEMENT_TYPE_EXPENSE;
        }

        if (bindings.ui.typeSelect.value === "Income") {
            return MONEY_MOVEMENT_TYPE_INCOME;
        }

        return null;
    }

    function updateMovementTypeByAmount(): void {
        const amount = Number(bindings.ui.amountInput.value);
        const expenseOption = getTypeOption("Expense");
        const incomeOption = getTypeOption("Income");

        expenseOption.disabled = false;
        incomeOption.disabled = false;

        if (!Number.isFinite(amount) || amount === 0) {
            return;
        }

        if (amount < 0) {
            bindings.ui.typeSelect.value = "Expense";
            incomeOption.disabled = true;
            return;
        }

        bindings.ui.typeSelect.value = "Income";
        expenseOption.disabled = true;
    }

    function getTypeOption(value: string): HTMLOptionElement {
        const option = Array.from(bindings.ui.typeSelect.options).find(item => item.value === value);

        if (!option) {
            throw new Error(`Не найден тип операции: ${value}.`);
        }

        return option;
    }

    return { handleCreateSubmit, handleAmountInput };
}
