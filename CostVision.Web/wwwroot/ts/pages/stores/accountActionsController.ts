import { runReceiptAccountCommand } from "../../shared/receipts/workflow.js";
import { getWriteErrorMessage } from "../../shared/requestCoordinator.js";

import { fillDeleteReceiptModal as fillDeleteReceiptModalUi } from "../reports/receipts/modals/deleteReceiptModal.js";

import { findReceiptAccount, findReceiptByAccountReceiptId, findReceiptById, getAvailableTargetAccounts, removeReceiptAccountLink, replaceReceiptAccountLink } from "../reports/receipts/state/accountModel.js";

import type { ReceiptDto } from "../reports/receipts/types.js";
import { moveStoreReceiptToAccount, removeStoreReceiptFromAccount } from "./api.js";

import type { StoresSession } from "./state.js";
import type { StoresBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<StoresSession,
        "accountActionContext" | "availableAccounts" | "confirmationContext" | "pendingReceiptAccountAction" | "preserveMoveReceiptAccountModalStateOnHide" | "receiptWrites" | "selectedStoreReceipts" | "selectedStoreReceiptsPage" | "shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation" | "storeContext" | "storeReceiptRequests">;
    bindings: Pick<StoresBindings,
        "confirmMoveReceiptAccountButton" | "confirmRemoveReceiptAccountButton" | "moveReceiptAccountAlertElement" | "moveReceiptAccountModal" | "moveReceiptSourceAccountElement" | "moveReceiptTargetAccountSelect" | "removeReceiptAccountModal" | "removeReceiptAccountModalMessageElement" | "removeReceiptAccountModalReceiptInfoElement" | "removeReceiptAccountModalTitleElement" | "removeReceiptFromAccountButton">;
    loadAvailableAccounts: () => Promise<boolean>;
    showStoreReceiptsAlert: (message: string) => void;
    loadSelectedStoreReceiptsPage: (page: number) => Promise<void>;
    formatCurrency: (value: number) => string;
}

export function createStoreReceiptAccountActionsController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    async function openMoveReceiptAccountModal(receiptId: string, sourceAccountId: string): Promise<void> {
        session.confirmationContext.invalidate();
        const request = session.accountActionContext.begin({ receiptId, sourceAccountId });
        const matchesStore = session.storeContext.capture();
        if (session.availableAccounts.length === 0) {
            if (!await dependencies.loadAvailableAccounts()) { return; }
            if (!request.isCurrent() || !matchesStore()) { return; }
        }

        const receipt = sourceAccountId
            ? findReceiptByAccountReceiptId(session.selectedStoreReceipts, receiptId, sourceAccountId)
            : findReceiptById(session.selectedStoreReceipts, receiptId);
        if (!receipt) {
            dependencies.showStoreReceiptsAlert("Чек не найден в текущем списке.");
            return;
        }

        const sourceAccount = sourceAccountId ? findReceiptAccount(receipt, sourceAccountId, receiptId) : null;
        if (sourceAccountId && !sourceAccount) {
            dependencies.showStoreReceiptsAlert("Связь со счётом не найдена.");
            return;
        }

        if (sourceAccount && !sourceAccount.canEditReceipt) {
            dependencies.showStoreReceiptsAlert("Недостаточно прав для изменения чека в выбранном счёте.");
            return;
        }

        bindings.confirmMoveReceiptAccountButton.textContent = "Перенести";
        session.pendingReceiptAccountAction = {
            receiptId: receiptId,
            sourceAccountId: sourceAccountId
        };

        bindings.moveReceiptSourceAccountElement.textContent = sourceAccount ? sourceAccount.name : "Без счёта";
        bindings.moveReceiptSourceAccountElement.style.border = "2px solid " + (sourceAccount ? sourceAccount.colorHex : "#dee2e6");
        hideMoveReceiptAccountAlert();
        renderMoveReceiptTargetOptions(receipt, sourceAccountId);
        updateMoveReceiptActionState();
        bindings.moveReceiptAccountModal.show();
    }

    function renderMoveReceiptTargetOptions(receipt: ReceiptDto, sourceAccountId: string): void {
        const selectedValue = bindings.moveReceiptTargetAccountSelect.value || "";
        bindings.moveReceiptTargetAccountSelect.replaceChildren();

        const accounts = getAvailableTargetAccounts(session.availableAccounts, receipt, sourceAccountId);
        for (const account of accounts) {
            const option = document.createElement("option");
            option.value = account.id;
            option.textContent = account.name;
            option.disabled = account.isDisabled === true;
            option.setAttribute("data-reason", account.disabledReason || "");
            bindings.moveReceiptTargetAccountSelect.appendChild(option);
        }

        if (selectedValue) {
            const matchingOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(option => option.value === selectedValue && option.disabled === false);
            bindings.moveReceiptTargetAccountSelect.value = matchingOption ? selectedValue : "";
        } else {
            const firstEnabledOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(option => option.value && option.disabled === false);
            bindings.moveReceiptTargetAccountSelect.value = firstEnabledOption ? firstEnabledOption.value : "";
        }

        bindings.moveReceiptTargetAccountSelect.disabled = accounts.length === 0;
        bindings.moveReceiptTargetAccountSelect.onchange = updateMoveReceiptActionState;
    }

    function updateMoveReceiptActionState(): void {
        const canRemove = canRemoveReceiptFromSelectedAccount();
        const selectedOption = getSelectedMoveReceiptTargetOption();
        const hasOtherAccounts = bindings.moveReceiptTargetAccountSelect.options.length > 0;
        const canMove = hasOtherAccounts && !!selectedOption && !selectedOption.disabled;

        bindings.removeReceiptFromAccountButton.disabled = !canRemove;
        bindings.confirmMoveReceiptAccountButton.disabled = !canMove;

        if (!hasOtherAccounts) {
            hideMoveReceiptAccountAlert();
            return;
        }

        if (!selectedOption) {
            const unavailableReason = getUnavailableMoveReceiptReason();
            if (unavailableReason) {
                showMoveReceiptAccountAlert(unavailableReason);
                return;
            }

            hideMoveReceiptAccountAlert();
            return;
        }

        const reason = selectedOption.getAttribute("data-reason") || "";
        if (selectedOption.disabled && reason) {
            showMoveReceiptAccountAlert(reason);
            return;
        }

        hideMoveReceiptAccountAlert();
    }

    function getUnavailableMoveReceiptReason(): string {
        if (bindings.moveReceiptTargetAccountSelect.options.length === 0) {
            return "";
        }

        const enabledOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(option => option.value && option.disabled === false);
        if (enabledOption) {
            return "";
        }

        const disabledOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(option => option.value && option.disabled === true && option.getAttribute("data-reason"));
        return disabledOption ? disabledOption.getAttribute("data-reason") || "" : "Нет доступных счетов для переноса этого чека.";
    }

    async function onConfirmMoveReceiptToAccount(): Promise<void> {
        if (!session.pendingReceiptAccountAction) {
            return;
        }

        const action = session.pendingReceiptAccountAction;
        if (session.receiptWrites.has(action.receiptId)) { return; }
        const matchesStore = session.storeContext.capture();
        const matchesPage = session.storeReceiptRequests.capture();
        const isCurrent = () => session.pendingReceiptAccountAction === action && matchesStore() && matchesPage();
        const selectedOption = getSelectedMoveReceiptTargetOption();
        if (!selectedOption || selectedOption.disabled) {
            updateMoveReceiptActionState();
            return;
        }

        const targetAccountId = selectedOption.value;
        const originalText = bindings.confirmMoveReceiptAccountButton.textContent;
        await runReceiptAccountCommand({
            command: { kind: "move", receiptId: action.receiptId, sourceAccountId: action.sourceAccountId, targetAccountId },
            writes: session.receiptWrites,
            isCurrent,
            onStart: () => {
                bindings.confirmMoveReceiptAccountButton.disabled = true;
                bindings.confirmMoveReceiptAccountButton.textContent = "Перенос...";
                bindings.removeReceiptFromAccountButton.disabled = true;
            },
            send: command => {
                if (command.kind !== "move") { throw new Error("Неизвестная команда чека."); }
                return moveStoreReceiptToAccount(command.receiptId, command.sourceAccountId, command.targetAccountId);
            },
            onSuccess: async () => {
                replaceReceiptAccountLink(session.selectedStoreReceipts, session.availableAccounts, action.receiptId, action.sourceAccountId, targetAccountId);
                closeMoveReceiptAccountModal();
                await dependencies.loadSelectedStoreReceiptsPage(session.selectedStoreReceiptsPage);
            },
            onError: showMoveReceiptAccountAlert,
            onFinally: () => {
                if (session.pendingReceiptAccountAction !== action) { return; }
                bindings.confirmMoveReceiptAccountButton.textContent = originalText;
                updateMoveReceiptActionState();
            }
        });
    }

    function openRemoveReceiptFromAccountConfirmation(): void {
        session.confirmationContext.invalidate();
        if (!session.pendingReceiptAccountAction) {
            return;
        }

        if (!canRemoveReceiptFromSelectedAccount()) {
            updateMoveReceiptActionState();
            return;
        }

        const receipt = findReceiptByAccountReceiptId(session.selectedStoreReceipts, session.pendingReceiptAccountAction.receiptId, session.pendingReceiptAccountAction.sourceAccountId);
        if (!receipt) {
            showMoveReceiptAccountAlert("Чек не найден в текущем списке.");
            return;
        }

        fillDeleteReceiptModalUi(
            {
                titleElement: bindings.removeReceiptAccountModalTitleElement,
                messageElement: bindings.removeReceiptAccountModalMessageElement,
                receiptInfoElement: bindings.removeReceiptAccountModalReceiptInfoElement
            },
            "Удаление чека из счёта",
            "Вы уверены, что хотите удалить этот чек из счёта?",
            receipt,
            dependencies.formatCurrency
        );

        bindings.confirmRemoveReceiptAccountButton.disabled = false;
        bindings.confirmRemoveReceiptAccountButton.textContent = "Удалить";
        session.preserveMoveReceiptAccountModalStateOnHide = true;
        session.shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = true;
        bindings.moveReceiptAccountModal.hide();
        bindings.removeReceiptAccountModal.show();
    }

    async function onConfirmRemoveReceiptFromAccount(): Promise<void> {
        if (!session.pendingReceiptAccountAction) {
            return;
        }

        const action = session.pendingReceiptAccountAction;
        if (session.receiptWrites.has(action.receiptId)) { return; }
        const matchesStore = session.storeContext.capture();
        const matchesPage = session.storeReceiptRequests.capture();
        const isCurrent = () => session.pendingReceiptAccountAction === action && matchesStore() && matchesPage();
        const originalText = bindings.confirmRemoveReceiptAccountButton.textContent;
        await runReceiptAccountCommand({
            command: { kind: "remove", receiptId: action.receiptId, accountId: action.sourceAccountId },
            writes: session.receiptWrites,
            isCurrent,
            onStart: () => {
                bindings.confirmRemoveReceiptAccountButton.disabled = true;
                bindings.confirmRemoveReceiptAccountButton.textContent = "Удаление...";
                session.shouldRestoreMoveReceiptAccountModalAfterRemoveConfirmation = false;
                session.preserveMoveReceiptAccountModalStateOnHide = false;
            },
            send: command => {
                if (command.kind !== "remove") { throw new Error("Неизвестная команда чека."); }
                return removeStoreReceiptFromAccount(command.receiptId, command.accountId);
            },
            onSuccess: async () => {
                session.selectedStoreReceipts = removeReceiptAccountLink(session.selectedStoreReceipts, action.receiptId, action.sourceAccountId);
                closeMoveReceiptAccountModal();
                bindings.removeReceiptAccountModal.hide();
                await dependencies.loadSelectedStoreReceiptsPage(session.selectedStoreReceiptsPage);
            },
            onError: dependencies.showStoreReceiptsAlert,
            onFinally: () => {
                if (session.pendingReceiptAccountAction !== action) { return; }
                bindings.confirmRemoveReceiptAccountButton.disabled = false;
                bindings.confirmRemoveReceiptAccountButton.textContent = originalText;
            }
        });
    }

    function getSelectedMoveReceiptTargetOption(): HTMLOptionElement | null {
        const selectedIndex = bindings.moveReceiptTargetAccountSelect.selectedIndex;
        if (selectedIndex < 0) {
            return null;
        }

        const option = bindings.moveReceiptTargetAccountSelect.options[selectedIndex];
        if (!option || !option.value) {
            return null;
        }

        return option;
    }

    function canRemoveReceiptFromSelectedAccount(): boolean {
        if (!session.pendingReceiptAccountAction) {
            return false;
        }

        const receipt = findReceiptByAccountReceiptId(session.selectedStoreReceipts, session.pendingReceiptAccountAction.receiptId, session.pendingReceiptAccountAction.sourceAccountId);
        if (!receipt) {
            return false;
        }

        const sourceAccount = findReceiptAccount(receipt, session.pendingReceiptAccountAction.sourceAccountId, session.pendingReceiptAccountAction.receiptId);
        return sourceAccount?.canEditReceipt === true;
    }

    function closeMoveReceiptAccountModal(): void {
        clearMoveReceiptAccountState();
        bindings.moveReceiptAccountModal.hide();
    }

    function clearMoveReceiptAccountState(): void {
        session.accountActionContext.invalidate();
        session.pendingReceiptAccountAction = null;
        hideMoveReceiptAccountAlert();
        bindings.moveReceiptTargetAccountSelect.replaceChildren();
        bindings.moveReceiptTargetAccountSelect.disabled = false;
        bindings.moveReceiptSourceAccountElement.textContent = "";
        bindings.moveReceiptSourceAccountElement.style.border = "";
    }

    function showMoveReceiptAccountAlert(message: string): void {
        bindings.moveReceiptAccountAlertElement.textContent = message || "";
        bindings.moveReceiptAccountAlertElement.classList.toggle("d-none", !message);
    }

    function hideMoveReceiptAccountAlert(): void {
        showMoveReceiptAccountAlert("");
    }

    return { clearMoveReceiptAccountState, onConfirmMoveReceiptToAccount, openRemoveReceiptFromAccountConfirmation, onConfirmRemoveReceiptFromAccount, openMoveReceiptAccountModal };
}
