import { createRequestCoordinator, getWriteErrorMessage, isIntentionalAbort } from "../../../shared/requestCoordinator.js";
import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { clearElement, requireElementById, requireInputById, requireSelectById } from "../../../shared/dom.js";
import { formatMoneyRub, formatRuNumber } from "../../../shared/formatters.js";
import { BootstrapModal, createBootstrapModal } from "../../../shared/bootstrap.js";
import { renderHelpTooltip } from "../../../shared/helpTooltip.js";
import { deleteReceiptApi, loadAvailableAccountsApi, loadReceiptsApi, moveReceiptToAccountApi, openReceiptApi, refreshReceiptApi, removeReceiptFromAccountApi } from "./api.js";
import { applyReceiptFilters as applyReceiptFiltersCore } from "./filters.js";
import { createReceiptsPageState } from "./state.js";
import { updateReceiptsSummary } from "./render.js";
import { formatDateForQuery, getDateRangeByPeriodPreset } from "./dateRange.js";
import { MoveReceiptAccountAction, PendingDeleteAction, ReceiptDto, ReceiptList } from "./types.js";
import { hideReceiptAccountFilterError as hideAccountFilterError, renderReceiptAccountFilterError as renderAccountFilterError, renderReceiptAccountFilterLoading as renderAccountFilterLoading, renderReceiptAccountFilterOptions as renderAccountFilterOptions } from "./ui/accountFilter.js";
import { buildReceiptCard as buildReceiptCardElement, updateCardFromDto as updateReceiptCardFromDto } from "./ui/receiptCards.js";
import { renderReceiptDetails as renderReceiptDetailsModal } from "./ui/receiptDetailsModal.js";
import { findReceiptAccount as findReceiptAccountCore, findReceiptByAccountReceiptId as findReceiptByAccountReceiptIdCore, findReceiptById as findReceiptByIdCore, getAvailableTargetAccounts as getAvailableTargetAccountsCore, normalizeAvailableAccount as normalizeAvailableAccountCore, removeReceiptAccountLink as removeReceiptAccountLinkCore, replaceReceiptAccountLink as replaceReceiptAccountLinkCore } from "./state/accountModel.js";
import { fillDeleteReceiptModal as fillDeleteReceiptModalUi } from "./modals/deleteReceiptModal.js";
import { initReceiptMoneyMovementsModal, ReceiptMoneyMovementsModalController } from "./modals/receiptMoneyMovementsModal.js";

// Глобальные переменные для страницы списка чеков
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
let receiptPageSizeSelect: HTMLSelectElement;
let receiptsPaginationElement: HTMLElement;
let currentReceiptList: ReceiptList | null = null;

// Для удаления
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
let receiptMoneyMovementsModal: ReceiptMoneyMovementsModalController;

const pageState = createReceiptsPageState();
const listRequests = createRequestCoordinator();
const accountRequests = createRequestCoordinator();
const detailRequests = createRequestCoordinator();
const receiptWrites = new Set<string>();
const confirmationContext = createRequestCoordinator();
window.addEventListener("pagehide", () => { listRequests.invalidate(); accountRequests.invalidate(); detailRequests.invalidate(); confirmationContext.invalidate();
    if (searchDebounceTimerId !== null) { window.clearTimeout(searchDebounceTimerId); searchDebounceTimerId = null; }
});
let searchDebounceTimerId: number | null = null;
let pendingReceiptAccountAction: MoveReceiptAccountAction | null = null;

const DELETE_ACTION_DELETE_RECEIPT = 'delete-receipt';
const DELETE_ACTION_REMOVE_FROM_ACCOUNT = 'remove-from-account';

// Инициализация после загрузки DOM
document.addEventListener('DOMContentLoaded', function () {
    initListOfChecksPage().catch(function (error) {
        console.error(error);
        alert(error && error.message ? error.message : 'Ошибка при инициализации страницы чеков.');
    });
});

// Инициализировать страницу списка чеков
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
    receiptPageSizeSelect = requireSelectById('receiptsPageSize');
    receiptsPaginationElement = requireElementById<HTMLElement>('receiptsPagination');
    forgeryToken = getRequestVerificationToken();

    if (modalElement) {
        bootstrapModal = createBootstrapModal(modalElement);

        // Убирать фокус из модалки при закрытии, чтобы избежать предупреждения aria-hidden
        modalElement.addEventListener('hide.bs.modal', function () {
            detailRequests.invalidate();
            if (document.activeElement instanceof HTMLElement && modalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });
    }

    if (applyFilterButton) {
        applyFilterButton.addEventListener('click', function () {
            void loadReceiptList(1);
        });
    }

    if (moveReceiptAccountModalElement) {
        moveReceiptAccountBootstrapModal = createBootstrapModal(moveReceiptAccountModalElement);

        moveReceiptAccountModalElement.addEventListener('hide.bs.modal', function () {
            if (document.activeElement instanceof HTMLElement && moveReceiptAccountModalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });

        moveReceiptAccountModalElement.addEventListener('hide.bs.modal', function () {
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

    receiptMoneyMovementsModal = initReceiptMoneyMovementsModal({
        getReceipts: function () {
            return pageState.receipts;
        },
        getForgeryToken: function () {
            return forgeryToken;
        },
        reloadReceiptList: loadReceiptList,
        formatCurrency: formatCurrency
    });

    if (deleteModalElement) {
        deleteBootstrapModal = createBootstrapModal(deleteModalElement);

        // Убирать фокус из модалки при закрытии, чтобы избежать предупреждения aria-hidden
        deleteModalElement.addEventListener('hide.bs.modal', function () {
            if (document.activeElement instanceof HTMLElement && deleteModalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });

        deleteModalElement.addEventListener('hide.bs.modal', function () {
            const isCurrent = confirmationContext.capture();
            const matchesList = listRequests.capture();
            const action = pendingReceiptAccountAction;
            const restore = shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation;
            pendingDeleteAction = null;
            pendingDeleteReceiptId = null;
            pendingDeleteCardElement = null;
            deleteModalElement.addEventListener('hidden.bs.modal', function () {
                if (!isCurrent() || !matchesList() || !restore || pendingReceiptAccountAction !== action) { return; }
                shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;
                preserveMoveReceiptAccountModalStateOnHide = false;
                moveReceiptAccountBootstrapModal.show();
            }, { once: true });
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
    renderHelpTooltip(requireElementById<HTMLElement>('receiptOperationFilterHelp'), {
        title: 'Связь с операциями',
        text: [
            'Все чеки — без фильтра.',
            'Без операций — чеки без привязанных операций.',
            'С операциями — чеки с одной или несколькими операциями.',
            'Расхождение суммы — сумма привязанных операций отличается от суммы чека.'
        ]
    });

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

    receiptPageSizeSelect.addEventListener('change', () => void loadReceiptList(1));
    dateFromInput.addEventListener('change', updatePeriodPresetForManualDateRange);
    dateToInput.addEventListener('change', updatePeriodPresetForManualDateRange);
    setCurrentMonthPeriod();

    if (!await loadAvailableAccountsAsync()) { return; }
    await loadReceiptList(getPageFromQuery());
}

function onReceiptPeriodPresetChanged(): void {
    if (receiptPeriodPresetSelect.value === 'other') return;
    applySelectedReceiptPeriodPreset();
    void loadReceiptList(1);
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

// Обработчик клика по карточкам чеков
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

        void receiptMoneyMovementsModal.open(receiptId);
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
        return;
    }

    if (assignAccountButton) {
        const receiptId = assignAccountButton.closest<HTMLElement>('.card')?.getAttribute('data-receipt-id');
        if (!receiptId) {
            return;
        }

        openMoveReceiptAccountModal(receiptId, '');
    }
}

// Собрать query-параметры для dateFrom/dateTo
function buildDateRangeQuery(page: number): string {
    const fromVal = dateFromInput.value;
    const toVal = dateToInput.value;

    const parts = [];

    if (fromVal) {
        parts.push('dateFrom=' + encodeURIComponent(fromVal));
    }
    if (toVal) {
        parts.push('dateTo=' + encodeURIComponent(toVal));
    }

    parts.push('page=' + page, 'pageSize=' + receiptPageSizeSelect.value, 'search=' + encodeURIComponent(getSearchQuery()), 'searchMode=' + encodeURIComponent(getSearchMode()), 'operationFilter=' + encodeURIComponent(getReceiptOperationFilter()));
    const accountId = getSelectedReceiptAccountId();
    if (accountId) parts.push('accountId=' + encodeURIComponent(accountId));
    return parts.join('&');
}

// Загрузить список чеков (ожидается массив DTO)
async function loadReceiptList(page = 1): Promise<void> {
    const request = listRequests.begin(buildDateRangeQuery(page));
    detailRequests.invalidate();
    const matchesQuery = () => request.parameters === buildDateRangeQuery(page);
    currentReceiptList = null;
    pageState.receipts = [];
    updateReceiptsSummary(receiptsCountElement, receiptsSumElement, null, formatCurrency);
    clearElement(receiptsPaginationElement);
    try {
        listContainer.replaceChildren(createLoadingIndicator());
        const result = await loadReceiptsApi(request.parameters, forgeryToken, request.signal);
        if (!request.isCurrent() || !matchesQuery()) { return; }
        currentReceiptList = result;
        pageState.receipts = currentReceiptList.items;
        renderReceiptList(currentReceiptList.items);
        updateReceiptsSummary(receiptsCountElement, receiptsSumElement, currentReceiptList, formatCurrency);
        renderReceiptPagination(currentReceiptList);
        updatePageQuery(currentReceiptList.page);
    } catch (error) {
        if (!request.isCurrent() || !matchesQuery() || isIntentionalAbort(error)) { return; }
        listContainer.replaceChildren();
        alert(getErrorMessage(error));
    }
}

async function loadAvailableAccountsAsync(): Promise<boolean> {
    const request = accountRequests.begin(forgeryToken);
    if (!receiptAccountFilterSelect) {
        throw new Error('Не найден фильтр счетов.');
    }

    receiptAccountFilterSelect.disabled = true;
    renderAccountFilterLoading(receiptAccountFilterSelect);

    try {
        const accounts = await loadAvailableAccountsApi(request.parameters, request.signal);
        if (!request.isCurrent()) { return false; }
        pageState.availableAccounts = accounts.map(normalizeAvailableAccountCore);

        renderAccountFilterOptions(receiptAccountFilterSelect, pageState.availableAccounts);
        hideAccountFilterError(receiptAccountFilterError);
        receiptAccountFilterSelect.disabled = false;
        return true;
    } catch (error) {
        if (!request.isCurrent() || isIntentionalAbort(error)) { return false; }
        renderAccountFilterError({ select: receiptAccountFilterSelect, error: receiptAccountFilterError }, error instanceof Error ? error : new Error("Не удалось загрузить список счетов."));
        return false;
    }
}

// Открыть чек (ожидается один ReceiptDto с Items)
async function openReceipt(receiptId: string): Promise<void> {
    const request = detailRequests.begin(receiptId);
    const matchesList = listRequests.capture();
    try {
        const data = await openReceiptApi(request.parameters, forgeryToken, request.signal);
        if (!request.isCurrent() || !matchesList()) { return; }
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
        if (!request.isCurrent() || !matchesList() || isIntentionalAbort(error)) { return; }
        alert(getErrorMessage(error));
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
        throw new Error('Элементы сводки чеков не найдены.');
    }

    updateReceiptsSummary(receiptsCountElement, receiptsSumElement, currentReceiptList, formatCurrency);
}

// Обновить чек (ожидается один ReceiptDto без Items)
async function refreshReceipt(receiptId: string, cardElement: HTMLElement, buttonElement: HTMLButtonElement): Promise<void> {
    if (receiptWrites.has(receiptId) || buttonElement.disabled) { return; }
    receiptWrites.add(receiptId);
    const matchesList = listRequests.capture();
    const originalText = buttonElement.textContent;
    buttonElement.disabled = true;
    buttonElement.textContent = 'Обновление...';

    try {
        const data = await refreshReceiptApi(receiptId, forgeryToken);
        if (!matchesList()) { return; }
        updateReceiptCardFromDto(cardElement, data, formatCurrency);
        pageState.receipts = pageState.receipts.map(receipt => receipt.id === data.id ? data : receipt);
    } catch (error) {
        if (matchesList()) { alert(getWriteErrorMessage(error)); }
    } finally {
        receiptWrites.delete(receiptId);
        if (!matchesList()) { return; }
        buttonElement.disabled = false;
        buttonElement.textContent = originalText;
    }
}

// Открыть модальное окно подтверждения удаления
function openDeleteReceiptModal(receiptId: string, cardElement: HTMLElement): void {
    confirmationContext.invalidate();
    const receipt = findReceiptByIdCore(pageState.receipts, receiptId);
    if (!receipt) {
        alert('Чек не найден в текущем списке.');
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
        'Удаление чека',
        'Вы уверены, что хотите удалить этот чек?',
        receipt
    );

    // Сбрасывать возможное предыдущее состояние кнопки
    if (deleteConfirmButton) {
        deleteConfirmButton.disabled = false;
        deleteConfirmButton.textContent = 'Удалить';
    }

    deleteBootstrapModal.show();
}

// Обработать подтверждение удаления
async function onConfirmDeleteReceipt(): Promise<void> {
    const action = pendingDeleteAction;
    if (!action || receiptWrites.has(action.receiptId)) { return; }
    const matchesList = listRequests.capture();
    const isCurrent = () => pendingDeleteAction === action && matchesList();
    receiptWrites.add(action.receiptId);
    const originalText = deleteConfirmButton.textContent;
    deleteConfirmButton.disabled = true;
    deleteConfirmButton.textContent = 'Удаление...';
    try {
        if (action.type === DELETE_ACTION_DELETE_RECEIPT) {
            await deleteReceiptApi(action.receiptId, forgeryToken);
        } else if (action.type === DELETE_ACTION_REMOVE_FROM_ACCOUNT) {
            await removeReceiptFromAccountApi(action.receiptId, action.accountId, forgeryToken);
        } else {
            throw new Error('Неизвестный тип удаления чека.');
        }
        if (!isCurrent()) { return; }
        shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;
        preserveMoveReceiptAccountModalStateOnHide = false;
        if (action.type === DELETE_ACTION_REMOVE_FROM_ACCOUNT) { closeMoveReceiptAccountModal(); }
        const page = currentReceiptList?.page ?? 1;
        deleteBootstrapModal.hide();
        await loadReceiptList(page);
    } catch (error) {
        if (isCurrent()) { alert(getWriteErrorMessage(error)); }
    } finally {
        receiptWrites.delete(action.receiptId);
        if (pendingDeleteAction === action) {
            deleteConfirmButton.disabled = false;
            deleteConfirmButton.textContent = originalText;
        }
    }
}

// Построить карточку чека
// Отрисовать детали чека в модальном окне
// Обновить существующую карточку чека
// Форматирование даты для query (YYYY-MM-DD)
// Форматирование числа
function formatNumber(value: number): string {
    if (typeof value !== 'number') {
        return value;
    }
    return formatRuNumber(value);
}

// Форматирование валюты
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


function openRemoveReceiptFromAccountConfirmation(): void {
    confirmationContext.invalidate();
    if (!pendingReceiptAccountAction || !deleteBootstrapModal || !moveReceiptAccountBootstrapModal) {
        return;
    }

    if (!canRemoveReceiptFromSelectedAccount()) {
        updateMoveReceiptActionState();
        return;
    }

    const receipt = findReceiptByAccountReceiptIdCore(pageState.receipts, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
    if (!receipt) {
        alert('Чек не найден в текущем списке.');
        return;
    }

    pendingDeleteAction = {
        type: DELETE_ACTION_REMOVE_FROM_ACCOUNT,
        receiptId: pendingReceiptAccountAction.receiptId,
        cardElement: null,
        accountId: pendingReceiptAccountAction.sourceAccountId
    };

    fillDeleteReceiptModal(
        'Удаление чека из счёта',
        'Вы уверены, что хотите удалить этот чек из счёта?',
        receipt
    );

    if (deleteConfirmButton) {
        deleteConfirmButton.disabled = false;
        deleteConfirmButton.textContent = 'Удалить';
    }

    preserveMoveReceiptAccountModalStateOnHide = true;
    shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = true;
    moveReceiptAccountBootstrapModal.hide();
    deleteBootstrapModal.show();
}

function onSearchChanged(): void {
    listRequests.invalidate();
    detailRequests.invalidate();
    if (searchDebounceTimerId) {
        clearTimeout(searchDebounceTimerId);
    }

    searchDebounceTimerId = setTimeout(function () {
        void loadReceiptList(1);
    }, 250);
}

function updatePeriodPresetForManualDateRange(): void {
    listRequests.invalidate();
    detailRequests.invalidate();
    const preset = receiptPeriodPresetSelect.value;
    if (preset === 'other') return;

    const range = getDateRangeByPeriodPreset(preset, new Date());
    if (dateFromInput.value !== formatDateForQuery(range.dateFrom) || dateToInput.value !== formatDateForQuery(range.dateTo)) {
        receiptPeriodPresetSelect.value = 'other';
    }
}

function getPageFromQuery(): number {
    const page = Number(new URLSearchParams(window.location.search).get('page'));
    return Number.isInteger(page) && page > 0 ? page : 1;
}

function updatePageQuery(page: number): void {
    const url = new URL(window.location.href);
    url.searchParams.set('page', String(page));
    window.history.replaceState(null, '', url);
}

function createLoadingIndicator(): HTMLElement {
    const indicator = document.createElement('div');
    indicator.className = 'text-center text-muted py-5';
    indicator.textContent = 'Идёт загрузка чеков...';
    return indicator;
}

function renderReceiptPagination(result: ReceiptList): void {
    clearElement(receiptsPaginationElement);
    if (result.page === 1 && result.totalCount < result.pageSize) return;

    if (result.hasPreviousPage) {
        receiptsPaginationElement.append(createReceiptPageButton('<<', 1), createReceiptPageButton('<', result.page - 1));
    }
    for (let page = Math.max(1, result.page - 2); page <= Math.min(result.totalPages, result.page + 2); page += 1) {
        receiptsPaginationElement.append(createReceiptPageButton(String(page), page, page === result.page));
    }
    if (result.hasNextPage) {
        receiptsPaginationElement.append(createReceiptPageButton('>', result.page + 1), createReceiptPageButton('>>', result.totalPages));
    }
}

function createReceiptPageButton(text: string, page: number, active = false): HTMLButtonElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = active ? 'btn btn-primary' : 'btn btn-outline-secondary';
    button.textContent = text;
    button.addEventListener('click', () => void loadReceiptList(page));
    return button;
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
    confirmationContext.invalidate();
    const receipt = sourceAccountId
        ? findReceiptByAccountReceiptIdCore(pageState.receipts, receiptId, sourceAccountId)
        : findReceiptByIdCore(pageState.receipts, receiptId);
    if (!receipt) {
        alert('Чек не найден в текущем списке.');
        return;
    }

    const sourceAccount = sourceAccountId ? findReceiptAccountCore(receipt, sourceAccountId, receiptId) : null;

    if (sourceAccount && !sourceAccount.canEditReceipt) {
        alert('Недостаточно прав для изменения чека в выбранном счёте.');
        return;
    }

    confirmMoveReceiptAccountButton.textContent = 'Перенести';
    pendingReceiptAccountAction = {
        receiptId: receiptId,
        sourceAccountId: sourceAccountId
    };

    moveReceiptSourceAccountElement.textContent = sourceAccount ? sourceAccount.name : 'Без счёта';
    moveReceiptSourceAccountElement.style.border = '2px solid ' + (sourceAccount ? sourceAccount.colorHex : '#dee2e6');
    hideMoveReceiptAccountAlert();
    renderMoveReceiptTargetOptions(receipt, sourceAccountId);
    updateMoveReceiptActionState();
    moveReceiptAccountBootstrapModal.show();
}

function renderMoveReceiptTargetOptions(receipt: ReceiptDto, sourceAccountId: string): void {
    const selectedValue = moveReceiptTargetAccountSelect.value || '';
    moveReceiptTargetAccountSelect.replaceChildren();

    const accounts = getAvailableTargetAccountsCore(pageState.availableAccounts, receipt, sourceAccountId);
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

    return disabledOption ? disabledOption.getAttribute('data-reason') || '' : 'Нет доступных счетов для переноса этого чека.';
}

async function onConfirmMoveReceiptToAccount(): Promise<void> {
    if (!pendingReceiptAccountAction) {
        return;
    }

    const action = pendingReceiptAccountAction;
    if (receiptWrites.has(action.receiptId)) { return; }
    const matchesList = listRequests.capture();
    const isCurrent = () => pendingReceiptAccountAction === action && matchesList();
    const selectedOption = getSelectedMoveReceiptTargetOption();
    if (!selectedOption || selectedOption.disabled) {
        updateMoveReceiptActionState();
        return;
    }

    const targetAccountId = selectedOption.value;
    receiptWrites.add(action.receiptId);
    const originalText = confirmMoveReceiptAccountButton.textContent;
    confirmMoveReceiptAccountButton.disabled = true;
    confirmMoveReceiptAccountButton.textContent = 'Перенос...';

    removeReceiptFromAccountButton.disabled = true;

    try {
        await moveReceiptToAccountApi(action.receiptId, action.sourceAccountId, targetAccountId, forgeryToken);

        if (!isCurrent()) { return; }
        replaceReceiptAccountLinkCore(
            pageState.receipts,
            pageState.availableAccounts,
            action.receiptId,
            action.sourceAccountId,
            targetAccountId
        );

        const page = currentReceiptList?.page ?? 1;
        closeMoveReceiptAccountModal();
        await loadReceiptList(page);
    } catch (error) {
        if (isCurrent()) { showMoveReceiptAccountAlert(getWriteErrorMessage(error)); }
    } finally {
        receiptWrites.delete(action.receiptId);
        if (pendingReceiptAccountAction !== action) { return; }
        confirmMoveReceiptAccountButton.textContent = originalText;
        updateMoveReceiptActionState();
    }
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

function getErrorMessage(error: unknown, fallback = 'Не удалось выполнить операцию.'): string {
    return error instanceof Error ? error.message : fallback;
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

    const receipt = findReceiptByAccountReceiptIdCore(pageState.receipts, pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
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
