import { getWriteErrorMessage } from "../../shared/requestCoordinator.js";

import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";

import { updateMoneyMovementComment } from "./api.js";

import { type MoneyMovementDto } from "./types.js";

import type { MoneyMovementsSession } from "./state.js";
import type { MoneyMovementsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<MoneyMovementsSession,
        "commands" | "data" | "forgeryToken" | "movementRequests">;
    bindings: Pick<MoneyMovementsBindings,
        "ui">;
    findMovement: (moneyMovementId: string) => MoneyMovementDto | undefined;
    renderMovements: () => void;
}

export function createMovementCommentCommands(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function startCommentEdit(moneyMovementId: string | null): void {
        if (!moneyMovementId || !dependencies.findMovement(moneyMovementId)) {
            return;
        }

        session.data.editingCommentMovementId = moneyMovementId;
        dependencies.renderMovements();
        const input = bindings.ui.list.querySelector<HTMLTextAreaElement>(`[data-action="edit-comment-input"][data-money-movement-id="${moneyMovementId}"]`);
        input?.focus();
    }

    function cancelCommentEdit(): void {
        session.data.editingCommentMovementId = null;
        dependencies.renderMovements();
    }

    async function handleSaveCommentClick(button: HTMLButtonElement): Promise<void> {
        if (button.disabled) { return; }
        const moneyMovementId = button.getAttribute("data-money-movement-id");
        if (!moneyMovementId) {
            return;
        }

        const movement = dependencies.findMovement(moneyMovementId);
        if (!movement) {
            showAlertMessage(bindings.ui.alert, "Операция не найдена в текущем списке.");
            return;
        }

        const input = bindings.ui.list.querySelector<HTMLTextAreaElement>(`[data-action="edit-comment-input"][data-money-movement-id="${moneyMovementId}"]`);
        if (!input) {
            showAlertMessage(bindings.ui.alert, "Поле комментария не найдено.");
            return;
        }

        if (session.commands.has(moneyMovementId)) { return; }
        session.commands.add(moneyMovementId);
        const matchesList = session.movementRequests.capture();
        const originalValue = input.value;
        const matchesEditor = () => matchesList() && session.data.editingCommentMovementId === moneyMovementId
            && bindings.ui.list.querySelector(`[data-action="edit-comment-input"][data-money-movement-id="${moneyMovementId}"]`) === input;
        const isCurrent = () => matchesEditor() && input.value === originalValue;
        const comment = input.value.trim() || null;
        const originalText = button.textContent;
        button.disabled = true;
        button.textContent = "...";

        try {
            hideAlertMessage(bindings.ui.alert);
            await updateMoneyMovementComment(session.forgeryToken, {
                moneyMovementId: movement.id,
                accountId: movement.accountId,
                comment: comment
            });

            if (!isCurrent()) { return; }
            movement.comment = comment;
            session.data.editingCommentMovementId = null;
            dependencies.renderMovements();
        }
        catch (error) {
            if (matchesEditor()) { showAlertMessage(bindings.ui.alert, getWriteErrorMessage(error)); }
        }
        finally {
            session.commands.delete(moneyMovementId);
            if (!matchesEditor()) { return; }
            button.disabled = false;
            button.textContent = originalText;
        }
    }

    return { startCommentEdit, cancelCommentEdit, handleSaveCommentClick };
}
