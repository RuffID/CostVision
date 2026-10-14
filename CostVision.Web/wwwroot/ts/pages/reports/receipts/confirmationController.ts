import { runReceiptAccountCommand, type ReceiptAccountCommand } from "../../../shared/receipts/workflow.js";
import { getWriteErrorMessage } from "../../../shared/requestCoordinator.js";

import { deleteReceiptApi, removeReceiptFromAccountApi } from "./api.js";

import { ReceiptDto } from "./types.js";

import { findReceiptByAccountReceiptId as findReceiptByAccountReceiptIdCore, findReceiptById as findReceiptByIdCore } from "./state/accountModel.js";
import { fillDeleteReceiptModal as fillDeleteReceiptModalUi } from "./modals/deleteReceiptModal.js";

import type { ReceiptsSession } from "./state.js";
import type { ReceiptsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<ReceiptsSession,
        "DELETE_ACTION_DELETE_RECEIPT" | "DELETE_ACTION_REMOVE_FROM_ACCOUNT" | "confirmationContext" | "currentReceiptList" | "forgeryToken" | "listRequests" | "pageState" | "pendingDeleteAction" | "pendingDeleteCardElement" | "pendingDeleteReceiptId" | "pendingReceiptAccountAction" | "preserveMoveReceiptAccountModalStateOnHide" | "receiptWrites" | "shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation">;
    bindings: Pick<ReceiptsBindings,
        "deleteBootstrapModal" | "deleteConfirmButton" | "deleteModalMessageElement" | "deleteModalReceiptInfoElement" | "deleteModalTitleElement" | "moveReceiptAccountBootstrapModal">;
    closeMoveReceiptAccountModal: () => void;
    loadReceiptList: (page?: number) => Promise<void>;
    formatCurrency: (value: number) => string;
    canRemoveReceiptFromSelectedAccount: () => boolean;
    updateMoveReceiptActionState: () => void;
}

export function createReceiptConfirmationController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function openDeleteReceiptModal(receiptId: string, cardElement: HTMLElement): void {
        session.confirmationContext.invalidate();
        const receipt = findReceiptByIdCore(session.pageState.receipts, receiptId);
        if (!receipt) {
            alert('Чек не найден в текущем списке.');
            return;
        }

        session.pendingDeleteReceiptId = receiptId;
        session.pendingDeleteCardElement = cardElement;
        session.pendingDeleteAction = {
            type: session.DELETE_ACTION_DELETE_RECEIPT,
            receiptId: receiptId,
            cardElement: cardElement
        };

        fillDeleteReceiptModal(
            'Удаление чека',
            'Вы уверены, что хотите удалить этот чек?',
            receipt
        );

        // Сбрасывать возможное предыдущее состояние кнопки
        if (bindings.deleteConfirmButton) {
            bindings.deleteConfirmButton.disabled = false;
            bindings.deleteConfirmButton.textContent = 'Удалить';
        }

        bindings.deleteBootstrapModal.show();
    }

    async function onConfirmDeleteReceipt(): Promise<void> {
        const action = session.pendingDeleteAction;
        if (!action || session.receiptWrites.has(action.receiptId)) { return; }
        const matchesList = session.listRequests.capture();
        const isCurrent = () => session.pendingDeleteAction === action && matchesList();
        const originalText = bindings.deleteConfirmButton.textContent;
        const command: ReceiptAccountCommand = action.type === session.DELETE_ACTION_DELETE_RECEIPT
            ? { kind: "delete", receiptId: action.receiptId }
            : { kind: "remove", receiptId: action.receiptId, accountId: action.accountId };
        await runReceiptAccountCommand({
            command,
            writes: session.receiptWrites,
            isCurrent,
            onStart: () => {
                bindings.deleteConfirmButton.disabled = true;
                bindings.deleteConfirmButton.textContent = "Удаление...";
            },
            send: snapshot => {
                if (snapshot.kind === "delete") { return deleteReceiptApi(snapshot.receiptId, session.forgeryToken); }
                if (snapshot.kind === "remove") { return removeReceiptFromAccountApi(snapshot.receiptId, snapshot.accountId, session.forgeryToken); }
                throw new Error("Неизвестный тип удаления чека.");
            },
            onSuccess: async () => {
                session.shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;
                session.preserveMoveReceiptAccountModalStateOnHide = false;
                if (action.type === session.DELETE_ACTION_REMOVE_FROM_ACCOUNT) { dependencies.closeMoveReceiptAccountModal(); }
                const page = session.currentReceiptList?.page ?? 1;
                bindings.deleteBootstrapModal.hide();
                await dependencies.loadReceiptList(page);
            },
            onError: message => alert(message),
            onFinally: () => {
                if (session.pendingDeleteAction !== action) { return; }
                bindings.deleteConfirmButton.disabled = false;
                bindings.deleteConfirmButton.textContent = originalText;
            }
        });
    }

    function fillDeleteReceiptModal(title: string, message: string, receipt: ReceiptDto): void {
        fillDeleteReceiptModalUi(
            {
                titleElement: bindings.deleteModalTitleElement,
                messageElement: bindings.deleteModalMessageElement,
                receiptInfoElement: bindings.deleteModalReceiptInfoElement
            },
            title,
            message,
            receipt,
            dependencies.formatCurrency
        );
    }

    function openRemoveReceiptFromAccountConfirmation(): void {
        session.confirmationContext.invalidate();
        if (!session.pendingReceiptAccountAction || !bindings.deleteBootstrapModal || !bindings.moveReceiptAccountBootstrapModal) {
            return;
        }

        if (!dependencies.canRemoveReceiptFromSelectedAccount()) {
            dependencies.updateMoveReceiptActionState();
            return;
        }

        const receipt = findReceiptByAccountReceiptIdCore(session.pageState.receipts, session.pendingReceiptAccountAction.receiptId, session.pendingReceiptAccountAction.sourceAccountId);
        if (!receipt) {
            alert('Чек не найден в текущем списке.');
            return;
        }

        session.pendingDeleteAction = {
            type: session.DELETE_ACTION_REMOVE_FROM_ACCOUNT,
            receiptId: session.pendingReceiptAccountAction.receiptId,
            cardElement: null,
            accountId: session.pendingReceiptAccountAction.sourceAccountId
        };

        fillDeleteReceiptModal(
            'Удаление чека из счёта',
            'Вы уверены, что хотите удалить этот чек из счёта?',
            receipt
        );

        if (bindings.deleteConfirmButton) {
            bindings.deleteConfirmButton.disabled = false;
            bindings.deleteConfirmButton.textContent = 'Удалить';
        }

        session.preserveMoveReceiptAccountModalStateOnHide = true;
        session.shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = true;
        bindings.moveReceiptAccountBootstrapModal.hide();
        bindings.deleteBootstrapModal.show();
    }

    return { onConfirmDeleteReceipt, openRemoveReceiptFromAccountConfirmation, openDeleteReceiptModal };
}
