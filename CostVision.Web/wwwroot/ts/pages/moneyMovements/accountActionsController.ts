import { getWriteErrorMessage } from "../../shared/requestCoordinator.js";

import { showAlertMessage } from "../../shared/alerts.js";

import { deleteMoneyMovement, moveMoneyMovementToAccount } from "./api.js";

import { type MoneyMovementDto, type UserAccountViewModel } from "./types.js";

import type { MoneyMovementsSession } from "./state.js";
import type { MoneyMovementsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<MoneyMovementsSession,
        "commands" | "data" | "forgeryToken" | "movementRequests">;
    bindings: Pick<MoneyMovementsBindings,
        "deleteModal" | "moveAccountModal" | "ui">;
    findMovement: (moneyMovementId: string) => MoneyMovementDto | undefined;
    findAccount: (accountId: string) => UserAccountViewModel | undefined;
    canEditAccount: (account: UserAccountViewModel) => boolean;
    reloadMovements: () => Promise<void>;
    getMovementInfoText: (movement: MoneyMovementDto) => string;
}

export function createMovementAccountActionsController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function openMoveAccountModal(moneyMovementId: string, sourceAccountId: string): void {
        const movement = dependencies.findMovement(moneyMovementId);
        if (!movement) {
            showAlertMessage(bindings.ui.alert, "Операция не найдена в текущем списке.");
            return;
        }

        const sourceAccount = sourceAccountId ? dependencies.findAccount(sourceAccountId) : null;
        if (!sourceAccount || !dependencies.canEditAccount(sourceAccount)) {
            if (!sourceAccountId) {
                session.data.pendingAccountAction = {
                    moneyMovementId: moneyMovementId,
                    sourceAccountId: sourceAccountId
                };

                bindings.ui.moveSourceAccount.textContent = "Без счёта";
                bindings.ui.moveSourceAccount.style.border = "2px solid #dee2e6";
                hideMoveAccountAlert();
                renderMoveAccountOptions(sourceAccountId);
                updateMoveActionState();
                bindings.moveAccountModal.show();
                return;
            }

            showAlertMessage(bindings.ui.alert, "Недостаточно прав для изменения операции в выбранном счёте.");
            return;
        }

        session.data.pendingAccountAction = {
            moneyMovementId: moneyMovementId,
            sourceAccountId: sourceAccountId
        };

        bindings.ui.confirmMoveAccountButton.textContent = "Перенести";
        bindings.ui.moveSourceAccount.textContent = movement.accountName || "Счёт";
        bindings.ui.moveSourceAccount.style.border = "2px solid " + movement.accountColorHex;
        hideMoveAccountAlert();
        renderMoveAccountOptions(sourceAccountId);
        updateMoveActionState();
        bindings.moveAccountModal.show();
    }

    function renderMoveAccountOptions(sourceAccountId: string): void {
        bindings.ui.moveTargetAccountSelect.replaceChildren();

        for (const account of session.data.accounts) {
            if (account.id === sourceAccountId) {
                continue;
            }

            const option = document.createElement("option");
            option.value = account.id;
            option.textContent = account.name;

            if (!dependencies.canEditAccount(account)) {
                option.disabled = true;
                option.setAttribute("data-reason", "Недостаточно прав для изменения операций в этом счёте.");
            }

            bindings.ui.moveTargetAccountSelect.append(option);
        }

        const firstEnabledOption = Array.from(bindings.ui.moveTargetAccountSelect.options).find(option => option.value && !option.disabled);
        bindings.ui.moveTargetAccountSelect.value = firstEnabledOption ? firstEnabledOption.value : "";
        bindings.ui.moveTargetAccountSelect.disabled = bindings.ui.moveTargetAccountSelect.options.length === 0;
    }

    function updateMoveActionState(): void {
        const selectedOption = getSelectedMoveTargetOption();
        const canMove = !!selectedOption && !selectedOption.disabled;

        bindings.ui.confirmMoveAccountButton.disabled = !canMove;
        bindings.ui.removeFromAccountButton.disabled = session.data.pendingAccountAction === null || !session.data.pendingAccountAction.sourceAccountId;

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
        const selectedIndex = bindings.ui.moveTargetAccountSelect.selectedIndex;
        if (selectedIndex < 0) {
            return null;
        }

        const option = bindings.ui.moveTargetAccountSelect.options[selectedIndex];
        return option && option.value ? option : null;
    }

    function getUnavailableMoveReason(): string {
        if (bindings.ui.moveTargetAccountSelect.options.length === 0) {
            return "Нет других доступных счетов для переноса операции.";
        }

        const disabledOption = Array.from(bindings.ui.moveTargetAccountSelect.options).find(option => option.disabled && option.getAttribute("data-reason"));
        return disabledOption?.getAttribute("data-reason") || "Нет доступных счетов для переноса операции.";
    }

    async function handleConfirmMoveAccountClick(): Promise<void> {
        const action = session.data.pendingAccountAction;
        if (!action || session.commands.has(action.moneyMovementId)) { return; }
        const selectedOption = getSelectedMoveTargetOption();
        if (!selectedOption || selectedOption.disabled) { updateMoveActionState(); return; }
        const targetAccountId = selectedOption.value;
        const matchesList = session.movementRequests.capture();
        const isCurrent = () => session.data.pendingAccountAction === action && matchesList();
        const originalText = bindings.ui.confirmMoveAccountButton.textContent;
        bindings.ui.confirmMoveAccountButton.disabled = true;
        bindings.ui.confirmMoveAccountButton.textContent = "...";
        session.commands.add(action.moneyMovementId);
        try {
            await moveMoneyMovementToAccount(session.forgeryToken, { moneyMovementId: action.moneyMovementId, sourceAccountId: action.sourceAccountId, targetAccountId });
            if (!isCurrent()) { return; }
            session.data.pendingAccountAction = null;
            bindings.moveAccountModal.hide();
            await dependencies.reloadMovements();
        } catch (error) {
            if (isCurrent()) { showMoveAccountAlert(getWriteErrorMessage(error)); }
        } finally {
            session.commands.delete(action.moneyMovementId);
            if (session.data.pendingAccountAction === action) {
                bindings.ui.confirmMoveAccountButton.textContent = originalText;
                updateMoveActionState();
            }
        }
    }

    function handleRemoveFromAccountClick(): void {
        if (!session.data.pendingAccountAction) {
            return;
        }

        const movement = dependencies.findMovement(session.data.pendingAccountAction.moneyMovementId);
        bindings.ui.deleteInfo.textContent = movement ? dependencies.getMovementInfoText(movement) : "";
        session.data.preserveMoveModalPendingOnHide = true;
        bindings.moveAccountModal.hide();
        bindings.ui.confirmDeleteButton.textContent = "Удалить";
        bindings.ui.confirmDeleteButton.disabled = false;
        bindings.deleteModal.show();
    }

    async function handleConfirmDeleteClick(): Promise<void> {
        const action = session.data.pendingAccountAction;
        if (!action || session.commands.has(action.moneyMovementId)) { return; }
        const matchesList = session.movementRequests.capture();
        const isCurrent = () => session.data.pendingAccountAction === action && matchesList();
        const originalText = bindings.ui.confirmDeleteButton.textContent;
        bindings.ui.confirmDeleteButton.disabled = true;
        bindings.ui.confirmDeleteButton.textContent = "...";
        session.commands.add(action.moneyMovementId);
        try {
            await deleteMoneyMovement(session.forgeryToken, { moneyMovementId: action.moneyMovementId, accountId: action.sourceAccountId });
            if (!isCurrent()) { return; }
            session.data.pendingAccountAction = null;
            bindings.deleteModal.hide();
            await dependencies.reloadMovements();
        } catch (error) {
            if (isCurrent()) { showAlertMessage(bindings.ui.alert, getWriteErrorMessage(error)); }
        } finally {
            session.commands.delete(action.moneyMovementId);
            if (session.data.pendingAccountAction === action) {
                bindings.ui.confirmDeleteButton.textContent = originalText;
                bindings.ui.confirmDeleteButton.disabled = false;
            }
        }
    }

    function resetMoveModal(): void {
        if (session.data.preserveMoveModalPendingOnHide) {
            session.data.preserveMoveModalPendingOnHide = false;
        } else {
            session.data.pendingAccountAction = null;
        }

        bindings.ui.moveTargetAccountSelect.replaceChildren();
        bindings.ui.moveTargetAccountSelect.disabled = false;
        bindings.ui.moveSourceAccount.textContent = "";
        bindings.ui.moveSourceAccount.style.border = "";
        hideMoveAccountAlert();
    }

    function resetDeleteModal(): void {
        bindings.ui.deleteInfo.textContent = "";
        bindings.ui.confirmDeleteButton.disabled = false;
    }

    function showMoveAccountAlert(message: string): void {
        bindings.ui.moveAccountAlert.textContent = message;
        bindings.ui.moveAccountAlert.classList.toggle("d-none", !message);
    }

    function hideMoveAccountAlert(): void {
        showMoveAccountAlert("");
    }

    return { updateMoveActionState, handleConfirmMoveAccountClick, handleRemoveFromAccountClick, handleConfirmDeleteClick, resetMoveModal, resetDeleteModal, openMoveAccountModal };
}
