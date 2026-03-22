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
let pendingDeleteReceiptId = null;
let pendingDeleteCardElement = null;

let receiptSearchInput;
let receiptSearchModeSelect;
let receiptAccountFilterSelect;
let receiptAccountFilterError;

let receiptsCache = [];
let availableAccountsCache = [];
let searchDebounceTimerId = null;
let pendingReceiptAccountAction = null;

// Инициализация после загрузки DOM
document.addEventListener('DOMContentLoaded', function () {
    initListOfChecksPage().catch(function (error) {
        console.error(error);
        alert(error && error.message ? error.message : 'Ошибка при инициализации страницы чеков.');
    });
});

// Инициализировать страницу списка чеков
async function initListOfChecksPage() {
    listContainer = document.getElementById('receipt-list');
    if (!listContainer) {
        return;
    }

    modalElement = document.getElementById('receiptDetailsModal');
    detailsList = document.getElementById('receipt-details-list');
    modalHeader = document.getElementById('receipt-details-header');
    modalTotal = document.getElementById('receipt-details-total');
    moveReceiptAccountModalElement = document.getElementById('moveReceiptAccountModal');
    moveReceiptSourceAccountElement = document.getElementById('moveReceiptSourceAccount');
    moveReceiptTargetAccountSelect = document.getElementById('moveReceiptTargetAccount');
    moveReceiptAccountAlertElement = document.getElementById('moveReceiptAccountAlert');
    confirmMoveReceiptAccountButton = document.getElementById('confirmMoveReceiptAccountBtn');
    removeReceiptFromAccountButton = document.getElementById('removeReceiptFromAccountBtn');
    deleteModalElement = document.getElementById('deleteReceiptModal');
    deleteConfirmButton = document.getElementById('confirmDeleteReceiptBtn');
    applyFilterButton = document.getElementById('applyFilter');
    dateFromInput = document.getElementById('dateFrom');
    dateToInput = document.getElementById('dateTo');
    receiptPeriodPresetSelect = document.getElementById('receiptPeriodPreset');
    receiptsCountElement = document.getElementById('receiptsCount');
    receiptsSumElement = document.getElementById('receiptsSum');
    forgeryToken = getRequestVerificationToken();

    if (modalElement) {
        bootstrapModal = new bootstrap.Modal(modalElement);

        // Убирать фокус из модалки при закрытии, чтобы избежать предупреждения aria-hidden
        modalElement.addEventListener('hide.bs.modal', function () {
            if (document.activeElement && modalElement.contains(document.activeElement)) {
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
            if (document.activeElement && moveReceiptAccountModalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });

        moveReceiptAccountModalElement.addEventListener('hidden.bs.modal', function () {
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
            if (document.activeElement && deleteModalElement.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });
    }

    if (deleteConfirmButton) {
        deleteConfirmButton.addEventListener('click', onConfirmDeleteReceipt);
    }

    if (confirmMoveReceiptAccountButton) {
        confirmMoveReceiptAccountButton.addEventListener('click', onConfirmMoveReceiptToAccount);
    }

    if (removeReceiptFromAccountButton) {
        removeReceiptFromAccountButton.addEventListener('click', onRemoveReceiptFromAccount);
    }

    receiptSearchInput = document.getElementById('receiptSearch');
    receiptSearchModeSelect = document.getElementById('receiptSearchMode');
    receiptAccountFilterSelect = document.getElementById('receiptAccountFilter');
    receiptAccountFilterError = document.getElementById('receiptAccountFilterError');

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
        const receiptId = accountButton.getAttribute('data-receipt-id');
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
        const url = rangeQuery
            ? '?handler=ReceiptList&' + rangeQuery
            : '?handler=ReceiptList';

        const response = await sendJsonRequest(url, 'GET', buildJsonHeaders(forgeryToken), null);
        receiptsCache = Array.isArray(response.data) ? response.data : [];
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
        const response = await sendJsonRequest('?handler=Accounts', 'GET', buildJsonHeaders(forgeryToken), null);
        availableAccountsCache = Array.isArray(response.data)
            ? response.data.map(normalizeAvailableAccount).filter(function (account) { return account !== null; })
            : [];

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
        return null;
    }

    const id = dto.id || dto.Id || '';
    const name = dto.name || dto.Name || '';
    const colorHex = normalizeReceiptAccountColorHex(dto.colorHex ?? dto.ColorHex);
    const accessRole = dto.accessRole ?? dto.AccessRole ?? 0;
    const canManage = dto.canManage === true || dto.CanManage === true;

    if (!id || !name) {
        return null;
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
        const data = await sendJsonRequest(
            '?handler=OpenReceipt',
            'POST',
            buildJsonHeaders(forgeryToken),
            { receiptId: receiptId }
        );

        if (!data || !data.data) {
            return;
        }

        renderReceiptDetails(data.data);
    } catch (error) {
        console.error(error);
        alert('Ошибка при получении деталей чека.');
    }
}

function renderReceiptList(list) {
    if (!listContainer) return;

    listContainer.innerHTML = '';
    updateReceiptsCount(list.length);
    updateReceiptsSum(list);

    for (const r of list) {
        const card = buildReceiptCard(r);
        listContainer.appendChild(card);
    }
}

function updateReceiptsCount(count) {
    if (!receiptsCountElement) {
        return;
    }

    receiptsCountElement.textContent = 'Чеков: ' + count;
}

function updateReceiptsSum(list) {
    if (!receiptsSumElement) {
        return;
    }

    const totalSum = list.reduce(function (sum, receipt) {
        return sum + (typeof receipt.totalSum === 'number' ? receipt.totalSum : 0);
    }, 0);

    receiptsSumElement.textContent = 'Сумма: ' + formatCurrency(totalSum);
}

// Обновить чек (ожидается один ReceiptDto без Items)
async function refreshReceipt(receiptId, cardElement, buttonElement) {
    const originalHtml = buttonElement.innerHTML;
    buttonElement.disabled = true;
    buttonElement.innerHTML = 'Обновление...';

    try {
        const data = await sendJsonRequest(
            '?handler=RefreshReceipt',
            'POST',
            buildJsonHeaders(forgeryToken),
            { receiptId: receiptId }
        );

        if (data && data.data && cardElement) {
            updateCardFromDto(cardElement, data.data);
        }
    } catch (error) {
        console.error(error);
        alert(error?.message ?? error);
    } finally {
        buttonElement.disabled = false;
        buttonElement.innerHTML = originalHtml;
    }
}

// Открыть модальное окно подтверждения удаления
function openDeleteReceiptModal(receiptId, cardElement) {
    if (!deleteBootstrapModal || !deleteModalElement) {
        return;
    }

    pendingDeleteReceiptId = receiptId;
    pendingDeleteCardElement = cardElement;

    // Сбрасывать возможное предыдущее состояние кнопки
    if (deleteConfirmButton) {
        deleteConfirmButton.disabled = false;
        deleteConfirmButton.textContent = 'Удалить';
    }

    deleteBootstrapModal.show();
}

// Обработать подтверждение удаления
async function onConfirmDeleteReceipt() {
    if (!pendingDeleteReceiptId) {
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
        const payload = { receiptId: pendingDeleteReceiptId };

        await sendJsonRequest('?handler=DeleteReceipt', 'POST', buildJsonHeaders(forgeryToken), payload);
        receiptsCache = receiptsCache.filter(function (receipt) {
            return receipt && receipt.id !== pendingDeleteReceiptId;
        });

        // Удалять карточку из DOM
        if (pendingDeleteCardElement && pendingDeleteCardElement.parentNode) {
            pendingDeleteCardElement.parentNode.removeChild(pendingDeleteCardElement);
        }

        renderReceiptList(applyReceiptFilters(receiptsCache));

        // Закрывать модалку
        if (deleteBootstrapModal) {
            deleteBootstrapModal.hide();
        }

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

    const deleteBtn = document.createElement('button');
    deleteBtn.type = 'button';
    deleteBtn.classList.add('btn', 'btn-sm', 'btn-outline-danger', 'flex-grow-1', 'flex-md-grow-0');
    deleteBtn.setAttribute('data-action', 'delete');
    deleteBtn.textContent = 'Удалить';

    btnGroup.appendChild(openBtn);
    btnGroup.appendChild(refreshBtn);

    rightDiv.appendChild(totalDiv);
    rightDiv.appendChild(btnGroup);

    topRow.appendChild(leftDiv);
    topRow.appendChild(rightDiv);
    cardBody.appendChild(topRow);

    const accountDiv = document.createElement('div');
    accountDiv.classList.add('small', 'mt-2', 'd-flex', 'align-items-start', 'gap-2', 'flex-wrap');
    accountDiv.setAttribute('data-role', 'receipt-accounts');

    if (getReceiptAccounts(r).length > 0) {
        renderReceiptAccountBadges(accountDiv, r);
    } else {
        accountDiv.style.display = 'none';
    }

    btnGroup.appendChild(deleteBtn);
    cardBody.appendChild(accountDiv);

    card.appendChild(cardBody);

    return card;
}

// Отрисовать детали чека в модальном окне
function renderReceiptDetails(data) {
    if (!detailsList || !modalHeader || !modalTotal || !bootstrapModal) {
        return;
    }

    detailsList.innerHTML = '';

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

    let headerHtml = '';
    headerParts.forEach(function (p) {
        headerHtml += `<div><strong>${p.label}</strong> ${p.value}</div>`;
    });

    // Под адресом: ФН, ФД, ФП
    if (data.fiscalDriveNumber) {
        headerHtml += `<div><strong>ФН:</strong> ${data.fiscalDriveNumber}</div>`;
    }
    if (data.fiscalDocumentNumber) {
        headerHtml += `<div><strong>ФД:</strong> ${data.fiscalDocumentNumber}</div>`;
    }
    if (data.fiscalSign) {
        headerHtml += `<div><strong>ФП:</strong> ${data.fiscalSign}</div>`;
    }

    modalHeader.innerHTML = headerHtml;

    if (typeof data.totalSum === 'number') {
        modalTotal.textContent = 'Итого: ' + formatCurrency(data.totalSum);
    } else {
        modalTotal.textContent = '';
    }

    if (document.activeElement && typeof document.activeElement.blur === 'function') {
        document.activeElement.blur();
    }
    bootstrapModal.show();
}

// Обновить существующую карточку чека
function updateCardFromDto(cardElement, receiptDto) {
    const bodyElement = cardElement.querySelector('.card-body');
    if (!bodyElement) {
        return;
    }

    const datePlaceElement = bodyElement.querySelector('.fw-semibold');
    const addressElement = bodyElement.querySelector('.text-muted.small.mt-2');
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
        accountElement.className = 'small mt-2 d-flex align-items-start gap-2 flex-wrap';

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
    return value.toLocaleString('ru-RU', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
}

// Форматирование валюты
function formatCurrency(value) {
    if (typeof value !== 'number') {
        return value;
    }
    return value.toLocaleString('ru-RU', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }) + ' ₽';
}

function normalizeSingleLineText(value) {
    return (value || '').toString().replace(/\s+/g, ' ').trim();
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

    let filteredByAccount = list;
    if (accountId) {
        filteredByAccount = [];
        for (const receipt of list) {
            if (receiptHasAccount(receipt, accountId)) {
                filteredByAccount.push(receipt);
            }
        }
    }

    if (!query) {
        return filteredByAccount;
    }

    const lowered = query.toLowerCase();

    const filtered = [];
    for (const r of filteredByAccount) {
        if (receiptMatchesQuery(r, lowered, mode)) {
            filtered.push(r);
        }
    }

    return filtered;
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

function receiptMatchesQuery(r, loweredQuery, mode) {
    const fn = (r.fiscalDriveNumber || '').toString();
    const fd = (r.fiscalDocumentNumber || '').toString();
    const fp = (r.fiscalSign || '').toString();
    const shop = (r.retailPlace || '').toString().toLowerCase();
    const account = getReceiptAccountNamesText(r).toLowerCase();

    if (mode === 'shop') return shop.includes(loweredQuery);
    if (mode === 'fn') return fn.toLowerCase().includes(loweredQuery);
    if (mode === 'fd') return fd.toLowerCase().includes(loweredQuery);
    if (mode === 'fp') return fp.toLowerCase().includes(loweredQuery);

    if (mode === 'sum') {
        return sumMatchesQuery(r.totalSum, loweredQuery);
    }

    // mode === 'all'
    const place = (r.retailPlace || '').toString().toLowerCase();
    const addr = (r.retailPlaceAddress || '').toString().toLowerCase();

    const sumText = (typeof r.totalSum === 'number')
        ? r.totalSum.toString()
        : (r.totalSum || '').toString();

    // “all”: место/адрес/фискальные поля/сумма
    return place.includes(loweredQuery)
        || addr.includes(loweredQuery)
        || account.includes(loweredQuery)
        || fn.toLowerCase().includes(loweredQuery)
        || fd.toLowerCase().includes(loweredQuery)
        || fp.toLowerCase().includes(loweredQuery)
        || sumText.toLowerCase().includes(loweredQuery);
}

function getReceiptAccounts(receipt) {
    if (receipt && Array.isArray(receipt.accounts)) {
        return receipt.accounts
            .map(function (account) {
                if (!account) {
                    return null;
                }

                const id = account.id || account.Id || '';
                const name = account.name || account.Name || '';
                const colorHex = normalizeReceiptAccountColorHex(account.colorHex ?? account.ColorHex);
                const accessRole = account.accessRole ?? account.AccessRole ?? null;
                if (!id || !name) {
                    return null;
                }

                return {
                    id: id,
                    name: name,
                    colorHex: colorHex,
                    accessRole: accessRole !== null ? Number(accessRole) : null
                };
            })
            .filter(function (account) { return account !== null; });
    }

    if (receipt && receipt.accountId && receipt.accountName) {
        const availableAccount = findAvailableAccountById(receipt.accountId);

        return [{
            id: receipt.accountId,
            name: receipt.accountName,
            colorHex: availableAccount ? availableAccount.colorHex : normalizeReceiptAccountColorHex(null),
            accessRole: availableAccount ? availableAccount.accessRole : null
        }];
    }

    return [];
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
        badge.setAttribute('data-receipt-id', receipt.id);
        badge.setAttribute('data-account-id', account.id);
        badge.textContent = account.name;
        badge.style.backgroundColor = 'transparent';
        badge.style.color = '#212529';
        badge.style.border = '2px solid ' + account.colorHex;
        badge.style.transition = 'background-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease';

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

        container.appendChild(badge);
    }
}

function getAvailableTargetAccounts(receipt, sourceAccountId) {
    return availableAccountsCache
        .filter(function (account) {
            return account && account.id !== sourceAccountId;
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
        return 'Нельзя переносить чек в счёт, где у тебя только роль Viewer.';
    }

    return '';
}

function openMoveReceiptAccountModal(receiptId, sourceAccountId) {
    if (!moveReceiptAccountBootstrapModal || !moveReceiptSourceAccountElement || !moveReceiptTargetAccountSelect) {
        return;
    }

    const receipt = findReceiptById(receiptId);
    if (!receipt) {
        alert('Чек не найден в текущем списке.');
        return;
    }

    const sourceAccount = findReceiptAccount(receipt, sourceAccountId);
    if (!sourceAccount) {
        alert('Связь со счётом не найдена.');
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
        const matchingOption = Array.from(moveReceiptTargetAccountSelect.options).find(function (option) {
            return option.value === selectedValue && option.disabled === false;
        });

        moveReceiptTargetAccountSelect.value = matchingOption ? selectedValue : '';
    } else {
        const firstEnabledOption = Array.from(moveReceiptTargetAccountSelect.options).find(function (option) {
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

    const enabledOption = Array.from(moveReceiptTargetAccountSelect.options).find(function (option) {
        return option.value && option.disabled === false;
    });

    if (enabledOption) {
        return '';
    }

    const disabledOption = Array.from(moveReceiptTargetAccountSelect.options).find(function (option) {
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
        await sendJsonRequest(
            '?handler=MoveReceiptToAccount',
            'POST',
            buildJsonHeaders(forgeryToken),
            {
                receiptId: pendingReceiptAccountAction.receiptId,
                sourceAccountId: pendingReceiptAccountAction.sourceAccountId,
                targetAccountId: selectedOption.value
            }
        );

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

async function onRemoveReceiptFromAccount() {
    if (!pendingReceiptAccountAction || !removeReceiptFromAccountButton) {
        return;
    }

    if (!canRemoveReceiptFromSelectedAccount()) {
        updateMoveReceiptActionState();
        return;
    }

    const originalText = removeReceiptFromAccountButton.textContent;
    removeReceiptFromAccountButton.disabled = true;
    removeReceiptFromAccountButton.textContent = 'Удаление...';

    if (confirmMoveReceiptAccountButton) {
        confirmMoveReceiptAccountButton.disabled = true;
    }

    try {
        await sendJsonRequest(
            '?handler=RemoveReceiptFromAccount',
            'POST',
            buildJsonHeaders(forgeryToken),
            {
                receiptId: pendingReceiptAccountAction.receiptId,
                accountId: pendingReceiptAccountAction.sourceAccountId
            }
        );

        removeReceiptAccountLink(
            pendingReceiptAccountAction.receiptId,
            pendingReceiptAccountAction.sourceAccountId
        );

        closeMoveReceiptAccountModal();
        renderReceiptList(applyReceiptFilters(receiptsCache));
    } catch (error) {
        console.error(error);
        showMoveReceiptAccountAlert(error && error.message ? error.message : 'Не удалось удалить чек из счёта.');
    } finally {
        removeReceiptFromAccountButton.textContent = originalText;
        updateMoveReceiptActionState();
    }
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

function findReceiptAccount(receipt, accountId) {
    const accounts = getReceiptAccounts(receipt);

    for (const account of accounts) {
        if (account && account.id === accountId) {
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

    const receipt = findReceiptById(pendingReceiptAccountAction.receiptId);
    if (!receipt) {
        return false;
    }

    const accounts = getReceiptAccounts(receipt);
    if (accounts.length < 2) {
        return false;
    }

    const sourceAccount = findAvailableAccountById(pendingReceiptAccountAction.sourceAccountId);
    return canEditAccount(sourceAccount);
}

function canEditAccount(account) {
    if (!account) {
        return false;
    }

    return account.canManage === true || account.accessRole === 1 || account.accessRole === 2;
}

function replaceReceiptAccountLink(receiptId, sourceAccountId, targetAccountId) {
    const receipt = findReceiptById(receiptId);
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
                name: targetAccount.name,
                colorHex: targetAccount.colorHex,
                accessRole: targetAccount.accessRole
            });
            continue;
        }

        updatedAccounts.push(account);
    }

    applyReceiptAccounts(receipt, updatedAccounts);
}

function removeReceiptAccountLink(receiptId, accountId) {
    const receipt = findReceiptById(receiptId);
    if (!receipt) {
        throw new Error('Не удалось обновить чек после удаления связи.');
    }

    const updatedAccounts = getReceiptAccounts(receipt).filter(function (account) {
        return account && account.id !== accountId;
    });

    applyReceiptAccounts(receipt, updatedAccounts);
}

function applyReceiptAccounts(receipt, accounts) {
    const normalizedAccounts = accounts
        .filter(function (account) { return !!account; })
        .sort(function (left, right) { return left.name.localeCompare(right.name, 'ru'); });

    receipt.accounts = normalizedAccounts.map(function (account) {
        return {
            id: account.id,
            name: account.name,
            colorHex: account.colorHex,
            accessRole: account.accessRole
        };
    });

    const firstAccount = normalizedAccounts.length > 0 ? normalizedAccounts[0] : null;
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
    return /^#[0-9A-F]{6}$/.test(value) ? value : '#0D6EFD';
}

function receiptHasAccount(receipt, accountId) {
    if (!accountId) {
        return true;
    }

    return getReceiptAccounts(receipt).some(function (account) {
        return account.id === accountId;
    });
}

function sumMatchesQuery(totalSum, loweredQuery) {
    if (typeof totalSum !== 'number') return false;

    // допускаем ввод "1234", "1234.56", "1234,56"
    const normalized = loweredQuery.replace(',', '.').replace(/\s+/g, '');

    const parsed = Number(normalized);
    if (!Number.isFinite(parsed)) {
        // если не число — fallback: поиск по строке
        return totalSum.toString().includes(normalized);
    }

    // сравнение по значению (рубли/копейки) с допуском
    const diff = Math.abs(totalSum - parsed);
    return diff < 0.01;
}
