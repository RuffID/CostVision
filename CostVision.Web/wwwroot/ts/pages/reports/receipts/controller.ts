import { getPageFromQuery, updatePageQuery } from "../../../shared/preferences.js";

import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { requireElementById, requireInputById, requireSelectById } from "../../../shared/dom.js";
import { formatMoneyRub, formatRuNumber } from "../../../shared/formatters.js";
import { createBootstrapModal } from "../../../shared/bootstrap.js";
import { renderHelpTooltip } from "../../../shared/helpTooltip.js";

import { createReceiptsSession } from "./state.js";

import { initReceiptMoneyMovementsModal } from "./modals/receiptMoneyMovementsModal.js";
import { ReceiptsBindings } from "./bindings.js";
import { createReceiptFiltersController } from "./filtersController.js";
import { createReceiptListController } from "./listController.js";
import { createReportReceiptCommands } from "./receiptCommands.js";
import { createReceiptConfirmationController } from "./confirmationController.js";
import { createReceiptAccountActionsController } from "./accountActionsController.js";

export function createReceiptsPage() {
    const session = createReceiptsSession();
    const bindings = new ReceiptsBindings();
    window.addEventListener("pagehide", () => {
        session.listRequests.invalidate();
        session.accountRequests.invalidate();
        session.detailRequests.invalidate();
        session.confirmationContext.invalidate();
        if (session.searchDebounceTimerId !== null) {
            window.clearTimeout(session.searchDebounceTimerId);
            session.searchDebounceTimerId = null;
        }
    });
    const filters = createReceiptFiltersController({ session, bindings,
        loadReceiptList: (...args) => list.loadReceiptList(...args),
    });
    const list = createReceiptListController({ session, bindings,
        buildDateRangeQuery: (...args) => filters.buildDateRangeQuery(...args),
        formatCurrency: (...args) => formatCurrency(...args),
        updatePageQuery: (...args) => updatePageQuery(...args),
        getErrorMessage: (...args) => getErrorMessage(...args),
    });
    const receiptCommands = createReportReceiptCommands({ session, bindings,
        formatNumber: (...args) => formatNumber(...args),
        formatCurrency: (...args) => formatCurrency(...args),
    });
    const confirmation = createReceiptConfirmationController({ session, bindings,
        closeMoveReceiptAccountModal: (...args) => accountActions.closeMoveReceiptAccountModal(...args),
        loadReceiptList: (...args) => list.loadReceiptList(...args),
        formatCurrency: (...args) => formatCurrency(...args),
        canRemoveReceiptFromSelectedAccount: (...args) => accountActions.canRemoveReceiptFromSelectedAccount(...args),
        updateMoveReceiptActionState: (...args) => accountActions.updateMoveReceiptActionState(...args),
    });
    const accountActions = createReceiptAccountActionsController({ session, bindings,
        loadReceiptList: (...args) => list.loadReceiptList(...args),
    });

    async function initListOfChecksPage(): Promise<void> {
        bindings.listContainer = requireElementById<HTMLElement>('receipt-list');
        bindings.modalElement = requireElementById<HTMLElement>('receiptDetailsModal');
        bindings.detailsList = requireElementById<HTMLElement>('receipt-details-list');
        bindings.modalHeader = requireElementById<HTMLElement>('receipt-details-header');
        bindings.modalTotal = requireElementById<HTMLElement>('receipt-details-total');
        bindings.moveReceiptAccountModalElement = requireElementById<HTMLElement>('moveReceiptAccountModal');
        bindings.moveReceiptSourceAccountElement = requireElementById<HTMLElement>('moveReceiptSourceAccount');
        bindings.moveReceiptTargetAccountSelect = requireSelectById('moveReceiptTargetAccount');
        bindings.moveReceiptAccountAlertElement = requireElementById<HTMLElement>('moveReceiptAccountAlert');
        bindings.confirmMoveReceiptAccountButton = requireElementById<HTMLButtonElement>('confirmMoveReceiptAccountBtn', HTMLButtonElement);
        bindings.removeReceiptFromAccountButton = requireElementById<HTMLButtonElement>('removeReceiptFromAccountBtn', HTMLButtonElement);
        bindings.deleteModalElement = requireElementById<HTMLElement>('deleteReceiptModal');
        bindings.deleteConfirmButton = requireElementById<HTMLButtonElement>('confirmDeleteReceiptBtn', HTMLButtonElement);
        bindings.deleteModalTitleElement = requireElementById<HTMLElement>('deleteReceiptModalTitle');
        bindings.deleteModalMessageElement = requireElementById<HTMLElement>('deleteReceiptModalMessage');
        bindings.deleteModalReceiptInfoElement = requireElementById<HTMLElement>('deleteReceiptModalReceiptInfo');
        bindings.applyFilterButton = requireElementById<HTMLButtonElement>('applyFilter', HTMLButtonElement);
        bindings.dateFromInput = requireInputById('dateFrom');
        bindings.dateToInput = requireInputById('dateTo');
        bindings.receiptPeriodPresetSelect = requireSelectById('receiptPeriodPreset');
        bindings.receiptsCountElement = requireElementById<HTMLElement>('receiptsCount');
        bindings.receiptsSumElement = requireElementById<HTMLElement>('receiptsSum');
        bindings.receiptPageSizeSelect = requireSelectById('receiptsPageSize');
        bindings.receiptsPaginationElement = requireElementById<HTMLElement>('receiptsPagination');
        session.forgeryToken = getRequestVerificationToken();

        if (bindings.modalElement) {
            bindings.bootstrapModal = createBootstrapModal(bindings.modalElement);

            // Убирать фокус из модалки при закрытии, чтобы избежать предупреждения aria-hidden
            bindings.modalElement.addEventListener('hide.bs.modal', function () {
                session.detailRequests.invalidate();
                if (document.activeElement instanceof HTMLElement && bindings.modalElement.contains(document.activeElement)) {
                    document.activeElement.blur();
                }
            });
        }

        if (bindings.applyFilterButton) {
            bindings.applyFilterButton.addEventListener('click', function () {
                void list.loadReceiptList(1);
            });
        }

        if (bindings.moveReceiptAccountModalElement) {
            bindings.moveReceiptAccountBootstrapModal = createBootstrapModal(bindings.moveReceiptAccountModalElement);

            bindings.moveReceiptAccountModalElement.addEventListener('hide.bs.modal', function () {
                if (document.activeElement instanceof HTMLElement && bindings.moveReceiptAccountModalElement.contains(document.activeElement)) {
                    document.activeElement.blur();
                }
            });

            bindings.moveReceiptAccountModalElement.addEventListener('hide.bs.modal', function () {
                if (session.preserveMoveReceiptAccountModalStateOnHide) {
                    return;
                }

                session.pendingReceiptAccountAction = null;
                accountActions.hideMoveReceiptAccountAlert();

                if (bindings.moveReceiptTargetAccountSelect) {
                    bindings.moveReceiptTargetAccountSelect.replaceChildren();
                    bindings.moveReceiptTargetAccountSelect.disabled = false;
                }

                if (bindings.moveReceiptSourceAccountElement) {
                    bindings.moveReceiptSourceAccountElement.textContent = '';
                    bindings.moveReceiptSourceAccountElement.style.border = '';
                }
            });
        }

        if (bindings.listContainer) {
            bindings.listContainer.addEventListener('click', onReceiptListClick);
        }

        bindings.receiptMoneyMovementsModal = initReceiptMoneyMovementsModal({
            getReceipts: function () {
                return session.pageState.receipts;
            },
            getForgeryToken: function () {
                return session.forgeryToken;
            },
            reloadReceiptList: list.loadReceiptList,
            formatCurrency: formatCurrency
        });

        if (bindings.deleteModalElement) {
            bindings.deleteBootstrapModal = createBootstrapModal(bindings.deleteModalElement);

            // Убирать фокус из модалки при закрытии, чтобы избежать предупреждения aria-hidden
            bindings.deleteModalElement.addEventListener('hide.bs.modal', function () {
                if (document.activeElement instanceof HTMLElement && bindings.deleteModalElement.contains(document.activeElement)) {
                    document.activeElement.blur();
                }
            });

            bindings.deleteModalElement.addEventListener('hide.bs.modal', function () {
                const isCurrent = session.confirmationContext.capture();
                const matchesList = session.listRequests.capture();
                const action = session.pendingReceiptAccountAction;
                const restore = session.shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation;
                session.pendingDeleteAction = null;
                session.pendingDeleteReceiptId = null;
                session.pendingDeleteCardElement = null;
                bindings.deleteModalElement.addEventListener('hidden.bs.modal', function () {
                    if (!isCurrent() || !matchesList() || !restore || session.pendingReceiptAccountAction !== action) { return; }
                    session.shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;
                    session.preserveMoveReceiptAccountModalStateOnHide = false;
                    bindings.moveReceiptAccountBootstrapModal.show();
                }, { once: true });
            });
        }

        if (bindings.deleteConfirmButton) {
            bindings.deleteConfirmButton.addEventListener('click', confirmation.onConfirmDeleteReceipt);
        }

        if (bindings.confirmMoveReceiptAccountButton) {
            bindings.confirmMoveReceiptAccountButton.addEventListener('click', accountActions.onConfirmMoveReceiptToAccount);
        }

        if (bindings.removeReceiptFromAccountButton) {
            bindings.removeReceiptFromAccountButton.addEventListener('click', confirmation.openRemoveReceiptFromAccountConfirmation);
        }

        bindings.receiptSearchInput = requireInputById('receiptSearch');
        bindings.receiptSearchModeSelect = requireSelectById('receiptSearchMode');
        bindings.receiptAccountFilterSelect = requireSelectById('receiptAccountFilter');
        bindings.receiptAccountFilterError = requireElementById<HTMLElement>('receiptAccountFilterError');
        bindings.receiptOperationFilterSelect = requireSelectById('receiptOperationFilter');
        renderHelpTooltip(requireElementById<HTMLElement>('receiptOperationFilterHelp'), {
            title: 'Связь с операциями',
            text: [
                'Все чеки — без фильтра.',
                'Без операций — чеки без привязанных операций.',
                'С операциями — чеки с одной или несколькими операциями.',
                'Расхождение суммы — сумма привязанных операций отличается от суммы чека.'
            ]
        });

        if (bindings.receiptSearchInput) {
            bindings.receiptSearchInput.addEventListener('input', filters.onSearchChanged);
        }

        if (bindings.receiptSearchModeSelect) {
            bindings.receiptSearchModeSelect.addEventListener('change', filters.onSearchChanged);
        }

        if (bindings.receiptAccountFilterSelect) {
            bindings.receiptAccountFilterSelect.addEventListener('change', filters.onSearchChanged);
        }

        if (bindings.receiptOperationFilterSelect) {
            bindings.receiptOperationFilterSelect.addEventListener('change', filters.onSearchChanged);
        }
        if (bindings.receiptPeriodPresetSelect) {
            bindings.receiptPeriodPresetSelect.addEventListener('change', filters.onReceiptPeriodPresetChanged);
        }

        bindings.receiptPageSizeSelect.addEventListener('change', () => void list.loadReceiptList(1));
        bindings.dateFromInput.addEventListener('change', filters.updatePeriodPresetForManualDateRange);
        bindings.dateToInput.addEventListener('change', filters.updatePeriodPresetForManualDateRange);
        filters.setCurrentMonthPeriod();

        if (!await list.loadAvailableAccountsAsync()) { return; }
        await list.loadReceiptList(getPageFromQuery());
    }

    function onReceiptListClick(event: MouseEvent): void {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const openButton = target.closest<HTMLButtonElement>('[data-action="open"]');
        const refreshButton = target.closest<HTMLButtonElement>('[data-action="refresh"]');
        const deleteButton = target.closest<HTMLButtonElement>('[data-action="delete"]');
        const accountButton = target.closest<HTMLButtonElement>('[data-action="edit-account-link"]');
        const assignAccountButton = target.closest<HTMLButtonElement>('[data-action="assign-account-link"]');
        const moneyMovementsButton = target.closest<HTMLButtonElement>('[data-action="open-money-movements"]');

        if (moneyMovementsButton) {
            const receiptId = moneyMovementsButton.getAttribute('data-receipt-id');
            if (!receiptId) {
                return;
            }

            void bindings.receiptMoneyMovementsModal.open(receiptId);
            return;
        }

        if (openButton) {
            const card = openButton.closest<HTMLElement>('.card');
            if (!card) {
                return;
            }
            const receiptId = card.getAttribute('data-receipt-id');
            if (!receiptId) {
                return;
            }
            receiptCommands.openReceipt(receiptId);
            return;
        }

        if (refreshButton) {
            const card = refreshButton.closest<HTMLElement>('.card');
            if (!card) {
                return;
            }
            const receiptId = card.getAttribute('data-receipt-id');
            if (!receiptId) {
                return;
            }
            receiptCommands.refreshReceipt(receiptId, card, refreshButton);
        }

        if (deleteButton) {
            const card = deleteButton.closest<HTMLElement>('.card');
            if (!card) {
                return;
            }
            const receiptId = card.getAttribute('data-receipt-id');
            if (!receiptId) {
                return;
            }
            confirmation.openDeleteReceiptModal(receiptId, card);
            return;
        }

        if (accountButton) {
            if (accountButton.disabled) {
                return;
            }

            const receiptId = accountButton.getAttribute('data-account-receipt-id');
            const accountId = accountButton.getAttribute('data-account-id');
            if (!receiptId || !accountId) {
                return;
            }

            accountActions.openMoveReceiptAccountModal(receiptId, accountId);
            return;
        }

        if (assignAccountButton) {
            const receiptId = assignAccountButton.closest<HTMLElement>('.card')?.getAttribute('data-receipt-id');
            if (!receiptId) {
                return;
            }

            accountActions.openMoveReceiptAccountModal(receiptId, '');
        }
    }

    function formatNumber(value: number): string {
        return formatRuNumber(value);
    }

    function formatCurrency(value: number): string {
        return formatMoneyRub(value);
    }

    

    

    function getErrorMessage(error: unknown, fallback = 'Не удалось выполнить операцию.'): string {
        return error instanceof Error ? error.message : fallback;
    }

    async function init(): Promise<void> {
        try {
            await initListOfChecksPage();
        } catch (error) {
            console.error(error);
            alert(error instanceof Error ? error.message : 'Ошибка при инициализации страницы чеков.');
        }
    }

    return { init };
}
