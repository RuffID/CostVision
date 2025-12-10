// Глобальные переменные для страницы списка чеков
let listContainer;
let modalElement;
let detailsList;
let modalHeader;
let modalTotal;
let bootstrapModal;
let forgeryToken;
let dateFromInput;
let dateToInput;
let applyFilterButton;

const RECEIPTS_DATE_FROM_KEY = 'costvision_receipts_dateFrom';
const RECEIPTS_DATE_TO_KEY = 'costvision_receipts_dateTo';

// Инициализация после загрузки DOM
document.addEventListener('DOMContentLoaded', function () {
    initListOfChecksPage();
});

// Инициализировать страницу списка чеков
function initListOfChecksPage() {
    listContainer = document.getElementById('receipt-list');
    if (!listContainer) {
        return;
    }

    modalElement = document.getElementById('receiptDetailsModal');
    detailsList = document.getElementById('receipt-details-list');
    modalHeader = document.getElementById('receipt-details-header');
    modalTotal = document.getElementById('receipt-details-total');

    dateFromInput = document.getElementById('dateFrom');
    dateToInput = document.getElementById('dateTo');

    if (dateFromInput) {
        dateFromInput.addEventListener('input', function () {
            normalizeDateInput(dateFromInput);
            clampDateTextInput(dateFromInput);
        });
    }

    if (dateToInput) {
        dateToInput.addEventListener('input', function () {
            normalizeDateInput(dateToInput);
            clampDateTextInput(dateToInput);
        });
    }


    applyFilterButton = document.getElementById('applyFilter');

    if (modalElement) {
        bootstrapModal = new bootstrap.Modal(modalElement);
    }

    forgeryToken = getRequestVerificationToken();

    if (applyFilterButton) {
        applyFilterButton.addEventListener('click', function () {
            savePeriodToStorage();
            loadReceiptList();
        });
    }

    if (listContainer) {
        listContainer.addEventListener('click', onReceiptListClick);
    }

    // Попробовать восстановить период из localStorage
    loadPeriodFromStorage();

    // Если период не задан, то установить период по умолчанию: текущий месяц
    setDefaultMonthIfEmpty();

    // Загрузить список чеков
    loadReceiptList();
}

// Установить период по умолчанию (текущий месяц), если даты не заданы
function setDefaultMonthIfEmpty() {
    if (!dateFromInput || !dateToInput) {
        return;
    }

    const hasFrom = !!dateFromInput.value;
    const hasTo = !!dateToInput.value;

    if (hasFrom && hasTo) {
        return;
    }

    const now = new Date();
    const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
    const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0);

    const fromStr = formatDateForQuery(firstDay);
    const toStr = formatDateForQuery(lastDay);

    if (!hasFrom) {
        dateFromInput.value = fromStr;
    }
    if (!hasTo) {
        dateToInput.value = toStr;
    }
}

// Загрузить период из localStorage (если есть)
function loadPeriodFromStorage() {
    if (!dateFromInput || !dateToInput) {
        return;
    }

    const storedFrom = localStorage.getItem(RECEIPTS_DATE_FROM_KEY);
    const storedTo = localStorage.getItem(RECEIPTS_DATE_TO_KEY);

    if (storedFrom) {
        dateFromInput.value = storedFrom;
    }

    if (storedTo) {
        dateToInput.value = storedTo;
    }
}

// Сохранить период в localStorage
function savePeriodToStorage() {
    if (!dateFromInput || !dateToInput) {
        return;
    }

    const fromVal = dateFromInput.value || '';
    const toVal = dateToInput.value || '';

    localStorage.setItem(RECEIPTS_DATE_FROM_KEY, fromVal);
    localStorage.setItem(RECEIPTS_DATE_TO_KEY, toVal);
}

// Обработчик клика по карточкам чеков
function onReceiptListClick(event) {
    const target = event.target;
    if (!target) {
        return;
    }

    const openButton = target.closest('[data-action="open"]');
    const refreshButton = target.closest('[data-action="refresh"]');

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

        const data = await sendJsonRequest(url, 'GET', buildJsonHeaders(forgeryToken), null);

        // Успешный ответ от JsonResultMapper.ToJsonResult: data = List<ReceiptDto>
        if (!Array.isArray(data)) {
            return;
        }

        listContainer.innerHTML = '';

        data.forEach(function (r) {
            const card = buildReceiptCard(r);
            listContainer.appendChild(card);
        });
    } catch (error) {
        console.error(error);
        alert('Ошибка при получении списка чеков.');
    }
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

        if (!data) {
            return;
        }

        renderReceiptDetails(data);
    } catch (error) {
        console.error(error);
        alert('Ошибка при получении деталей чека.');
    }
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

        if (data && cardElement) {
            updateCardFromDto(cardElement, data);
        }
    } catch (error) {
        console.error(error);
        alert(error?.message ?? error);
    } finally {
        buttonElement.disabled = false;
        buttonElement.innerHTML = originalHtml;
    }
}

// Построить карточку чека
function buildReceiptCard(r) {
    const card = document.createElement('div');
    card.classList.add('card', 'shadow-sm');
    card.setAttribute('data-receipt-id', r.id);

    const cardBody = document.createElement('div');
    cardBody.classList.add('card-body', 'd-flex', 'justify-content-between', 'align-items-center');

    const leftDiv = document.createElement('div');
    leftDiv.classList.add('me-3');

    const titleDiv = document.createElement('div');
    titleDiv.classList.add('fw-semibold', 'mb-1');

    let dateText = '';
    if (r.dateTime) {
        const date = new Date(r.dateTime);
        dateText =
            date.toLocaleDateString('ru-RU') + ' ' +
            date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
    }

    const place = r.retailPlace || '';
    titleDiv.textContent = dateText + ' — ' + place;

    leftDiv.appendChild(titleDiv);

    if (r.retailPlaceAddress) {
        const addrDiv = document.createElement('div');
        addrDiv.classList.add('text-muted', 'small');
        addrDiv.textContent = r.retailPlaceAddress;
        leftDiv.appendChild(addrDiv);
    }

    const rightDiv = document.createElement('div');
    rightDiv.classList.add('d-flex', 'align-items-center', 'ms-auto');

    const totalDiv = document.createElement('div');
    totalDiv.classList.add('text-end', 'me-3');

    const totalSpan = document.createElement('div');
    totalSpan.classList.add('fw-bold');
    totalSpan.textContent = formatCurrency(r.totalSum);

    totalDiv.appendChild(totalSpan);

    const btnGroup = document.createElement('div');
    btnGroup.classList.add('btn-group');

    const openBtn = document.createElement('button');
    openBtn.type = 'button';
    openBtn.classList.add('btn', 'btn-sm', 'btn-outline-primary');
    openBtn.setAttribute('data-action', 'open');
    openBtn.textContent = 'Открыть';

    const refreshBtn = document.createElement('button');
    refreshBtn.type = 'button';
    refreshBtn.classList.add('btn', 'btn-sm', 'btn-outline-secondary');
    refreshBtn.setAttribute('data-action', 'refresh');
    refreshBtn.textContent = 'Обновить данные';

    btnGroup.appendChild(openBtn);
    btnGroup.appendChild(refreshBtn);

    rightDiv.appendChild(totalDiv);
    rightDiv.appendChild(btnGroup);

    cardBody.appendChild(leftDiv);
    cardBody.appendChild(rightDiv);

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
    const addressElement = bodyElement.querySelector('.text-muted.small');
    const totalElement = bodyElement.querySelector('.fw-bold');

    if (datePlaceElement && receiptDto.dateTime) {
        const date = new Date(receiptDto.dateTime);
        const formatted =
            date.toLocaleDateString('ru-RU') + ' ' +
            date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
        const place = receiptDto.retailPlace || '';
        datePlaceElement.textContent = formatted + ' — ' + place;
    }

    if (addressElement) {
        if (receiptDto.retailPlaceAddress) {
            addressElement.textContent = receiptDto.retailPlaceAddress;
        } else {
            addressElement.textContent = '';
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

function clampDateTextInput(input) {
    const v = input.value;

    if (!/^\d{4}-\d{2}-\d{2}$/.test(v)) {
        return; // формат ещё не готов
    }

    const year = Number(v.slice(0, 4));
    const month = Number(v.slice(5, 7));
    let day = Number(v.slice(8, 10));

    if (month < 1 || month > 12) return;

    const lastDay = new Date(year, month, 0).getDate();
    if (day > lastDay) day = lastDay;

    input.value = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}


function normalizeDateInput(input) {
    let v = input.value.replace(/[^\d-]/g, ''); // убрать всё, кроме цифр и '-'

    // Автоформирование YYYY-MM-DD
    if (v.length > 4 && v[4] !== '-') {
        v = v.slice(0, 4) + '-' + v.slice(4);
    }
    if (v.length > 7 && v[7] !== '-') {
        v = v.slice(0, 7) + '-' + v.slice(7);
    }

    // Ограничение длины
    if (v.length > 10) {
        v = v.slice(0, 10);
    }

    input.value = v;
}
