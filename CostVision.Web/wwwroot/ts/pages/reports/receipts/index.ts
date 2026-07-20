import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { clearElement, requireElementById, requireInputById, requireSelectById } from "../../../shared/dom.js";
import { formatMoneyRub, formatRuNumber } from "../../../shared/formatters.js";
import { BootstrapModal, createBootstrapModal } from "../../../shared/bootstrap.js";
import { renderHelpTooltip } from "../../../shared/helpTooltip.js";
import { deleteReceiptApi, loadAvailableAccountsApi, loadReceiptsApi, moveReceiptToAccountApi, openReceiptApi, refreshReceiptApi, removeReceiptFromAccountApi } from "./api.js";
import { applyReceiptFilters as applyReceiptFiltersCore } from "./filters.js";
import { createReceiptsPageState, removeReceiptFromCache } from "./state.js";
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

    await loadAvailableAccountsAsync();
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
    try {
        listContainer.replaceChildren(createLoadingIndicator());
        currentReceiptList = await loadReceiptsApi(buildDateRangeQuery(page), forgeryToken);
        pageState.receipts = currentReceiptList.items;
        renderReceiptList(currentReceiptList.items);
        receiptsCountElement.textContent = 'Чеков: ' + currentReceiptList.totalCount;
        receiptsSumElement.textContent = 'Сумма: ' + formatCurrency(currentReceiptList.totalSum);
        renderReceiptPagination(currentReceiptList);
        updatePageQuery(currentReceiptList.page);
    } catch (error) {
        console.error(error);
        alert('Ошибка при получении списка чеков.');
    }
}

async function loadAvailableAccountsAsync(): Promise<void> {
    if (!receiptAccountFilterSelect) {
        throw new Error('Не найден фильтр счетов.');
    }

    receiptAccountFilterSelect.disabled = true;
    renderAccountFilterLoading(receiptAccountFilterSelect);

    try {
        pageState.availableAccounts = (await loadAvailableAccountsApi(forgeryToken)).map(normalizeAvailableAccountCore);

        renderAccountFilterOptions(receiptAccountFilterSelect, pageState.availableAccounts);
        hideAccountFilterError(receiptAccountFilterError);
        receiptAccountFilterSelect.disabled = false;
    } catch (error) {
        renderAccountFilterError({ select: receiptAccountFilterSelect, error: receiptAccountFilterError }, error instanceof Error ? error : new Error("Не удалось загрузить список счетов."));
        throw error;
    }
}

// Открыть чек (ожидается один ReceiptDto с Items)
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
        alert('Ошибка при получении деталей чека.');
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

    if (!currentReceiptList) updateReceiptsSummary(receiptsCountElement, receiptsSumElement, list, formatCurrency);
}

// Обновить чек (ожидается один ReceiptDto без Items)
async function refreshReceipt(receiptId: string, cardElement: HTMLElement, buttonElement: HTMLButtonElement): Promise<void> {
    const originalText = buttonElement.textContent;
    buttonElement.disabled = true;
    buttonElement.textContent = 'Обновление...';

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

// Открыть модальное окно подтверждения удаления
function openDeleteReceiptModal(receiptId: string, cardElement: HTMLElement): void {
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
    if (!pendingDeleteAction) {
        return;
    }

    // Блокировать кнопку и показать состояние удаления
    const originalText = deleteConfirmButton.textContent;
    deleteConfirmButton.disabled = true;
    deleteConfirmButton.textContent = 'Удаление...';

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
            throw new Error('Неизвестный тип удаления чека.');
        }

        renderReceiptList(applyReceiptFilters(pageState.receipts));

        deleteBootstrapModal.hide();

        pendingDeleteAction = null;
        pendingDeleteReceiptId = null;
        pendingDeleteCardElement = null;
    } catch (error) {
        console.error(error);
        alert('Ошибка при удалении чека.');
    } finally {
        // Восстанавливать кнопку
        deleteConfirmButton.disabled = false;
        deleteConfirmButton.textContent = originalText;
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

async function deleteReceiptAsync(receiptId: string): Promise<void> {
    await deleteReceiptApi(receiptId, forgeryToken);
    pageState.receipts = removeReceiptFromCache(pageState.receipts, receiptId);

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
    if (searchDebounceTimerId) {
        clearTimeout(searchDebounceTimerId);
    }

    searchDebounceTimerId = setTimeout(function () {
        void loadReceiptList(1);
    }, 250);
}

function updatePeriodPresetForManualDateRange(): void {
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

    const selectedOption = getSelectedMoveReceiptTargetOption();
    if (!selectedOption || selectedOption.disabled) {
        updateMoveReceiptActionState();
        return;
    }

    const originalText = confirmMoveReceiptAccountButton.textContent;
    confirmMoveReceiptAccountButton.disabled = true;
    confirmMoveReceiptAccountButton.textContent = 'Перенос...';

    removeReceiptFromAccountButton.disabled = true;

    try {
        await moveReceiptToAccountApi(pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId, selectedOption.value, forgeryToken);

        replaceReceiptAccountLinkCore(
            pageState.receipts,
            pageState.availableAccounts,
            pendingReceiptAccountAction.receiptId,
            pendingReceiptAccountAction.sourceAccountId,
            selectedOption.value
        );

        closeMoveReceiptAccountModal();
        renderReceiptList(applyReceiptFilters(pageState.receipts));
    } catch (error) {
        console.error(error);
        showMoveReceiptAccountAlert(error && error.message ? error.message : 'Не удалось перенести чек в другой счёт.');
    } finally {
        confirmMoveReceiptAccountButton.textContent = originalText;
        updateMoveReceiptActionState();
    }
}

async function removeReceiptFromAccountAsync(receiptId: string, accountId: string): Promise<void> {
    await removeReceiptFromAccountApi(receiptId, accountId, forgeryToken);

    pageState.receipts = removeReceiptAccountLinkCore(pageState.receipts, receiptId, accountId);
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
