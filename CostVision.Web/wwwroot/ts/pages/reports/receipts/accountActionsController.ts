import { runReceiptAccountCommand } from "../../../shared/receipts/workflow.js";
import { getWriteErrorMessage } from "../../../shared/requestCoordinator.js";

import { moveReceiptToAccountApi } from "./api.js";

import { ReceiptDto } from "./types.js";

import { findReceiptAccount as findReceiptAccountCore, findReceiptByAccountReceiptId as findReceiptByAccountReceiptIdCore, findReceiptById as findReceiptByIdCore, getAvailableTargetAccounts as getAvailableTargetAccountsCore, replaceReceiptAccountLink as replaceReceiptAccountLinkCore } from "./state/accountModel.js";

import type { ReceiptsSession } from "./state.js";
import type { ReceiptsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<ReceiptsSession,
        "confirmationContext" | "currentReceiptList" | "forgeryToken" | "listRequests" | "pageState" | "pendingReceiptAccountAction" | "receiptWrites">;
    bindings: Pick<ReceiptsBindings,
        "confirmMoveReceiptAccountButton" | "moveReceiptAccountAlertElement" | "moveReceiptAccountBootstrapModal" | "moveReceiptSourceAccountElement" | "moveReceiptTargetAccountSelect" | "removeReceiptFromAccountButton">;
    loadReceiptList: (page?: number) => Promise<void>;
}

export function createReceiptAccountActionsController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function openMoveReceiptAccountModal(receiptId: string, sourceAccountId: string): void {
        session.confirmationContext.invalidate();
        const receipt = sourceAccountId
            ? findReceiptByAccountReceiptIdCore(session.pageState.receipts, receiptId, sourceAccountId)
            : findReceiptByIdCore(session.pageState.receipts, receiptId);
        if (!receipt) {
            alert('Чек не найден в текущем списке.');
            return;
        }

        const sourceAccount = sourceAccountId ? findReceiptAccountCore(receipt, sourceAccountId, receiptId) : null;

        if (sourceAccount && !sourceAccount.canEditReceipt) {
            alert('Недостаточно прав для изменения чека в выбранном счёте.');
            return;
        }

        bindings.confirmMoveReceiptAccountButton.textContent = 'Перенести';
        session.pendingReceiptAccountAction = {
            receiptId: receiptId,
            sourceAccountId: sourceAccountId
        };

        bindings.moveReceiptSourceAccountElement.textContent = sourceAccount ? sourceAccount.name : 'Без счёта';
        bindings.moveReceiptSourceAccountElement.style.border = '2px solid ' + (sourceAccount ? sourceAccount.colorHex : '#dee2e6');
        hideMoveReceiptAccountAlert();
        renderMoveReceiptTargetOptions(receipt, sourceAccountId);
        updateMoveReceiptActionState();
        bindings.moveReceiptAccountBootstrapModal.show();
    }

    function renderMoveReceiptTargetOptions(receipt: ReceiptDto, sourceAccountId: string): void {
        const selectedValue = bindings.moveReceiptTargetAccountSelect.value || '';
        bindings.moveReceiptTargetAccountSelect.replaceChildren();

        const accounts = getAvailableTargetAccountsCore(session.pageState.availableAccounts, receipt, sourceAccountId);
        for (const account of accounts) {
            const option = document.createElement('option');
            option.value = account.id;
            option.textContent = account.name;
            option.disabled = account.isDisabled === true;
            option.setAttribute('data-reason', account.disabledReason || '');
            bindings.moveReceiptTargetAccountSelect.appendChild(option);
        }

        if (selectedValue) {
            const matchingOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(function (option) {
                return option.value === selectedValue && option.disabled === false;
            });

            bindings.moveReceiptTargetAccountSelect.value = matchingOption ? selectedValue : '';
        } else {
            const firstEnabledOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(function (option) {
                return option.value && option.disabled === false;
            });

            bindings.moveReceiptTargetAccountSelect.value = firstEnabledOption ? firstEnabledOption.value : '';
        }

        bindings.moveReceiptTargetAccountSelect.disabled = accounts.length === 0;
        bindings.moveReceiptTargetAccountSelect.onchange = onMoveReceiptTargetAccountChanged;
    }

    function onMoveReceiptTargetAccountChanged(): void {
        updateMoveReceiptActionState();
    }

    function updateMoveReceiptActionState(): void {
        const canRemove = canRemoveReceiptFromSelectedAccount();
        const selectedOption = getSelectedMoveReceiptTargetOption();
        const hasOtherAccounts = !!bindings.moveReceiptTargetAccountSelect && bindings.moveReceiptTargetAccountSelect.options.length > 0;
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

        const reason = selectedOption.getAttribute('data-reason') || '';
        if (selectedOption.disabled && reason) {
            showMoveReceiptAccountAlert(reason);
            return;
        }

        hideMoveReceiptAccountAlert();
    }

    function getUnavailableMoveReceiptReason(): string {
        if (bindings.moveReceiptTargetAccountSelect.options.length === 0) {
            return '';
        }

        const enabledOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(function (option) {
            return option.value && option.disabled === false;
        });

        if (enabledOption) {
            return '';
        }

        const disabledOption = Array.from<HTMLOptionElement>(bindings.moveReceiptTargetAccountSelect.options).find(function (option) {
            return option.value && option.disabled === true && option.getAttribute('data-reason');
        });

        return disabledOption ? disabledOption.getAttribute('data-reason') || '' : 'Нет доступных счетов для переноса этого чека.';
    }

    async function onConfirmMoveReceiptToAccount(): Promise<void> {
        if (!session.pendingReceiptAccountAction) {
            return;
        }

        const action = session.pendingReceiptAccountAction;
        if (session.receiptWrites.has(action.receiptId)) { return; }
        const matchesList = session.listRequests.capture();
        const isCurrent = () => session.pendingReceiptAccountAction === action && matchesList();
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
                return moveReceiptToAccountApi(command.receiptId, command.sourceAccountId, command.targetAccountId, session.forgeryToken);
            },
            onSuccess: async () => {
                replaceReceiptAccountLinkCore(session.pageState.receipts, session.pageState.availableAccounts, action.receiptId, action.sourceAccountId, targetAccountId);
                const page = session.currentReceiptList?.page ?? 1;
                closeMoveReceiptAccountModal();
                await dependencies.loadReceiptList(page);
            },
            onError: showMoveReceiptAccountAlert,
            onFinally: () => {
                if (session.pendingReceiptAccountAction !== action) { return; }
                bindings.confirmMoveReceiptAccountButton.textContent = originalText;
                updateMoveReceiptActionState();
            }
        });
    }

    function closeMoveReceiptAccountModal(): void {
        session.pendingReceiptAccountAction = null;
        hideMoveReceiptAccountAlert();

        bindings.moveReceiptTargetAccountSelect.replaceChildren();
        bindings.moveReceiptTargetAccountSelect.disabled = false;

        bindings.moveReceiptSourceAccountElement.textContent = '';
        bindings.moveReceiptSourceAccountElement.style.border = '';

        bindings.moveReceiptAccountBootstrapModal.hide();
    }

    function getSelectedMoveReceiptTargetOption(): HTMLOptionElement | null {
        if (!bindings.moveReceiptTargetAccountSelect) {
            return null;
        }

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

        const receipt = findReceiptByAccountReceiptIdCore(session.pageState.receipts, session.pendingReceiptAccountAction.receiptId, session.pendingReceiptAccountAction.sourceAccountId);
        if (!receipt) {
            return false;
        }

        const sourceAccount = findReceiptAccountCore(receipt, session.pendingReceiptAccountAction.sourceAccountId, session.pendingReceiptAccountAction.receiptId);
        if (!sourceAccount) {
            return false;
        }

        return sourceAccount.canEditReceipt === true;
    }

    function showMoveReceiptAccountAlert(message: string): void {
        bindings.moveReceiptAccountAlertElement.textContent = message || '';
        bindings.moveReceiptAccountAlertElement.classList.toggle('d-none', !message);
    }

    function hideMoveReceiptAccountAlert(): void {
        showMoveReceiptAccountAlert('');
    }

    return { hideMoveReceiptAccountAlert, onConfirmMoveReceiptToAccount, openMoveReceiptAccountModal, closeMoveReceiptAccountModal, canRemoveReceiptFromSelectedAccount, updateMoveReceiptActionState };
}
