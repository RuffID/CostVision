import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { clearElement, requireElementById, requireInputById, requireSelectById } from "../../../shared/dom.js";
import { formatMoneyRub, formatRuNumber, normalizeSingleLineTextValue } from "../../../shared/formatters.js";
import { BootstrapModal, createBootstrapModal } from "../../../shared/bootstrap.js";
import { deleteReceiptApi, linkReceiptMoneyMovementApi, loadAvailableAccountsApi, loadLinkedMoneyMovementsApi, loadMoneyMovementCandidatesApi, loadReceiptsApi, moveReceiptToAccountApi, openReceiptApi, refreshReceiptApi, removeReceiptFromAccountApi, unlinkReceiptMoneyMovementApi } from "./api.js";
import { applyReceiptFilters as applyReceiptFiltersCore } from "./filters.js";
import { removeReceiptFromCache } from "./state.js";
import { updateReceiptsSummary } from "./render.js";
import { formatDateForQuery, getDateRangeByPeriodPreset } from "./dateRange.js";
import { AvailableAccountDto, GetReceiptMoneyMovementCandidatesRequest, MoveReceiptAccountAction, PendingDeleteAction, ReceiptDto, ReceiptMoneyMovementDto } from "./types.js";
import { hideReceiptAccountFilterError as hideAccountFilterError, renderReceiptAccountFilterError as renderAccountFilterError, renderReceiptAccountFilterLoading as renderAccountFilterLoading, renderReceiptAccountFilterOptions as renderAccountFilterOptions } from "./ui/accountFilter.js";
import { buildReceiptCard as buildReceiptCardElement, updateCardFromDto as updateReceiptCardFromDto } from "./ui/receiptCards.js";
import { renderReceiptDetails as renderReceiptDetailsModal } from "./ui/receiptDetailsModal.js";
import { findReceiptAccount as findReceiptAccountCore, findReceiptByAccountReceiptId as findReceiptByAccountReceiptIdCore, findReceiptById as findReceiptByIdCore, getAvailableTargetAccounts as getAvailableTargetAccountsCore, normalizeAvailableAccount as normalizeAvailableAccountCore, removeReceiptAccountLink as removeReceiptAccountLinkCore, replaceReceiptAccountLink as replaceReceiptAccountLinkCore } from "./state/accountModel.js";
import { fillDeleteReceiptModal as fillDeleteReceiptModalUi } from "./modals/deleteReceiptModal.js";

// Р“Р»РѕР±Р°Р»СЊРЅС‹Рµ РїРµСЂРµРјРµРЅРЅС‹Рµ РґР»СЏ СЃС‚СЂР°РЅРёС†С‹ СЃРїРёСЃРєР° С‡РµРєРѕРІ
let listContainer: HTMLElement;
let modalElement: HTMLElement;
let detailsList: HTMLElement;
let modalHeader: HTMLElement;
let modalTotal: HTMLElement;
let bootstrapModal: BootstrapModal;
let moveReceiptAccountModalElement: HTMLElement;
let moveReceiptAccountBootstrapModal: BootstrapModal;
let moveReceiptSourceAccountElement: HTMLElement;
let moveReceiptTargetAccountSelect: HTMLSelectElement;
let moveReceiptAccountAlertElement: HTMLElement;
let confirmMoveReceiptAccountButton: HTMLButtonElement;
let removeReceiptFromAccountButton: HTMLButtonElement;
let forgeryToken: string | null;
let dateFromInput: HTMLInputElement;
let dateToInput: HTMLInputElement;
let applyFilterButton: HTMLButtonElement;
let receiptPeriodPresetSelect: HTMLSelectElement;
let receiptsCountElement: HTMLElement;
let receiptsSumElement: HTMLElement;

// Р”Р»СЏ СѓРґР°Р»РµРЅРёСЏ
let deleteModalElement: HTMLElement;
let deleteBootstrapModal: BootstrapModal;
let deleteConfirmButton: HTMLButtonElement;
let deleteModalTitleElement: HTMLElement;
let deleteModalMessageElement: HTMLElement;
let deleteModalReceiptInfoElement: HTMLElement;
let pendingDeleteReceiptId: string | null = null;
let pendingDeleteCardElement: HTMLElement | null = null;
let pendingDeleteAction: PendingDeleteAction | null = null;
let preserveMoveReceiptAccountModalStateOnHide = false;
let shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;

let receiptSearchInput: HTMLInputElement;
let receiptSearchModeSelect: HTMLSelectElement;
let receiptAccountFilterSelect: HTMLSelectElement;
let receiptAccountFilterError: HTMLElement;
let receiptOperationFilterSelect: HTMLSelectElement;
let receiptMoneyMovementsModalElement: HTMLElement;
let receiptMoneyMovementsBootstrapModal: BootstrapModal;
let receiptMoneyMovementsAlert: HTMLElement;
let receiptMoneyMovementsInfo: HTMLElement;
let receiptLinkedMoneyMovements: HTMLElement;
let receiptMoneyMovementCandidates: HTMLElement;
let receiptMoneyMovementsDateFrom: HTMLInputElement;
let receiptMoneyMovementsDateTo: HTMLInputElement;
let receiptMoneyMovementsAmountTolerance: HTMLInputElement;
let receiptMoneyMovementsTimeWindowHours: HTMLInputElement;
let receiptMoneyMovementsUseTimeWindow: HTMLInputElement;
let receiptMoneyMovementsUseAmountFilter: HTMLInputElement;
let receiptMoneyMovementsExcludeLinked: HTMLInputElement;
let receiptMoneyMovementsReloadCandidatesButton: HTMLButtonElement;

let receiptsCache: ReceiptDto[] = [];
let availableAccountsCache: AvailableAccountDto[] = [];
let searchDebounceTimerId: number | null = null;
let pendingReceiptAccountAction: MoveReceiptAccountAction | null = null;
let selectedMoneyMovementsReceiptId: string | null = null;

const DELETE_ACTION_DELETE_RECEIPT = 'delete-receipt';
const DELETE_ACTION_REMOVE_FROM_ACCOUNT = 'remove-from-account';

// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РїРѕСЃР»Рµ Р·Р°РіСЂСѓР·РєРё DOM
document.addEventListener('DOMContentLoaded', function () {
    initListOfChecksPage().catch(function (error) {
        console.error(error);
        alert(error && error.message ? error.message : 'РћС€РёР±РєР° РїСЂРё РёРЅРёС†РёР°Р»РёР·Р°С†РёРё СЃС‚СЂР°РЅРёС†С‹ С‡РµРєРѕРІ.');
    });
});

// РРЅРёС†РёР°Р»РёР·РёСЂРѕРІР°С‚СЊ СЃС‚СЂР°РЅРёС†Сѓ СЃРїРёСЃРєР° С‡РµРєРѕРІ
async function initListOfChecksPage(): Promise<void> {
    listContainer = requireElementById<HTMLElement>('receipt-list');
    modalElement = requireElementById<HTMLElement>('receiptDetailsModal');
    detailsList = requireElementById<HTMLElement>('receipt-details-list');
    modalHeader = requireElementById<HTMLElement>('receipt-details-header');
    modalTotal = requireElementById<HTMLElement>('receipt-details-total');
    moveReceiptAccountModalElement = requireElementById<HTMLElement>('moveReceiptAccountModal');
    moveReceiptSourceAccountElement = requireElementById<HTMLElement>('moveReceiptSourceAccount');
    moveReceiptTargetAccountSelect = requireSelectById('moveReceiptTargetAccount');
    moveReceiptAccountAlertElement = requireElementById<HTMLElement>('moveReceiptAccountAlert');
    confirmMoveReceiptAccountButton = requireElementById<HTMLButtonElement>('confirmMoveReceiptAccountBtn');
    removeReceiptFromAccountButton = requireElementById<HTMLButtonElement>('removeReceiptFromAccountBtn');
    deleteModalElement = requireElementById<HTMLElement>('deleteReceiptModal');
    deleteConfirmButton = requireElementById<HTMLButtonElement>('confirmDeleteReceiptBtn');
    deleteModalTitleElement = requireElementById<HTMLElement>('deleteReceiptModalTitle');
    deleteModalMessageElement = requireElementById<HTMLElement>('deleteReceiptModalMessage');
    deleteModalReceiptInfoElement = requireElementById<HTMLElement>('deleteReceiptModalReceiptInfo');
    applyFilterButton = requireElementById<HTMLButtonElement>('applyFilter');
    dateFromInput = requireInputById('dateFrom');
    dateToInput = requireInputById('dateTo');
    receiptPeriodPresetSelect = requireSelectById('receiptPeriodPreset');
    receiptsCountElement = requireElementById<HTMLElement>('receiptsCount');
    receiptsSumElement = requireElementById<HTMLElement>('receiptsSum');
    receiptMoneyMovementsModalElement = requireElementById<HTMLElement>('receiptMoneyMovementsModal');
    receiptMoneyMovementsAlert = requireElementById<HTMLElement>('receiptMoneyMovementsAlert');
    receiptMoneyMovementsInfo = requireElementById<HTMLElement>('receiptMoneyMovementsInfo');
    receiptLinkedMoneyMovements = requireElementById<HTMLElement>('receiptLinkedMoneyMovements');
    receiptMoneyMovementCandidates = requireElementById<HTMLElement>('receiptMoneyMovementCandidates');
    receiptMoneyMovementsDateFrom = requireInputById('receiptMoneyMovementsDateFrom');
    receiptMoneyMovementsDateTo = requireInputById('receiptMoneyMovementsDateTo');
    receiptMoneyMovementsAmountTolerance = requireInputById('receiptMoneyMovementsAmountTolerance');
    receiptMoneyMovementsTimeWindowHours = requireInputById('receiptMoneyMovementsTimeWindowHours');
    receiptMoneyMovementsUseTimeWindow = requireInputById('receiptMoneyMovementsUseTimeWindow');
    receiptMoneyMovementsUseAmountFilter = requireInputById('receiptMoneyMovementsUseAmountFilter');
    receiptMoneyMovementsExcludeLinked = requireInputById('receiptMoneyMovementsExcludeLinked');
    receiptMoneyMovementsReloadCandidatesButton = requireElementById<HTMLButtonElement>('receiptMoneyMovementsReloadCandidatesButton');
    forgeryToken = getRequestVerificationToken();

    if (modalElement) {
        bootstrapModal = createBootstrapModal(modalElement);

        // РЈР±РёСЂР°С‚СЊ С„РѕРєСѓСЃ РёР· РјРѕРґР°Р»РєРё РїСЂРё Р·Р°РєСЂС‹С‚РёРё, С‡С‚РѕР±С‹ РёР·Р±РµР¶Р°С‚СЊ РїСЂРµРґСѓРїСЂРµР¶РґРµРЅРёСЏ aria-hidden
        modalElement.addEventListener('hide.bs.modal', function () {
            if (document.activeElement instanceof HTMLElement && modalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });
    }

    if (applyFilterButton) {
        applyFilterButton.addEventListener('click', function () {
            loadReceiptList();
        });
    }

    if (moveReceiptAccountModalElement) {
        moveReceiptAccountBootstrapModal = createBootstrapModal(moveReceiptAccountModalElement);

        moveReceiptAccountModalElement.addEventListener('hide.bs.modal', function () {
            if (document.activeElement instanceof HTMLElement && moveReceiptAccountModalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });

        moveReceiptAccountModalElement.addEventListener('hidden.bs.modal', function () {
            if (preserveMoveReceiptAccountModalStateOnHide) {
                return;
            }

            pendingReceiptAccountAction = null;
            hideMoveReceiptAccountAlert();

            if (moveReceiptTargetAccountSelect) {
                moveReceiptTargetAccountSelect.replaceChildren();
                moveReceiptTargetAccountSelect.disabled = false;
            }

            if (moveReceiptSourceAccountElement) {
                moveReceiptSourceAccountElement.textContent = '';
                moveReceiptSourceAccountElement.style.border = '';
            }
        });
    }

    if (listContainer) {
        listContainer.addEventListener('click', onReceiptListClick);
    }

    receiptMoneyMovementsBootstrapModal = createBootstrapModal(receiptMoneyMovementsModalElement);
    receiptMoneyMovementsReloadCandidatesButton.addEventListener('click', function () {
        reloadReceiptMoneyMovementCandidates().catch(function (error) {
            showReceiptMoneyMovementsAlert(getErrorMessage(error));
        });
    });
    receiptMoneyMovementsUseTimeWindow.addEventListener('change', updateReceiptMoneyMovementFilterState);
    receiptMoneyMovementsUseAmountFilter.addEventListener('change', updateReceiptMoneyMovementFilterState);
    receiptLinkedMoneyMovements.addEventListener('click', onReceiptLinkedMoneyMovementsClick);
    receiptMoneyMovementCandidates.addEventListener('click', onReceiptMoneyMovementCandidatesClick);
    receiptMoneyMovementsModalElement.addEventListener('hidden.bs.modal', function () {
        selectedMoneyMovementsReceiptId = null;
        hideReceiptMoneyMovementsAlert();
    });

    if (deleteModalElement) {
        deleteBootstrapModal = createBootstrapModal(deleteModalElement);

        // РЈР±РёСЂР°С‚СЊ С„РѕРєСѓСЃ РёР· РјРѕРґР°Р»РєРё РїСЂРё Р·Р°РєСЂС‹С‚РёРё, С‡С‚РѕР±С‹ РёР·Р±РµР¶Р°С‚СЊ РїСЂРµРґСѓРїСЂРµР¶РґРµРЅРёСЏ aria-hidden
        deleteModalElement.addEventListener('hide.bs.modal', function () {
            if (document.activeElement instanceof HTMLElement && deleteModalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });

        deleteModalElement.addEventListener('hidden.bs.modal', function () {
            if (shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation && moveReceiptAccountBootstrapModal) {
                shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;
                preserveMoveReceiptAccountModalStateOnHide = false;
                moveReceiptAccountBootstrapModal.show();
                return;
            }

            pendingDeleteAction = null;
            pendingDeleteReceiptId = null;
            pendingDeleteCardElement = null;
        });
    }

    if (deleteConfirmButton) {
        deleteConfirmButton.addEventListener('click', onConfirmDeleteReceipt);
    }

    if (confirmMoveReceiptAccountButton) {
        confirmMoveReceiptAccountButton.addEventListener('click', onConfirmMoveReceiptToAccount);
    }

    if (removeReceiptFromAccountButton) {
        removeReceiptFromAccountButton.addEventListener('click', openRemoveReceiptFromAccountConfirmation);
    }

    receiptSearchInput = requireInputById('receiptSearch');
    receiptSearchModeSelect = requireSelectById('receiptSearchMode');
    receiptAccountFilterSelect = requireSelectById('receiptAccountFilter');
    receiptAccountFilterError = requireElementById<HTMLElement>('receiptAccountFilterError');
    receiptOperationFilterSelect = requireSelectById('receiptOperationFilter');

    if (receiptSearchInput) {
        receiptSearchInput.addEventListener('input', onSearchChanged);
    }

    if (receiptSearchModeSelect) {
        receiptSearchModeSelect.addEventListener('change', onSearchChanged);
    }

    if (receiptAccountFilterSelect) {
        receiptAccountFilterSelect.addEventListener('change', onSearchChanged);
    }

    if (receiptOperationFilterSelect) {
        receiptOperationFilterSelect.addEventListener('change', onSearchChanged);
    }
    if (receiptPeriodPresetSelect) {
        receiptPeriodPresetSelect.addEventListener('change', onReceiptPeriodPresetChanged);
    }

    setCurrentMonthPeriod();

    await loadAvailableAccountsAsync();
    await loadReceiptList();
}

function onReceiptPeriodPresetChanged(): void {
    applySelectedReceiptPeriodPreset();
}

function applySelectedReceiptPeriodPreset(): void {
    const periodPreset = receiptPeriodPresetSelect.value || 'currentMonth';
    const range = getDateRangeByPeriodPreset(periodPreset, new Date());

    setReceiptDateRange(range.dateFrom, range.dateTo);
}

function setCurrentMonthPeriod(): void {
    receiptPeriodPresetSelect.value = 'currentMonth';

    applySelectedReceiptPeriodPreset();
}

function setReceiptDateRange(dateFrom: Date, dateTo: Date): void {
    dateFromInput.value = formatDateForQuery(dateFrom);
    dateToInput.value = formatDateForQuery(dateTo);
}

// РћР±СЂР°Р±РѕС‚С‡РёРє РєР»РёРєР° РїРѕ РєР°СЂС‚РѕС‡РєР°Рј С‡РµРєРѕРІ
function onReceiptListClick(event: MouseEvent): void {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const openButton = target.closest<HTMLButtonElement>('[data-action="open"]');
    const refreshButton = target.closest<HTMLButtonElement>('[data-action="refresh"]');
    const deleteButton = target.closest<HTMLButtonElement>('[data-action="delete"]');
    const accountButton = target.closest<HTMLButtonElement>('[data-action="edit-account-link"]');
    const moneyMovementsButton = target.closest<HTMLButtonElement>('[data-action="open-money-movements"]');

    if (moneyMovementsButton) {
        const receiptId = moneyMovementsButton.getAttribute('data-receipt-id');
        if (!receiptId) {
            return;
        }

        openReceiptMoneyMovementsModal(receiptId);
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
        openReceipt(receiptId);
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
        refreshReceipt(receiptId, card, refreshButton);
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
        openDeleteReceiptModal(receiptId, card);
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

        openMoveReceiptAccountModal(receiptId, accountId);
    }
}

// РЎРѕР±СЂР°С‚СЊ query-РїР°СЂР°РјРµС‚СЂС‹ РґР»СЏ dateFrom/dateTo
function buildDateRangeQuery(): string {
    const fromVal = dateFromInput.value;
    const toVal = dateToInput.value;

    const parts = [];

    if (fromVal) {
        parts.push('dateFrom=' + encodeURIComponent(fromVal));
    }
    if (toVal) {
        parts.push('dateTo=' + encodeURIComponent(toVal));
    }

    return parts.join('&');
}

// Р—Р°РіСЂСѓР·РёС‚СЊ СЃРїРёСЃРѕРє С‡РµРєРѕРІ (РѕР¶РёРґР°РµС‚СЃСЏ РјР°СЃСЃРёРІ DTO)
async function loadReceiptList(): Promise<void> {
    try {
        const rangeQuery = buildDateRangeQuery();
        receiptsCache = await loadReceiptsApi(rangeQuery, forgeryToken);
        renderReceiptList(applyReceiptFilters(receiptsCache));
    } catch (error) {
        console.error(error);
        alert('РћС€РёР±РєР° РїСЂРё РїРѕР»СѓС‡РµРЅРёРё СЃРїРёСЃРєР° С‡РµРєРѕРІ.');
    }
}

async function loadAvailableAccountsAsync(): Promise<void> {
    if (!receiptAccountFilterSelect) {
        throw new Error('РќРµ РЅР°Р№РґРµРЅ С„РёР»СЊС‚СЂ СЃС‡РµС‚РѕРІ.');
    }

    receiptAccountFilterSelect.disabled = true;
    renderAccountFilterLoading(receiptAccountFilterSelect);

    try {
        availableAccountsCache = (await loadAvailableAccountsApi(forgeryToken)).map(normalizeAvailableAccountCore);

        renderAccountFilterOptions(receiptAccountFilterSelect, availableAccountsCache);
        hideAccountFilterError(receiptAccountFilterError);
        receiptAccountFilterSelect.disabled = false;
    } catch (error) {
        renderAccountFilterError({ select: receiptAccountFilterSelect, error: receiptAccountFilterError }, error instanceof Error ? error : new Error("РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ СЃРїРёСЃРѕРє СЃС‡РµС‚РѕРІ."));
        throw error;
    }
}

// РћС‚РєСЂС‹С‚СЊ С‡РµРє (РѕР¶РёРґР°РµС‚СЃСЏ РѕРґРёРЅ ReceiptDto СЃ Items)
async function openReceipt(receiptId: string): Promise<void> {
    try {
        const data = await openReceiptApi(receiptId, forgeryToken);
        renderReceiptDetailsModal(
            data,
            {
                detailsList: detailsList,
                modalHeader: modalHeader,
                modalTotal: modalTotal,
                bootstrapModal: bootstrapModal
            },
            formatNumber,
            formatCurrency
        );
    } catch (error) {
        console.error(error);
        alert('РћС€РёР±РєР° РїСЂРё РїРѕР»СѓС‡РµРЅРёРё РґРµС‚Р°Р»РµР№ С‡РµРєР°.');
    }
}

function renderReceiptList(list: ReceiptDto[]): void {
    clearElement(listContainer);
    updateReceiptSummary(list);

    for (const r of list) {
        const card = buildReceiptCardElement(r, formatCurrency);
        listContainer.appendChild(card);
    }
}

function updateReceiptSummary(list: ReceiptDto[]): void {
    if (!receiptsCountElement || !receiptsSumElement) {
        throw new Error('Р­Р»РµРјРµРЅС‚С‹ СЃРІРѕРґРєРё С‡РµРєРѕРІ РЅРµ РЅР°Р№РґРµРЅС‹.');
    }

    updateReceiptsSummary(receiptsCountElement, receiptsSumElement, list, formatCurrency);
}

// РћР±РЅРѕРІРёС‚СЊ С‡РµРє (РѕР¶РёРґР°РµС‚СЃСЏ РѕРґРёРЅ ReceiptDto Р±РµР· Items)
async function refreshReceipt(receiptId: string, cardElement: HTMLElement, buttonElement: HTMLButtonElement): Promise<void> {
    const originalText = buttonElement.textContent;
    buttonElement.disabled = true;
    buttonElement.textContent = 'РћР±РЅРѕРІР»РµРЅРёРµ...';

    try {
        const data = await refreshReceiptApi(receiptId, forgeryToken);
        updateReceiptCardFromDto(cardElement, data, formatCurrency);
    } catch (error) {
        console.error(error);
        alert(error?.message ?? error);
    } finally {
        buttonElement.disabled = false;
        buttonElement.textContent = originalText;
    }
}

// РћС‚РєСЂС‹С‚СЊ РјРѕРґР°Р»СЊРЅРѕРµ РѕРєРЅРѕ РїРѕРґС‚РІРµСЂР¶РґРµРЅРёСЏ СѓРґР°Р»РµРЅРёСЏ
function openDeleteReceiptModal(receiptId: string, cardElement: HTMLElement): void {
    const receipt = findReceiptByIdCore(receiptsCache, receiptId);
    if (!receipt) {
        alert('Р§РµРє РЅРµ РЅР°Р№РґРµРЅ РІ С‚РµРєСѓС‰РµРј СЃРїРёСЃРєРµ.');
        return;
    }

    pendingDeleteReceiptId = receiptId;
    pendingDeleteCardElement = cardElement;
    pendingDeleteAction = {
        type: DELETE_ACTION_DELETE_RECEIPT,
        receiptId: receiptId,
        cardElement: cardElement
    };

    fillDeleteReceiptModal(
        'РЈРґР°Р»РµРЅРёРµ С‡РµРєР°',
        'Р’С‹ СѓРІРµСЂРµРЅС‹, С‡С‚Рѕ С…РѕС‚РёС‚Рµ СѓРґР°Р»РёС‚СЊ СЌС‚РѕС‚ С‡РµРє?',
        receipt
    );

    // РЎР±СЂР°СЃС‹РІР°С‚СЊ РІРѕР·РјРѕР¶РЅРѕРµ РїСЂРµРґС‹РґСѓС‰РµРµ СЃРѕСЃС‚РѕСЏРЅРёРµ РєРЅРѕРїРєРё
    if (deleteConfirmButton) {
        deleteConfirmButton.disabled = false;
        deleteConfirmButton.textContent = 'РЈРґР°Р»РёС‚СЊ';
    }

    deleteBootstrapModal.show();
}

// РћР±СЂР°Р±РѕС‚Р°С‚СЊ РїРѕРґС‚РІРµСЂР¶РґРµРЅРёРµ СѓРґР°Р»РµРЅРёСЏ
async function onConfirmDeleteReceipt(): Promise<void> {
    if (!pendingDeleteAction) {
        return;
    }

    // Р‘Р»РѕРєРёСЂРѕРІР°С‚СЊ РєРЅРѕРїРєСѓ Рё РїРѕРєР°Р·Р°С‚СЊ СЃРѕСЃС‚РѕСЏРЅРёРµ СѓРґР°Р»РµРЅРёСЏ
    const originalText = deleteConfirmButton.textContent;
    deleteConfirmButton.disabled = true;
    deleteConfirmButton.textContent = 'РЈРґР°Р»РµРЅРёРµ...';

    try {
        if (pendingDeleteAction.type === DELETE_ACTION_DELETE_RECEIPT) {
            await deleteReceiptAsync(pendingDeleteAction.receiptId);
        } else if (pendingDeleteAction.type === DELETE_ACTION_REMOVE_FROM_ACCOUNT) {
            shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;
            preserveMoveReceiptAccountModalStateOnHide = false;
            await removeReceiptFromAccountAsync(pendingDeleteAction.receiptId, pendingDeleteAction.accountId);
            closeMoveReceiptAccountModal();
        }
        else {
            throw new Error('РќРµРёР·РІРµСЃС‚РЅС‹Р№ С‚РёРї СѓРґР°Р»РµРЅРёСЏ С‡РµРєР°.');
        }

        renderReceiptList(applyReceiptFilters(receiptsCache));

        deleteBootstrapModal.hide();

        pendingDeleteAction = null;
        pendingDeleteReceiptId = null;
        pendingDeleteCardElement = null;
    } catch (error) {
        console.error(error);
        alert('РћС€РёР±РєР° РїСЂРё СѓРґР°Р»РµРЅРёРё С‡РµРєР°.');
    } finally {
        // Р’РѕСЃСЃС‚Р°РЅР°РІР»РёРІР°С‚СЊ РєРЅРѕРїРєСѓ
        deleteConfirmButton.disabled = false;
        deleteConfirmButton.textContent = originalText;
    }
}

// РџРѕСЃС‚СЂРѕРёС‚СЊ РєР°СЂС‚РѕС‡РєСѓ С‡РµРєР°
// РћС‚СЂРёСЃРѕРІР°С‚СЊ РґРµС‚Р°Р»Рё С‡РµРєР° РІ РјРѕРґР°Р»СЊРЅРѕРј РѕРєРЅРµ
// РћР±РЅРѕРІРёС‚СЊ СЃСѓС‰РµСЃС‚РІСѓСЋС‰СѓСЋ РєР°СЂС‚РѕС‡РєСѓ С‡РµРєР°
// Р¤РѕСЂРјР°С‚РёСЂРѕРІР°РЅРёРµ РґР°С‚С‹ РґР»СЏ query (YYYY-MM-DD)
// Р¤РѕСЂРјР°С‚РёСЂРѕРІР°РЅРёРµ С‡РёСЃР»Р°
function formatNumber(value: number): string {
    if (typeof value !== 'number') {
        return value;
    }
    return formatRuNumber(value);
}

// Р¤РѕСЂРјР°С‚РёСЂРѕРІР°РЅРёРµ РІР°Р»СЋС‚С‹
function formatCurrency(value: number): string {
    if (typeof value !== 'number') {
        return value;
    }
    return formatMoneyRub(value);
}

function fillDeleteReceiptModal(title: string, message: string, receipt: ReceiptDto): void {
    fillDeleteReceiptModalUi(
        {
            titleElement: deleteModalTitleElement,
            messageElement: deleteModalMessageElement,
            receiptInfoElement: deleteModalReceiptInfoElement
        },
        title,
        message,
        receipt,
        formatCurrency
    );
}

async function deleteReceiptAsync(receiptId: string): Promise<void> {
    await deleteReceiptApi(receiptId, forgeryToken);
    receiptsCache = removeReceiptFromCache(receiptsCache, receiptId);

    if (pendingDeleteCardElement && pendingDeleteCardElement.parentNode) {
        pendingDeleteCardElement.parentNode.removeChild(pendingDeleteCardElement);
    }
}

function openRemoveReceiptFromAccountConfirmation(): void {
    if (!pendingReceiptAccountAction || !deleteBootstrapModal || !moveReceiptAccountBootstrapModal) {
        return;
    }

    if (!canRemoveReceiptFromSelectedAccount()) {
        updateMoveReceiptActionState();
        return;
    }

    const receipt = findReceiptByAccountReceiptIdCore(receiptsCache, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
    if (!receipt) {
        alert('Р§РµРє РЅРµ РЅР°Р№РґРµРЅ РІ С‚РµРєСѓС‰РµРј СЃРїРёСЃРєРµ.');
        return;
    }

    pendingDeleteAction = {
        type: DELETE_ACTION_REMOVE_FROM_ACCOUNT,
        receiptId: pendingReceiptAccountAction.receiptId,
        cardElement: null,
        accountId: pendingReceiptAccountAction.sourceAccountId
    };

    fillDeleteReceiptModal(
        'РЈРґР°Р»РµРЅРёРµ С‡РµРєР° РёР· СЃС‡С‘С‚Р°',
        'Р’С‹ СѓРІРµСЂРµРЅС‹, С‡С‚Рѕ С…РѕС‚РёС‚Рµ СѓРґР°Р»РёС‚СЊ СЌС‚РѕС‚ С‡РµРє РёР· СЃС‡С‘С‚Р°?',
        receipt
    );

    if (deleteConfirmButton) {
        deleteConfirmButton.disabled = false;
        deleteConfirmButton.textContent = 'РЈРґР°Р»РёС‚СЊ';
    }

    preserveMoveReceiptAccountModalStateOnHide = true;
    shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = true;
    moveReceiptAccountBootstrapModal.hide();
    deleteBootstrapModal.show();
}

function normalizeSingleLineText(value: string | null | undefined): string {
    return normalizeSingleLineTextValue(value);
}

function onSearchChanged(): void {
    if (searchDebounceTimerId) {
        clearTimeout(searchDebounceTimerId);
    }

    searchDebounceTimerId = setTimeout(function () {
        renderReceiptList(applyReceiptFilters(receiptsCache));
    }, 250);
}

function applyReceiptFilters(list: ReceiptDto[]): ReceiptDto[] {
    const query = getSearchQuery();
    const mode = getSearchMode();
    const accountId = getSelectedReceiptAccountId();
    const operationFilter = getReceiptOperationFilter();
    return applyReceiptFiltersCore(list, query, mode, accountId, operationFilter);
}

function getSearchQuery(): string {
    if (!receiptSearchInput) return '';
    const v = (receiptSearchInput.value || '').trim();
    return v;
}

function getSearchMode(): string {
    if (!receiptSearchModeSelect) return 'all';
    return receiptSearchModeSelect.value || 'all';
}

function getSelectedReceiptAccountId(): string {
    if (!receiptAccountFilterSelect) return '';
    return receiptAccountFilterSelect.value || '';
}

function getReceiptOperationFilter(): string {
    if (!receiptOperationFilterSelect) return 'all';
    return receiptOperationFilterSelect.value || 'all';
}

function openMoveReceiptAccountModal(receiptId: string, sourceAccountId: string): void {
    const receipt = findReceiptByAccountReceiptIdCore(receiptsCache, receiptId, sourceAccountId);
    if (!receipt) {
        alert('Р§РµРє РЅРµ РЅР°Р№РґРµРЅ РІ С‚РµРєСѓС‰РµРј СЃРїРёСЃРєРµ.');
        return;
    }

    const sourceAccount = findReceiptAccountCore(receipt, sourceAccountId, receiptId);
    if (!sourceAccount) {
        alert('РЎРІСЏР·СЊ СЃРѕ СЃС‡С‘С‚РѕРј РЅРµ РЅР°Р№РґРµРЅР°.');
        return;
    }

    if (!sourceAccount.canEditReceipt) {
        alert('РќРµРґРѕСЃС‚Р°С‚РѕС‡РЅРѕ РїСЂР°РІ РґР»СЏ РёР·РјРµРЅРµРЅРёСЏ С‡РµРєР° РІ РІС‹Р±СЂР°РЅРЅРѕРј СЃС‡С‘С‚Рµ.');
        return;
    }

    pendingReceiptAccountAction = {
        receiptId: receiptId,
        sourceAccountId: sourceAccountId
    };

    moveReceiptSourceAccountElement.textContent = sourceAccount.name;
    moveReceiptSourceAccountElement.style.border = '2px solid ' + sourceAccount.colorHex;
    hideMoveReceiptAccountAlert();
    renderMoveReceiptTargetOptions(receipt, sourceAccountId);
    updateMoveReceiptActionState();
    moveReceiptAccountBootstrapModal.show();
}

function renderMoveReceiptTargetOptions(receipt: ReceiptDto, sourceAccountId: string): void {
    const selectedValue = moveReceiptTargetAccountSelect.value || '';
    moveReceiptTargetAccountSelect.replaceChildren();

    const accounts = getAvailableTargetAccountsCore(availableAccountsCache, receipt, sourceAccountId);
    for (const account of accounts) {
        const option = document.createElement('option');
        option.value = account.id;
        option.textContent = account.name;
        option.disabled = account.isDisabled === true;
        option.setAttribute('data-reason', account.disabledReason || '');
        moveReceiptTargetAccountSelect.appendChild(option);
    }

    if (selectedValue) {
        const matchingOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(function (option) {
            return option.value === selectedValue && option.disabled === false;
        });

        moveReceiptTargetAccountSelect.value = matchingOption ? selectedValue : '';
    } else {
        const firstEnabledOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(function (option) {
            return option.value && option.disabled === false;
        });

        moveReceiptTargetAccountSelect.value = firstEnabledOption ? firstEnabledOption.value : '';
    }

    moveReceiptTargetAccountSelect.disabled = accounts.length === 0;
    moveReceiptTargetAccountSelect.onchange = onMoveReceiptTargetAccountChanged;
}

function onMoveReceiptTargetAccountChanged(): void {
    updateMoveReceiptActionState();
}

function updateMoveReceiptActionState(): void {
    const canRemove = canRemoveReceiptFromSelectedAccount();
    const selectedOption = getSelectedMoveReceiptTargetOption();
    const hasOtherAccounts = !!moveReceiptTargetAccountSelect && moveReceiptTargetAccountSelect.options.length > 0;
    const canMove = hasOtherAccounts && !!selectedOption && !selectedOption.disabled;

    removeReceiptFromAccountButton.disabled = !canRemove;

    confirmMoveReceiptAccountButton.disabled = !canMove;

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
    if (moveReceiptTargetAccountSelect.options.length === 0) {
        return '';
    }

    const enabledOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(function (option) {
        return option.value && option.disabled === false;
    });

    if (enabledOption) {
        return '';
    }

    const disabledOption = Array.from<HTMLOptionElement>(moveReceiptTargetAccountSelect.options).find(function (option) {
        return option.value && option.disabled === true && option.getAttribute('data-reason');
    });

    return disabledOption ? disabledOption.getAttribute('data-reason') || '' : 'РќРµС‚ РґРѕСЃС‚СѓРїРЅС‹С… СЃС‡РµС‚РѕРІ РґР»СЏ РїРµСЂРµРЅРѕСЃР° СЌС‚РѕРіРѕ С‡РµРєР°.';
}

async function onConfirmMoveReceiptToAccount(): Promise<void> {
    if (!pendingReceiptAccountAction) {
        return;
    }

    const selectedOption = getSelectedMoveReceiptTargetOption();
    if (!selectedOption || selectedOption.disabled) {
        updateMoveReceiptActionState();
        return;
    }

    const originalText = confirmMoveReceiptAccountButton.textContent;
    confirmMoveReceiptAccountButton.disabled = true;
    confirmMoveReceiptAccountButton.textContent = 'РџРµСЂРµРЅРѕСЃ...';

    removeReceiptFromAccountButton.disabled = true;

    try {
        await moveReceiptToAccountApi(pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId, selectedOption.value, forgeryToken);

        replaceReceiptAccountLinkCore(
            receiptsCache,
            availableAccountsCache,
            pendingReceiptAccountAction.receiptId,
            pendingReceiptAccountAction.sourceAccountId,
            selectedOption.value
        );

        closeMoveReceiptAccountModal();
        renderReceiptList(applyReceiptFilters(receiptsCache));
    } catch (error) {
        console.error(error);
        showMoveReceiptAccountAlert(error && error.message ? error.message : 'РќРµ СѓРґР°Р»РѕСЃСЊ РїРµСЂРµРЅРµСЃС‚Рё С‡РµРє РІ РґСЂСѓРіРѕР№ СЃС‡С‘С‚.');
    } finally {
        confirmMoveReceiptAccountButton.textContent = originalText;
        updateMoveReceiptActionState();
    }
}

async function removeReceiptFromAccountAsync(receiptId: string, accountId: string): Promise<void> {
    await removeReceiptFromAccountApi(receiptId, accountId, forgeryToken);

    receiptsCache = removeReceiptAccountLinkCore(receiptsCache, receiptId, accountId);
}

function closeMoveReceiptAccountModal(): void {
    pendingReceiptAccountAction = null;
    hideMoveReceiptAccountAlert();

    moveReceiptTargetAccountSelect.replaceChildren();
    moveReceiptTargetAccountSelect.disabled = false;

    moveReceiptSourceAccountElement.textContent = '';
    moveReceiptSourceAccountElement.style.border = '';

    moveReceiptAccountBootstrapModal.hide();
}

function getSelectedMoveReceiptTargetOption(): HTMLOptionElement | null {
    if (!moveReceiptTargetAccountSelect) {
        return null;
    }

    const selectedIndex = moveReceiptTargetAccountSelect.selectedIndex;
    if (selectedIndex < 0) {
        return null;
    }

    const option = moveReceiptTargetAccountSelect.options[selectedIndex];
    if (!option || !option.value) {
        return null;
    }

    return option;
}

function canRemoveReceiptFromSelectedAccount(): boolean {
    if (!pendingReceiptAccountAction) {
        return false;
    }

    const receipt = findReceiptByAccountReceiptIdCore(receiptsCache, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
    if (!receipt) {
        return false;
    }

    const sourceAccount = findReceiptAccountCore(receipt, pendingReceiptAccountAction.sourceAccountId, pendingReceiptAccountAction.receiptId);
    if (!sourceAccount) {
        return false;
    }

    return sourceAccount.canEditReceipt === true;
}

function showMoveReceiptAccountAlert(message: string): void {
    moveReceiptAccountAlertElement.textContent = message || '';
    moveReceiptAccountAlertElement.classList.toggle('d-none', !message);
}

function hideMoveReceiptAccountAlert(): void {
    showMoveReceiptAccountAlert('');
}

async function openReceiptMoneyMovementsModal(receiptId: string): Promise<void> {
    const receipt = findReceiptByIdCore(receiptsCache, receiptId);
    if (!receipt) {
        alert('Р§РµРє РЅРµ РЅР°Р№РґРµРЅ РІ С‚РµРєСѓС‰РµРј СЃРїРёСЃРєРµ.');
        return;
    }

    selectedMoneyMovementsReceiptId = receiptId;
    receiptMoneyMovementsInfo.textContent = buildReceiptMoneyMovementInfoText(receipt);
    initReceiptMoneyMovementFilters(receipt);
    updateReceiptMoneyMovementFilterState();
    hideReceiptMoneyMovementsAlert();
    receiptMoneyMovementsBootstrapModal.show();
    await reloadReceiptMoneyMovementDetails();
}

async function reloadReceiptMoneyMovementDetails(): Promise<void> {
    if (!selectedMoneyMovementsReceiptId) {
        return;
    }

    await Promise.all([
        reloadReceiptLinkedMoneyMovements(),
        reloadReceiptMoneyMovementCandidates()
    ]);
}

async function reloadReceiptLinkedMoneyMovements(): Promise<void> {
    if (!selectedMoneyMovementsReceiptId) {
        return;
    }

    const movements = await loadLinkedMoneyMovementsApi(selectedMoneyMovementsReceiptId, forgeryToken);
    renderReceiptMoneyMovementList(receiptLinkedMoneyMovements, movements, 'unlink-money-movement', 'РћС‚РІСЏР·Р°С‚СЊ', 'РџСЂРёРІСЏР·Р°РЅРЅС‹С… РѕРїРµСЂР°С†РёР№ РЅРµС‚.');
}

async function reloadReceiptMoneyMovementCandidates(): Promise<void> {
    if (!selectedMoneyMovementsReceiptId) {
        return;
    }

    const movements = await loadMoneyMovementCandidatesApi(readReceiptMoneyMovementCandidatesRequest(selectedMoneyMovementsReceiptId), forgeryToken);
    renderReceiptMoneyMovementList(receiptMoneyMovementCandidates, movements, 'link-money-movement', 'РџСЂРёРІСЏР·Р°С‚СЊ', 'РџРѕРґС…РѕРґСЏС‰РёРµ РѕРїРµСЂР°С†РёРё РЅРµ РЅР°Р№РґРµРЅС‹.');
}

function readReceiptMoneyMovementCandidatesRequest(receiptId: string): GetReceiptMoneyMovementCandidatesRequest {
    const amountTolerance = Number(receiptMoneyMovementsAmountTolerance.value);
    const timeWindowHours = Number(receiptMoneyMovementsTimeWindowHours.value);

    return {
        receiptId: receiptId,
        useTimeWindow: receiptMoneyMovementsUseTimeWindow.checked,
        timeWindowHours: Number.isFinite(timeWindowHours) ? timeWindowHours : null,
        dateFrom: receiptMoneyMovementsDateFrom.value || null,
        dateTo: receiptMoneyMovementsDateTo.value || null,
        useAmountFilter: receiptMoneyMovementsUseAmountFilter.checked,
        amountTolerance: Number.isFinite(amountTolerance) ? amountTolerance : null,
        excludeLinkedMoneyMovements: receiptMoneyMovementsExcludeLinked.checked
    };
}

function initReceiptMoneyMovementFilters(receipt: ReceiptDto): void {
    const receiptDate = new Date(receipt.dateTime);
    const date = Number.isNaN(receiptDate.getTime()) ? new Date() : receiptDate;
    const dateFrom = new Date(date);
    const dateTo = new Date(date);
    dateFrom.setDate(dateFrom.getDate() - 1);
    dateTo.setDate(dateTo.getDate() + 1);

    receiptMoneyMovementsDateFrom.value = formatDateForQuery(dateFrom);
    receiptMoneyMovementsDateTo.value = formatDateForQuery(dateTo);
    receiptMoneyMovementsUseTimeWindow.checked = false;
    receiptMoneyMovementsUseAmountFilter.checked = true;
    receiptMoneyMovementsExcludeLinked.checked = true;
    receiptMoneyMovementsAmountTolerance.value = '1';
    receiptMoneyMovementsTimeWindowHours.value = '1';
}

function updateReceiptMoneyMovementFilterState(): void {
    const useTimeWindow = receiptMoneyMovementsUseTimeWindow.checked;
    receiptMoneyMovementsDateFrom.disabled = useTimeWindow;
    receiptMoneyMovementsDateTo.disabled = useTimeWindow;
    receiptMoneyMovementsTimeWindowHours.disabled = !useTimeWindow;
    receiptMoneyMovementsAmountTolerance.disabled = !receiptMoneyMovementsUseAmountFilter.checked;
}

async function onReceiptLinkedMoneyMovementsClick(event: MouseEvent): Promise<void> {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const button = target.closest<HTMLButtonElement>('[data-action="unlink-money-movement"]');
    if (!button) {
        return;
    }

    const moneyMovementId = button.getAttribute('data-money-movement-id');
    if (!selectedMoneyMovementsReceiptId || !moneyMovementId) {
        return;
    }

    await runReceiptMoneyMovementButtonAction(button, async () => {
        await unlinkReceiptMoneyMovementApi({
            receiptId: selectedMoneyMovementsReceiptId!,
            moneyMovementId: moneyMovementId
        }, forgeryToken);
        await loadReceiptList();
        await reloadReceiptMoneyMovementDetails();
    });
}

async function onReceiptMoneyMovementCandidatesClick(event: MouseEvent): Promise<void> {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const button = target.closest<HTMLButtonElement>('[data-action="link-money-movement"]');
    if (!button) {
        return;
    }

    const moneyMovementId = button.getAttribute('data-money-movement-id');
    if (!selectedMoneyMovementsReceiptId || !moneyMovementId) {
        return;
    }

    await runReceiptMoneyMovementButtonAction(button, async () => {
        await linkReceiptMoneyMovementApi({
            receiptId: selectedMoneyMovementsReceiptId!,
            moneyMovementId: moneyMovementId
        }, forgeryToken);
        await loadReceiptList();
        await reloadReceiptMoneyMovementDetails();
    });
}

async function runReceiptMoneyMovementButtonAction(button: HTMLButtonElement, action: () => Promise<void>): Promise<void> {
    const originalText = button.textContent;
    button.disabled = true;
    button.textContent = '...';

    try {
        hideReceiptMoneyMovementsAlert();
        await action();
    } catch (error) {
        showReceiptMoneyMovementsAlert(getErrorMessage(error));
    } finally {
        button.disabled = false;
        button.textContent = originalText;
    }
}

function renderReceiptMoneyMovementList(container: HTMLElement, movements: ReceiptMoneyMovementDto[], action: string, actionText: string, emptyText: string): void {
    clearElement(container);

    if (movements.length === 0) {
        const empty = document.createElement('div');
        empty.classList.add('text-muted', 'py-2');
        empty.textContent = emptyText;
        container.append(empty);
        return;
    }

    for (const movement of movements) {
        container.append(createReceiptMoneyMovementCard(movement, action, actionText));
    }
}

function createReceiptMoneyMovementCard(movement: ReceiptMoneyMovementDto, action: string, actionText: string): HTMLElement {
    const wrapper = document.createElement('div');
    wrapper.classList.add('border', 'rounded-3', 'p-2', 'd-flex', 'flex-wrap', 'justify-content-between', 'gap-2', 'align-items-start');

    if (movement.isLinkedToOtherReceipt && action === 'link-money-movement') {
        wrapper.classList.add('border-warning');
    }

    const left = document.createElement('div');
    left.classList.add('d-flex', 'flex-column', 'gap-1');

    const title = document.createElement('div');
    title.classList.add('fw-semibold');
    title.textContent = movement.comment || movement.importComment || 'Р‘РµР· РєРѕРјРјРµРЅС‚Р°СЂРёСЏ';

    const meta = document.createElement('div');
    meta.classList.add('small', 'text-muted');
    const accountText = movement.accountName ? ` В· ${movement.accountName}` : '';
    meta.textContent = `${formatDateTime(movement.occurredAt)}${accountText}`;

    const amount = document.createElement('div');
    amount.classList.add('small');
    amount.textContent = formatCurrency(movement.amount);

    left.append(title, meta, amount);

    if (movement.isLinkedToOtherReceipt && action === 'link-money-movement') {
        const warning = document.createElement('div');
        warning.classList.add('small', 'text-warning');
        warning.textContent = 'РЈР¶Рµ СЃРІСЏР·Р°РЅР° СЃ С‡РµРєРѕРј';
        left.append(warning);
    }

    const button = document.createElement('button');
    button.type = 'button';
    button.classList.add('btn', 'btn-sm', action === 'link-money-movement' ? 'btn-outline-primary' : 'btn-outline-danger');
    button.setAttribute('data-action', action);
    button.setAttribute('data-money-movement-id', movement.moneyMovementId);
    button.textContent = actionText;

    wrapper.append(left, button);
    return wrapper;
}

function buildReceiptMoneyMovementInfoText(receipt: ReceiptDto): string {
    return `${formatDateTime(receipt.dateTime)} В· ${formatCurrency(receipt.totalSum)} В· ${normalizeSingleLineTextValue(receipt.retailPlace)}`;
}

function showReceiptMoneyMovementsAlert(message: string): void {
    receiptMoneyMovementsAlert.textContent = message || '';
    receiptMoneyMovementsAlert.classList.toggle('d-none', !message);
}

function hideReceiptMoneyMovementsAlert(): void {
    showReceiptMoneyMovementsAlert('');
}

function formatDateTime(value: string): string {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return date.toLocaleString('ru-RU', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function getErrorMessage(error: unknown): string {
    if (error instanceof Error) {
        return error.message;
    }

    return 'РћС€РёР±РєР° РѕРїРµСЂР°С†РёРё.';
}
