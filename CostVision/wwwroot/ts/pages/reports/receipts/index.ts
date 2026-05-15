import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { clearElement, requireElementById, requireInputById, requireSelectById } from "../../../shared/dom.js";
import { formatMoneyRub, formatRuNumber, normalizeSingleLineTextValue } from "../../../shared/formatters.js";
import { deleteReceiptApi, loadAvailableAccountsApi, loadReceiptsApi, moveReceiptToAccountApi, openReceiptApi, refreshReceiptApi, removeReceiptFromAccountApi } from "./api.js";
import { applyReceiptFilters as applyReceiptFiltersCore } from "./filters.js";
import { canEditAccount } from "./accountActions.js";
import { removeReceiptFromCache } from "./state.js";
import { updateReceiptsSummary } from "./render.js";
import { AvailableAccountDto, MoveReceiptAccountAction, PendingDeleteAction, ReceiptAccountDto, ReceiptDateRange, ReceiptDto, MoveReceiptTargetAccount } from "./types.js";

// Глобальные переменные для страницы списка чеков
let listContainer;
let modalElement;
let detailsList;
let modalHeader;
let modalTotal;
let bootstrapModal;
let moveReceiptAccountModalElement;
let moveReceiptAccountBootstrapModal;
let moveReceiptSourceAccountElement;
let moveReceiptTargetAccountSelect;
let moveReceiptAccountAlertElement;
let confirmMoveReceiptAccountButton;
let removeReceiptFromAccountButton;
let forgeryToken;
let dateFromInput;
let dateToInput;
let applyFilterButton;
let receiptPeriodPresetSelect;
let receiptsCountElement;
let receiptsSumElement;

// Для удаления
let deleteModalElement;
let deleteBootstrapModal;
let deleteConfirmButton;
let deleteModalTitleElement;
let deleteModalMessageElement;
let deleteModalReceiptInfoElement;
let pendingDeleteReceiptId = null;
let pendingDeleteCardElement = null;
let pendingDeleteAction = null;
let preserveMoveReceiptAccountModalStateOnHide = false;
let shouldRestoreMoveReceiptAccountModalAfterDeleteConfirmation = false;

let receiptSearchInput;
let receiptSearchModeSelect;
let receiptAccountFilterSelect;
let receiptAccountFilterError;

let receiptsCache = [];
let availableAccountsCache = [];
let searchDebounceTimerId = null;
let pendingReceiptAccountAction = null;

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
async function initListOfChecksPage() {
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
    forgeryToken = getRequestVerificationToken();

    if (modalElement) {
        bootstrapModal = new bootstrap.Modal(modalElement);

        // Убирать фокус из модалки при закрытии, чтобы избежать предупреждения aria-hidden
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
        moveReceiptAccountBootstrapModal = new bootstrap.Modal(moveReceiptAccountModalElement);

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

    if (deleteModalElement) {
        deleteBootstrapModal = new bootstrap.Modal(deleteModalElement);

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

    if (receiptSearchInput) {
        receiptSearchInput.addEventListener('input', onSearchChanged);
    }

    if (receiptSearchModeSelect) {
        receiptSearchModeSelect.addEventListener('change', onSearchChanged);
    }

    if (receiptAccountFilterSelect) {
        receiptAccountFilterSelect.addEventListener('change', onSearchChanged);
    }
    if (receiptPeriodPresetSelect) {
        receiptPeriodPresetSelect.addEventListener('change', onReceiptPeriodPresetChanged);
    }

    setCurrentMonthPeriod();

    await loadAvailableAccountsAsync();
    await loadReceiptList();
}

function onReceiptPeriodPresetChanged() {
    applySelectedReceiptPeriodPreset();
}

function applySelectedReceiptPeriodPreset() {
    if (!receiptPeriodPresetSelect) {
        const currentMonthRange = getDateRangeByPeriodPreset('currentMonth', new Date());
        setReceiptDateRange(currentMonthRange.dateFrom, currentMonthRange.dateTo);
        return;
    }

    const periodPreset = receiptPeriodPresetSelect.value || 'currentMonth';
    const range = getDateRangeByPeriodPreset(periodPreset, new Date());

    setReceiptDateRange(range.dateFrom, range.dateTo);
}

function setCurrentMonthPeriod() {
    if (receiptPeriodPresetSelect) {
        receiptPeriodPresetSelect.value = 'currentMonth';
    }

    applySelectedReceiptPeriodPreset();
}

function getDateRangeByPeriodPreset(periodPreset, now) {
    const currentDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    if (periodPreset === 'currentDay') {
        return {
            dateFrom: currentDate,
            dateTo: currentDate
        };
    }

    if (periodPreset === 'currentWeek') {
        const dayOfWeek = currentDate.getDay();
        const daysFromMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
        const dateFrom = new Date(currentDate);
        dateFrom.setDate(currentDate.getDate() - daysFromMonday);

        const dateTo = new Date(dateFrom);
        dateTo.setDate(dateFrom.getDate() + 6);

        return {
            dateFrom: dateFrom,
            dateTo: dateTo
        };
    }

    if (periodPreset === 'currentYear') {
        return {
            dateFrom: new Date(currentDate.getFullYear(), 0, 1),
            dateTo: new Date(currentDate.getFullYear(), 11, 31)
        };
    }

    return {
        dateFrom: new Date(currentDate.getFullYear(), currentDate.getMonth(), 1),
        dateTo: new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0)
    };
}

function setReceiptDateRange(dateFrom, dateTo) {
    if (!dateFromInput || !dateToInput) {
        return;
    }

    dateFromInput.value = formatDateForQuery(dateFrom);
    dateToInput.value = formatDateForQuery(dateTo);
}

// Обработчик клика по карточкам чеков
function onReceiptListClick(event) {
    const target = event.target;
    if (!target) {
        return;
    }

    const openButton = target.closest('[data-action="open"]');
    const refreshButton = target.closest('[data-action="refresh"]');
    const deleteButton = target.closest('[data-action="delete"]');
    const accountButton = target.closest('[data-action="edit-account-link"]');

    if (openButton) {
        const card = openButton.closest('.card');
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
        const card = refreshButton.closest('.card');
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
        const card = deleteButton.closest('.card');
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

// Собрать query-параметры для dateFrom/dateTo
function buildDateRangeQuery() {
    if (!dateFromInput || !dateToInput) {
        return '';
    }

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

// Загрузить список чеков (ожидается массив DTO)
async function loadReceiptList() {
    try {
        const rangeQuery = buildDateRangeQuery();
        receiptsCache = await loadReceiptsApi(rangeQuery, forgeryToken);
        renderReceiptList(applyReceiptFilters(receiptsCache));
    } catch (error) {
        console.error(error);
        alert('Ошибка при получении списка чеков.');
    }
}

async function loadAvailableAccountsAsync() {
    if (!receiptAccountFilterSelect) {
        throw new Error('Не найден фильтр счетов.');
    }

    receiptAccountFilterSelect.disabled = true;
    renderReceiptAccountFilterLoading();

    try {
        availableAccountsCache = (await loadAvailableAccountsApi(forgeryToken)).map(normalizeAvailableAccount);

        renderReceiptAccountFilterOptions(availableAccountsCache);
        hideReceiptAccountFilterError();
        receiptAccountFilterSelect.disabled = false;
    } catch (error) {
        renderReceiptAccountFilterError(error);
        throw error;
    }
}

function normalizeAvailableAccount(dto) {
    if (!dto) {
        throw new Error('Счёт недоступен.');
    }

    const id = dto.id || '';
    const name = dto.name || '';
    const colorHex = normalizeReceiptAccountColorHex(dto.colorHex);
    const accessRole = dto.accessRole ?? 0;
    const canManage = dto.canManage === true;

    if (!id || !name) {
        throw new Error('Счёт получен без обязательных полей.');
    }

    return {
        id: id,
        name: name,
        colorHex: colorHex,
        accessRole: Number(accessRole),
        canManage: canManage
    };
}

function renderReceiptAccountFilterLoading() {
    if (!receiptAccountFilterSelect) {
        return;
    }

    receiptAccountFilterSelect.replaceChildren();

    const option = document.createElement('option');
    option.value = '';
    option.textContent = 'Загрузка счетов...';
    receiptAccountFilterSelect.appendChild(option);
}

function renderReceiptAccountFilterOptions(accounts) {
    if (!receiptAccountFilterSelect) {
        return;
    }

    const currentValue = receiptAccountFilterSelect.value || '';
    receiptAccountFilterSelect.replaceChildren();

    const allOption = document.createElement('option');
    allOption.value = '';
    allOption.textContent = 'Все счета';
    receiptAccountFilterSelect.appendChild(allOption);

    for (const account of accounts) {
        const option = document.createElement('option');
        option.value = account.id;
        option.textContent = account.name;
        receiptAccountFilterSelect.appendChild(option);
    }

    if (currentValue && accounts.some(function (account) { return account.id === currentValue; })) {
        receiptAccountFilterSelect.value = currentValue;
    }
}

function renderReceiptAccountFilterError(error) {
    renderReceiptAccountFilterOptions([]);

    if (!receiptAccountFilterSelect) {
        return;
    }

    receiptAccountFilterSelect.disabled = true;

    if (!receiptAccountFilterError) {
        return;
    }

    receiptAccountFilterError.textContent = error && error.message ? error.message : 'Не удалось загрузить список счетов.';
    receiptAccountFilterError.classList.remove('d-none');
}

function hideReceiptAccountFilterError() {
    if (!receiptAccountFilterError) {
        return;
    }

    receiptAccountFilterError.textContent = '';
    receiptAccountFilterError.classList.add('d-none');
}

// Открыть чек (ожидается один ReceiptDto с Items)
async function openReceipt(receiptId) {
    try {
        const data = await openReceiptApi(receiptId, forgeryToken);
        renderReceiptDetails(data);
    } catch (error) {
        console.error(error);
        alert('Ошибка при получении деталей чека.');
    }
}

function renderReceiptList(list) {
    if (!listContainer) return;

    clearElement(listContainer);
    updateReceiptSummary(list);

    for (const r of list) {
        const card = buildReceiptCard(r);
        listContainer.appendChild(card);
    }
}

function updateReceiptSummary(list) {
    if (!receiptsCountElement || !receiptsSumElement) {
        throw new Error('Элементы сводки чеков не найдены.');
    }

    updateReceiptsSummary(receiptsCountElement, receiptsSumElement, list, formatCurrency);
}

// Обновить чек (ожидается один ReceiptDto без Items)
async function refreshReceipt(receiptId, cardElement, buttonElement) {
    const originalText = buttonElement.textContent;
    buttonElement.disabled = true;
    buttonElement.textContent = 'Обновление...';

    try {
        const data = await refreshReceiptApi(receiptId, forgeryToken);
        updateCardFromDto(cardElement, data);
    } catch (error) {
        console.error(error);
        alert(error?.message ?? error);
    } finally {
        buttonElement.disabled = false;
        buttonElement.textContent = originalText;
    }
}

// Открыть модальное окно подтверждения удаления
function openDeleteReceiptModal(receiptId, cardElement) {
    if (!deleteBootstrapModal || !deleteModalElement) {
        return;
    }

    const receipt = findReceiptById(receiptId);
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
async function onConfirmDeleteReceipt() {
    if (!pendingDeleteAction) {
        return;
    }

    if (!deleteConfirmButton) {
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

        renderReceiptList(applyReceiptFilters(receiptsCache));

        if (deleteBootstrapModal) {
            deleteBootstrapModal.hide();
        }

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
function buildReceiptCard(r) {
    const card = document.createElement('div');
    card.classList.add('card', 'shadow-sm');
    card.setAttribute('data-receipt-id', r.id);

    const cardBody = document.createElement('div');
    cardBody.classList.add('card-body');

    const topRow = document.createElement('div');
    topRow.classList.add('d-flex', 'flex-column', 'flex-md-row', 'align-items-start', 'gap-2');

    const leftDiv = document.createElement('div');
    leftDiv.classList.add('me-md-3', 'flex-grow-1');
    leftDiv.style.minWidth = '0';

    const titleDiv = document.createElement('div');
    titleDiv.classList.add('fw-semibold', 'mb-1');

    let dateText = '';
    if (r.dateTime) {
        const date = new Date(r.dateTime);
        dateText =
            date.toLocaleDateString('ru-RU') + ' ' +
            date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
    }

    const place = normalizeSingleLineText(r.retailPlace);
    titleDiv.textContent = dateText + ' — ' + place;

    leftDiv.appendChild(titleDiv);

    const addrDiv = document.createElement('div');
    addrDiv.classList.add('text-muted', 'small');

    if (r.retailPlaceAddress) {
        addrDiv.textContent = r.retailPlaceAddress;
    } else {
        addrDiv.textContent = '';
        addrDiv.style.display = 'none';
    }

    leftDiv.appendChild(addrDiv);

    const accountDiv = document.createElement('div');
    accountDiv.classList.add('small', 'mt-1', 'd-flex', 'align-items-start', 'gap-2', 'flex-wrap');
    accountDiv.setAttribute('data-role', 'receipt-accounts');

    const receiptAccounts = getReceiptAccounts(r);

    if (receiptAccounts.length > 0) {
        renderReceiptAccountBadges(accountDiv, r);
    } else {
        accountDiv.style.display = 'none';
    }

    leftDiv.appendChild(accountDiv);

    const rightDiv = document.createElement('div');
    rightDiv.classList.add('d-flex', 'flex-column', 'align-items-start', 'align-items-md-end', 'ms-md-auto', 'gap-2');

    const totalDiv = document.createElement('div');
    totalDiv.classList.add('text-start', 'text-md-end');

    const totalSpan = document.createElement('div');
    totalSpan.classList.add('fw-bold');
    totalSpan.textContent = formatCurrency(r.totalSum);

    totalDiv.appendChild(totalSpan);

    const btnGroup = document.createElement('div');
    btnGroup.classList.add('d-flex', 'gap-1', 'flex-wrap', 'w-100', 'justify-content-start', 'justify-content-md-end');

    const openBtn = document.createElement('button');
    openBtn.type = 'button';
    openBtn.classList.add('btn', 'btn-sm', 'btn-outline-primary', 'flex-grow-1', 'flex-md-grow-0');
    openBtn.setAttribute('data-action', 'open');
    openBtn.textContent = 'Открыть';

    const refreshBtn = document.createElement('button');
    refreshBtn.type = 'button';
    refreshBtn.classList.add('btn', 'btn-sm', 'btn-outline-secondary', 'flex-grow-1', 'flex-md-grow-0');
    refreshBtn.setAttribute('data-action', 'refresh');
    refreshBtn.textContent = 'Обновить данные';

    let deleteBtn = null;

    if (receiptAccounts.length === 0) {
        deleteBtn = document.createElement('button');
        deleteBtn.type = 'button';
        deleteBtn.classList.add('btn', 'btn-sm', 'btn-outline-danger', 'flex-grow-1', 'flex-md-grow-0');
        deleteBtn.setAttribute('data-action', 'delete');
        deleteBtn.textContent = 'Удалить';
    }

    btnGroup.appendChild(openBtn);
    btnGroup.appendChild(refreshBtn);

    rightDiv.appendChild(totalDiv);
    rightDiv.appendChild(btnGroup);

    topRow.appendChild(leftDiv);
    topRow.appendChild(rightDiv);
    cardBody.appendChild(topRow);

    if (deleteBtn) {
        btnGroup.appendChild(deleteBtn);
    }

    card.appendChild(cardBody);

    return card;
}

// Отрисовать детали чека в модальном окне
function renderReceiptDetails(data) {
    if (!detailsList || !modalHeader || !modalTotal || !bootstrapModal) {
        return;
    }

    clearElement(detailsList);

    if (Array.isArray(data.items)) {
        data.items.forEach(function (item) {
            const row = document.createElement('div');
            row.classList.add('border', 'rounded', 'p-2', 'd-flex', 'justify-content-between', 'align-items-center');

            const left = document.createElement('div');
            left.classList.add('me-3');

            const nameDiv = document.createElement('div');
            nameDiv.classList.add('fw-semibold');
            nameDiv.textContent = item.name;

            const metaDiv = document.createElement('div');
            metaDiv.classList.add('text-muted', 'small');
            metaDiv.textContent = formatNumber(item.quantity) + ' × ' + formatCurrency(item.price);

            left.appendChild(nameDiv);
            left.appendChild(metaDiv);

            const right = document.createElement('div');
            right.classList.add('text-end', 'fw-bold');
            right.textContent = formatCurrency(item.sum);

            row.appendChild(left);
            row.appendChild(right);

            detailsList.appendChild(row);
        });
    }

    const headerParts = [];
    if (data.dateTime) {
        const date = new Date(data.dateTime);
        headerParts.push({ label: 'Время чека:', value: date.toLocaleString('ru-RU') });
    }
    if (data.retailPlace) {
        headerParts.push({ label: 'Магазин:', value: data.retailPlace });
    }
    if (data.retailPlaceAddress) {
        headerParts.push({ label: 'Адрес:', value: data.retailPlaceAddress });
    }
    const accountNamesText = getReceiptAccountNamesText(data);
    if (accountNamesText) {
        headerParts.push({ label: 'Счета:', value: accountNamesText });
    }

    clearElement(modalHeader);
    headerParts.forEach(function (p) {
        appendReceiptHeaderPart(modalHeader, p.label, p.value);
    });

    // Под адресом: ФН, ФД, ФП
    if (data.fiscalDriveNumber) {
        appendReceiptHeaderPart(modalHeader, 'ФН:', data.fiscalDriveNumber);
    }
    if (data.fiscalDocumentNumber) {
        appendReceiptHeaderPart(modalHeader, 'ФД:', data.fiscalDocumentNumber);
    }
    if (data.fiscalSign) {
        appendReceiptHeaderPart(modalHeader, 'ФП:', data.fiscalSign);
    }

    if (typeof data.totalSum === 'number') {
        modalTotal.textContent = 'Итого: ' + formatCurrency(data.totalSum);
    } else {
        modalTotal.textContent = '';
    }

    if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }
    bootstrapModal.show();
}

function appendReceiptHeaderPart(container, label, value) {
    const row = document.createElement('div');
    const labelElement = document.createElement('strong');
    labelElement.textContent = label;
    row.append(labelElement, document.createTextNode(' ' + value));
    container.appendChild(row);
}

// Обновить существующую карточку чека
function updateCardFromDto(cardElement, receiptDto) {
    cardElement.setAttribute('data-receipt-id', receiptDto.id);

    const bodyElement = cardElement.querySelector('.card-body');
    if (!bodyElement) {
        return;
    }

    const datePlaceElement = bodyElement.querySelector('.fw-semibold');
    const addressElement = bodyElement.querySelector('.text-muted.small');
    const accountElement = bodyElement.querySelector('[data-role="receipt-accounts"]');
    const totalElement = bodyElement.querySelector('.fw-bold');

    if (datePlaceElement && receiptDto.dateTime) {
        const date = new Date(receiptDto.dateTime);
        const formatted =
            date.toLocaleDateString('ru-RU') + ' ' +
            date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
        const place = normalizeSingleLineText(receiptDto.retailPlace);
        datePlaceElement.textContent = formatted + ' — ' + place;
    }

    if (addressElement) {
        if (receiptDto.retailPlaceAddress) {
            addressElement.textContent = receiptDto.retailPlaceAddress;
            addressElement.style.display = '';
        } else {
            addressElement.textContent = '';
            addressElement.style.display = 'none';
        }
    }

    if (accountElement) {
        accountElement.replaceChildren();
        accountElement.className = 'small mt-1 d-flex align-items-start gap-2 flex-wrap';

        if (getReceiptAccounts(receiptDto).length > 0) {
            renderReceiptAccountBadges(accountElement, receiptDto);
            accountElement.style.display = '';
        } else {
            accountElement.style.display = 'none';
        }
    }

    if (totalElement && typeof receiptDto.totalSum === 'number') {
        totalElement.textContent = formatCurrency(receiptDto.totalSum);
    }
}

// Форматирование даты для query (YYYY-MM-DD)
function formatDateForQuery(date) {
    const year = date.getFullYear();
    const month = (date.getMonth() + 1).toString().padStart(2, '0');
    const day = date.getDate().toString().padStart(2, '0');
    return year + '-' + month + '-' + day;
}

// Форматирование числа
function formatNumber(value) {
    if (typeof value !== 'number') {
        return value;
    }
    return formatRuNumber(value);
}

// Форматирование валюты
function formatCurrency(value) {
    if (typeof value !== 'number') {
        return value;
    }
    return formatMoneyRub(value);
}

function fillDeleteReceiptModal(title, message, receipt) {
    if (deleteModalTitleElement) {
        deleteModalTitleElement.textContent = title;
    }

    if (deleteModalMessageElement) {
        deleteModalMessageElement.textContent = message;
    }

    if (deleteModalReceiptInfoElement) {
        deleteModalReceiptInfoElement.textContent = buildDeleteReceiptInfoText(receipt);
    }
}

function buildDeleteReceiptInfoText(receipt) {
    if (!receipt) {
        return '';
    }

    const parts = [];
    if (receipt.dateTime) {
        const date = new Date(receipt.dateTime);
        parts.push(formatDeleteReceiptDateTime(date));
    }

    if (typeof receipt.totalSum === 'number') {
        parts.push(formatCurrency(receipt.totalSum));
    }

    return parts.join(', ');
}

function formatDeleteReceiptDateTime(date) {
    return date.toLocaleDateString('ru-RU') + ' ' + date.toLocaleTimeString('ru-RU', {
        hour: '2-digit',
        minute: '2-digit'
    });
}

async function deleteReceiptAsync(receiptId) {
    await deleteReceiptApi(receiptId, forgeryToken);
    receiptsCache = removeReceiptFromCache(receiptsCache, receiptId);

    if (pendingDeleteCardElement && pendingDeleteCardElement.parentNode) {
        pendingDeleteCardElement.parentNode.removeChild(pendingDeleteCardElement);
    }
}

function openRemoveReceiptFromAccountConfirmation() {
    if (!pendingReceiptAccountAction || !deleteBootstrapModal || !moveReceiptAccountBootstrapModal) {
        return;
    }

    if (!canRemoveReceiptFromSelectedAccount()) {
        updateMoveReceiptActionState();
        return;
    }

    const receipt = findReceiptByAccountReceiptId(pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
    if (!receipt) {
        alert('Чек не найден в текущем списке.');
        return;
    }

    pendingDeleteAction = {
        type: DELETE_ACTION_REMOVE_FROM_ACCOUNT,
        receiptId: pendingReceiptAccountAction.receiptId,
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

function normalizeSingleLineText(value) {
    return normalizeSingleLineTextValue(value);
}

function onSearchChanged() {
    if (searchDebounceTimerId) {
        clearTimeout(searchDebounceTimerId);
    }

    searchDebounceTimerId = setTimeout(function () {
        renderReceiptList(applyReceiptFilters(receiptsCache));
    }, 250);
}

function applyReceiptFilters(list) {
    const query = getSearchQuery();
    const mode = getSearchMode();
    const accountId = getSelectedReceiptAccountId();
    return applyReceiptFiltersCore(list, query, mode, accountId);
}

function getSearchQuery() {
    if (!receiptSearchInput) return '';
    const v = (receiptSearchInput.value || '').trim();
    return v;
}

function getSearchMode() {
    if (!receiptSearchModeSelect) return 'all';
    return receiptSearchModeSelect.value || 'all';
}

function getSelectedReceiptAccountId() {
    if (!receiptAccountFilterSelect) return '';
    return receiptAccountFilterSelect.value || '';
}

function getReceiptAccounts(receipt) {
    if (receipt && Array.isArray(receipt.accounts)) {
        return receipt.accounts
            .map(function (account) {
                if (!account) {
                    throw new Error('Счёт чека недоступен.');
                }

                const id = account.id || '';
                const receiptId = account.receiptId || '';
                const name = account.name || '';
                const colorHex = normalizeReceiptAccountColorHex(account.colorHex);
                const accessRole = account.accessRole ?? null;
                const canEditReceipt = account.canEditReceipt === true;
                if (!id || !name || !receiptId) {
                    throw new Error('Счёт чека получен без обязательных полей.');
                }

                return {
                    id: id,
                    receiptId: receiptId,
                    name: name,
                    colorHex: colorHex,
                    accessRole: accessRole !== null ? Number(accessRole) : null,
                    canEditReceipt: canEditReceipt
                };
            })
            .filter(function (account) { return account !== null; });
    }

    throw new Error('Чек получен без списка счетов.');
}

function getReceiptAccountNamesText(receipt) {
    const names = getReceiptAccounts(receipt)
        .map(function (account) { return account.name; })
        .filter(function (name, index, items) {
            return !!name && items.indexOf(name) === index;
        });

    return names.join(', ');
}

function renderReceiptAccountBadges(container, receipt) {
    if (!container) {
        return;
    }

    container.replaceChildren();

    const accounts = getReceiptAccounts(receipt);
    for (const account of accounts) {
        const badge = document.createElement('button');
        badge.type = 'button';
        badge.className = 'btn btn-sm px-2 py-1 rounded-pill';
        badge.setAttribute('data-action', 'edit-account-link');
        badge.setAttribute('data-account-receipt-id', account.receiptId);
        badge.setAttribute('data-account-id', account.id);
        badge.textContent = account.name;
        badge.style.backgroundColor = 'transparent';
        badge.style.color = '#212529';
        badge.style.border = '2px solid ' + account.colorHex;
        badge.style.transition = 'background-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease';
        badge.disabled = !account.canEditReceipt;

        if (account.canEditReceipt) {
            badge.addEventListener('mouseenter', function () {
                badge.style.backgroundColor = account.colorHex + '14';
                badge.style.boxShadow = '0 0 0 0.2rem ' + account.colorHex + '22';
                badge.style.transform = 'translateY(-1px)';
            });

            badge.addEventListener('mouseleave', function () {
                badge.style.backgroundColor = 'transparent';
                badge.style.boxShadow = 'none';
                badge.style.transform = 'translateY(0)';
            });
        } else {
            badge.title = 'Чужой расшаренный счёт недоступен для изменения чека.';
            badge.style.opacity = '0.65';
            badge.style.cursor = 'not-allowed';
        }

        container.appendChild(badge);
    }
}

function getAvailableTargetAccounts(receipt, sourceAccountId) {
    return availableAccountsCache
        .filter(function (account) {
            return account && account.id !== sourceAccountId && canEditAccount(account);
        })
        .map(function (account) {
            const reason = getMoveReceiptTargetDisabledReason(receipt, account);
            return {
                id: account.id,
                name: account.name,
                isDisabled: !!reason,
                disabledReason: reason
            };
        })
        .sort(function (left, right) {
            if (left.isDisabled === right.isDisabled) {
                return left.name.localeCompare(right.name, 'ru');
            }

            return left.isDisabled ? 1 : -1;
        });
}

function getMoveReceiptTargetDisabledReason(receipt, account) {
    if (!account) {
        return 'Счёт назначения недоступен.';
    }

    if (receiptHasAccount(receipt, account.id)) {
        return 'Этот чек уже привязан к выбранному счёту.';
    }

    if (!canEditAccount(account)) {
        return 'Недостаточно прав для переноса чека в выбранный счёт.';
    }

    return '';
}

function openMoveReceiptAccountModal(receiptId, sourceAccountId) {
    if (!moveReceiptAccountBootstrapModal || !moveReceiptSourceAccountElement || !moveReceiptTargetAccountSelect) {
        return;
    }

    const receipt = findReceiptByAccountReceiptId(receiptId, sourceAccountId);
    if (!receipt) {
        alert('Чек не найден в текущем списке.');
        return;
    }

    const sourceAccount = findReceiptAccount(receipt, sourceAccountId, receiptId);
    if (!sourceAccount) {
        alert('Связь со счётом не найдена.');
        return;
    }

    if (!sourceAccount.canEditReceipt) {
        alert('Недостаточно прав для изменения чека в выбранном счёте.');
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

function renderMoveReceiptTargetOptions(receipt, sourceAccountId) {
    if (!moveReceiptTargetAccountSelect) {
        return;
    }

    const selectedValue = moveReceiptTargetAccountSelect.value || '';
    moveReceiptTargetAccountSelect.replaceChildren();

    const accounts = getAvailableTargetAccounts(receipt, sourceAccountId);
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

function onMoveReceiptTargetAccountChanged() {
    updateMoveReceiptActionState();
}

function updateMoveReceiptActionState() {
    const canRemove = canRemoveReceiptFromSelectedAccount();
    const selectedOption = getSelectedMoveReceiptTargetOption();
    const hasOtherAccounts = !!moveReceiptTargetAccountSelect && moveReceiptTargetAccountSelect.options.length > 0;
    const canMove = hasOtherAccounts && !!selectedOption && !selectedOption.disabled;

    if (removeReceiptFromAccountButton) {
        removeReceiptFromAccountButton.disabled = !canRemove;
    }

    if (confirmMoveReceiptAccountButton) {
        confirmMoveReceiptAccountButton.disabled = !canMove;
    }

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

function getUnavailableMoveReceiptReason() {
    if (!moveReceiptTargetAccountSelect) {
        return '';
    }

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

async function onConfirmMoveReceiptToAccount() {
    if (!pendingReceiptAccountAction || !confirmMoveReceiptAccountButton) {
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

    if (removeReceiptFromAccountButton) {
        removeReceiptFromAccountButton.disabled = true;
    }

    try {
        await moveReceiptToAccountApi(pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId, selectedOption.value, forgeryToken);

        replaceReceiptAccountLink(
            pendingReceiptAccountAction.receiptId,
            pendingReceiptAccountAction.sourceAccountId,
            selectedOption.value
        );

        closeMoveReceiptAccountModal();
        renderReceiptList(applyReceiptFilters(receiptsCache));
    } catch (error) {
        console.error(error);
        showMoveReceiptAccountAlert(error && error.message ? error.message : 'Не удалось перенести чек в другой счёт.');
    } finally {
        confirmMoveReceiptAccountButton.textContent = originalText;
        updateMoveReceiptActionState();
    }
}

async function removeReceiptFromAccountAsync(receiptId, accountId) {
    await removeReceiptFromAccountApi(receiptId, accountId, forgeryToken);

    removeReceiptAccountLink(receiptId, accountId);
}

function closeMoveReceiptAccountModal() {
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

    if (moveReceiptAccountBootstrapModal) {
        moveReceiptAccountBootstrapModal.hide();
    }
}

function findAvailableAccountById(accountId) {
    if (!accountId) {
        return null;
    }

    for (const account of availableAccountsCache) {
        if (account && account.id === accountId) {
            return account;
        }
    }

    return null;
}

function findReceiptById(receiptId) {
    for (const receipt of receiptsCache) {
        if (receipt && receipt.id === receiptId) {
            return receipt;
        }
    }

    return null;
}

function findReceiptByAccountReceiptId(receiptId, accountId) {
    if (!receiptId || !accountId) {
        return null;
    }

    for (const receipt of receiptsCache) {
        if (!receipt) {
            continue;
        }

        const account = findReceiptAccount(receipt, accountId, receiptId);
        if (account) {
            return receipt;
        }
    }

    return null;
}

function findReceiptAccount(receipt, accountId, receiptId) {
    const accounts = getReceiptAccounts(receipt);

    for (const account of accounts) {
        if (!account || account.id !== accountId) {
            continue;
        }

        if (!receiptId || account.receiptId === receiptId) {
            return account;
        }
    }

    return null;
}

function getSelectedMoveReceiptTargetOption() {
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

function canRemoveReceiptFromSelectedAccount() {
    if (!pendingReceiptAccountAction) {
        return false;
    }

    const receipt = findReceiptByAccountReceiptId(pendingReceiptAccountAction.receiptId, pendingReceiptAccountAction.sourceAccountId);
    if (!receipt) {
        return false;
    }

    const sourceAccount = findReceiptAccount(receipt, pendingReceiptAccountAction.sourceAccountId, pendingReceiptAccountAction.receiptId);
    if (!sourceAccount) {
        return false;
    }

    return sourceAccount.canEditReceipt === true;
}

function replaceReceiptAccountLink(receiptId, sourceAccountId, targetAccountId) {
    const receipt = findReceiptByAccountReceiptId(receiptId, sourceAccountId);
    if (!receipt) {
        throw new Error('Не удалось обновить чек после переноса.');
    }

    const targetAccount = findAvailableAccountById(targetAccountId);
    if (!targetAccount) {
        throw new Error('Счёт назначения не найден в текущем списке.');
    }

    const accounts = getReceiptAccounts(receipt);
    const updatedAccounts = [];

    for (const account of accounts) {
        if (!account || account.id === targetAccountId) {
            continue;
        }

        if (account.id === sourceAccountId) {
            updatedAccounts.push({
                id: targetAccount.id,
                receiptId: receiptId,
                name: targetAccount.name,
                colorHex: targetAccount.colorHex,
                accessRole: targetAccount.accessRole,
                canEditReceipt: true
            });
            continue;
        }

        updatedAccounts.push(account);
    }

    applyReceiptAccounts(receipt, updatedAccounts);
}

function removeReceiptAccountLink(receiptId, accountId) {
    const receipt = findReceiptByAccountReceiptId(receiptId, accountId);
    if (!receipt) {
        throw new Error('Не удалось обновить чек после удаления связи.');
    }

    const updatedAccounts = getReceiptAccounts(receipt).filter(function (account) {
        return account && !(account.id === accountId && account.receiptId === receiptId);
    });

    if (updatedAccounts.length === 0) {
        receiptsCache = receiptsCache.filter(function (item) {
            return item && item !== receipt;
        });
        return;
    }

    applyReceiptAccounts(receipt, updatedAccounts);
}

function applyReceiptAccounts(receipt, accounts) {
    const normalizedAccounts = accounts
        .filter(function (account) { return !!account; })
        .sort(function (left, right) { return left.name.localeCompare(right.name, 'ru'); });

    receipt.accounts = normalizedAccounts.map(function (account) {
        return {
            id: account.id,
            receiptId: account.receiptId,
            name: account.name,
            colorHex: account.colorHex,
            accessRole: account.accessRole,
            canEditReceipt: account.canEditReceipt === true
        };
    });

    const firstAccount = normalizedAccounts.length > 0 ? normalizedAccounts[0] : null;
    const firstEditableAccount = normalizedAccounts.find(function (account) { return account.canEditReceipt === true; }) || firstAccount;
    receipt.id = firstEditableAccount ? firstEditableAccount.receiptId : receipt.id;
    receipt.accountId = firstAccount ? firstAccount.id : null;
    receipt.accountName = firstAccount ? firstAccount.name : '';
}

function showMoveReceiptAccountAlert(message) {
    if (!moveReceiptAccountAlertElement) {
        return;
    }

    moveReceiptAccountAlertElement.textContent = message || '';
    moveReceiptAccountAlertElement.classList.toggle('d-none', !message);
}

function hideMoveReceiptAccountAlert() {
    showMoveReceiptAccountAlert('');
}

function normalizeReceiptAccountColorHex(colorHex) {
    const value = (colorHex || '').toString().trim().toUpperCase();
    if (!/^#[0-9A-F]{6}$/.test(value)) {
        throw new Error('Некорректный HEX-цвет счёта.');
    }

    return value;
}

function receiptHasAccount(receipt, accountId) {
    if (!accountId) {
        return true;
    }

    return getReceiptAccounts(receipt).some(function (account) {
        return account.id === accountId;
    });
}
