import { createBootstrapModal } from "../../../../shared/bootstrap.js";
import { clearElement, requireElementById, requireInputById } from "../../../../shared/dom.js";
import { formatDateForQuery } from "../dateRange.js";
import { linkReceiptMoneyMovementApi, loadLinkedMoneyMovementsApi, loadMoneyMovementCandidatesApi, unlinkReceiptMoneyMovementApi } from "../api.js";
import { GetReceiptMoneyMovementCandidatesRequest, ReceiptDto, ReceiptMoneyMovementDto } from "../types.js";
import { normalizeSingleLineTextValue } from "../../../../shared/formatters.js";
import { renderHelpTooltip } from "../../../../shared/helpTooltip.js";

type ReceiptMoneyMovementsModalOptions = {
    getReceipts: () => ReceiptDto[];
    getForgeryToken: () => string | null;
    reloadReceiptList: () => Promise<void>;
    formatCurrency: (value: number) => string;
};

type ReceiptMoneyMovementsModalElements = {
    modalElement: HTMLElement;
    alert: HTMLElement;
    info: HTMLElement;
    linkedMovements: HTMLElement;
    candidates: HTMLElement;
    dateFrom: HTMLInputElement;
    dateTo: HTMLInputElement;
    amountTolerance: HTMLInputElement;
    timeWindowHours: HTMLInputElement;
    useTimeWindow: HTMLInputElement;
    useAmountFilter: HTMLInputElement;
    excludeLinked: HTMLInputElement;
    reloadButton: HTMLButtonElement;
};

export type ReceiptMoneyMovementsModalController = {
    open: (receiptId: string) => Promise<void>;
};

export function initReceiptMoneyMovementsModal(options: ReceiptMoneyMovementsModalOptions): ReceiptMoneyMovementsModalController {
    const elements = getReceiptMoneyMovementsModalElements();
    const bootstrapModal = createBootstrapModal(elements.modalElement);
    let selectedReceiptId: string | null = null;

    initReceiptMoneyMovementHelpTooltips();

    elements.reloadButton.addEventListener('click', function () {
        reloadDetails().catch(function (error) {
            showAlert(elements, getErrorMessage(error));
        });
    });

    elements.useTimeWindow.addEventListener('change', function () {
        updateFilterState(elements);
    });

    elements.useAmountFilter.addEventListener('change', function () {
        updateFilterState(elements);
    });

    elements.linkedMovements.addEventListener('click', onLinkedMovementsClick);
    elements.candidates.addEventListener('click', onCandidatesClick);
    elements.modalElement.addEventListener('hidden.bs.modal', function () {
        selectedReceiptId = null;
        hideAlert(elements);
    });

    async function open(receiptId: string): Promise<void> {
        const receipt = findReceiptById(options.getReceipts(), receiptId);
        if (!receipt) {
            alert('Чек не найден в текущем списке.');
            return;
        }

        selectedReceiptId = receiptId;
        elements.info.textContent = buildInfoText(receipt, options.formatCurrency);
        initFilters(elements, receipt);
        updateFilterState(elements);
        hideAlert(elements);
        bootstrapModal.show();
        await reloadDetails();
    }

    async function reloadDetails(): Promise<void> {
        if (!selectedReceiptId) {
            return;
        }

        await Promise.all([
            reloadLinkedMovements(selectedReceiptId),
            reloadCandidates(selectedReceiptId)
        ]);
    }

    async function reloadLinkedMovements(receiptId: string): Promise<void> {
        const movements = await loadLinkedMoneyMovementsApi(receiptId, options.getForgeryToken());
        renderMovementList(elements.linkedMovements, movements, 'unlink-money-movement', 'Отвязать', 'Привязанных операций нет.', options.formatCurrency);
    }

    async function reloadCandidates(receiptId: string): Promise<void> {
        const movements = await loadMoneyMovementCandidatesApi(readCandidatesRequest(elements, receiptId), options.getForgeryToken());
        renderMovementList(elements.candidates, movements, 'link-money-movement', 'Привязать', 'Подходящие операции не найдены.', options.formatCurrency);
    }

    async function onLinkedMovementsClick(event: MouseEvent): Promise<void> {
        const button = getMoneyMovementActionButton(event, 'unlink-money-movement');
        if (!button || !selectedReceiptId) {
            return;
        }

        const moneyMovementId = button.getAttribute('data-money-movement-id');
        if (!moneyMovementId) {
            return;
        }

        await runButtonAction(elements, button, async () => {
            await unlinkReceiptMoneyMovementApi({
                receiptId: selectedReceiptId!,
                moneyMovementId: moneyMovementId
            }, options.getForgeryToken());
            await options.reloadReceiptList();
            await reloadDetails();
        });
    }

    async function onCandidatesClick(event: MouseEvent): Promise<void> {
        const button = getMoneyMovementActionButton(event, 'link-money-movement');
        if (!button || !selectedReceiptId) {
            return;
        }

        const moneyMovementId = button.getAttribute('data-money-movement-id');
        if (!moneyMovementId) {
            return;
        }

        await runButtonAction(elements, button, async () => {
            await linkReceiptMoneyMovementApi({
                receiptId: selectedReceiptId!,
                moneyMovementId: moneyMovementId
            }, options.getForgeryToken());
            await options.reloadReceiptList();
            await reloadDetails();
        });
    }

    return { open };
}

function getReceiptMoneyMovementsModalElements(): ReceiptMoneyMovementsModalElements {
    return {
        modalElement: requireElementById<HTMLElement>('receiptMoneyMovementsModal'),
        alert: requireElementById<HTMLElement>('receiptMoneyMovementsAlert'),
        info: requireElementById<HTMLElement>('receiptMoneyMovementsInfo'),
        linkedMovements: requireElementById<HTMLElement>('receiptLinkedMoneyMovements'),
        candidates: requireElementById<HTMLElement>('receiptMoneyMovementCandidates'),
        dateFrom: requireInputById('receiptMoneyMovementsDateFrom'),
        dateTo: requireInputById('receiptMoneyMovementsDateTo'),
        amountTolerance: requireInputById('receiptMoneyMovementsAmountTolerance'),
        timeWindowHours: requireInputById('receiptMoneyMovementsTimeWindowHours'),
        useTimeWindow: requireInputById('receiptMoneyMovementsUseTimeWindow'),
        useAmountFilter: requireInputById('receiptMoneyMovementsUseAmountFilter'),
        excludeLinked: requireInputById('receiptMoneyMovementsExcludeLinked'),
        reloadButton: requireElementById<HTMLButtonElement>('receiptMoneyMovementsReloadCandidatesButton')
    };
}

function initReceiptMoneyMovementHelpTooltips(): void {
    renderHelpTooltip(requireElementById<HTMLElement>('receiptMoneyMovementsAmountToleranceHelp'), {
        title: "Допуск суммы",
        text: "Разрешённая разница между суммой чека и суммой операции при поиске кандидатов."
    });
    renderHelpTooltip(requireElementById<HTMLElement>('receiptMoneyMovementsTimeWindowHoursHelp'), {
        title: "Допуск времени",
        text: "Размер окна поиска по времени в часах до и после времени чека. Используется только при включённом окне времени."
    });
    renderHelpTooltip(requireElementById<HTMLElement>('receiptMoneyMovementsUseTimeWindowHelp'), {
        title: "Окно времени",
        text: "Искать операции в пределах указанного допуска времени до и после времени чека. При включении ручной период дат не используется."
    });
    renderHelpTooltip(requireElementById<HTMLElement>('receiptMoneyMovementsUseAmountFilterHelp'), {
        title: "По сумме",
        text: "Показывать только операции, сумма которых близка к сумме чека с учётом допуска."
    });
    renderHelpTooltip(requireElementById<HTMLElement>('receiptMoneyMovementsExcludeLinkedHelp'), {
        title: "Без привязанных",
        text: "Скрывать операции, которые уже связаны с любым чеком. Отключите, если операцию можно привязать повторно."
    });
}

function readCandidatesRequest(elements: ReceiptMoneyMovementsModalElements, receiptId: string): GetReceiptMoneyMovementCandidatesRequest {
    const amountTolerance = Number(elements.amountTolerance.value);
    const timeWindowHours = Number(elements.timeWindowHours.value);

    return {
        receiptId: receiptId,
        useTimeWindow: elements.useTimeWindow.checked,
        timeWindowHours: Number.isFinite(timeWindowHours) ? timeWindowHours : null,
        dateFrom: elements.dateFrom.value || null,
        dateTo: elements.dateTo.value || null,
        useAmountFilter: elements.useAmountFilter.checked,
        amountTolerance: Number.isFinite(amountTolerance) ? amountTolerance : null,
        excludeLinkedMoneyMovements: elements.excludeLinked.checked
    };
}

function initFilters(elements: ReceiptMoneyMovementsModalElements, receipt: ReceiptDto): void {
    const receiptDate = new Date(receipt.dateTime);
    const date = Number.isNaN(receiptDate.getTime()) ? new Date() : receiptDate;
    const dateFrom = new Date(date);
    const dateTo = new Date(date);
    dateFrom.setDate(dateFrom.getDate() - 1);
    dateTo.setDate(dateTo.getDate() + 1);

    elements.dateFrom.value = formatDateForQuery(dateFrom);
    elements.dateTo.value = formatDateForQuery(dateTo);
    elements.useTimeWindow.checked = false;
    elements.useAmountFilter.checked = true;
    elements.excludeLinked.checked = true;
    elements.amountTolerance.value = '1';
    elements.timeWindowHours.value = '1';
}

function updateFilterState(elements: ReceiptMoneyMovementsModalElements): void {
    const useTimeWindow = elements.useTimeWindow.checked;
    elements.dateFrom.disabled = useTimeWindow;
    elements.dateTo.disabled = useTimeWindow;
    elements.timeWindowHours.disabled = !useTimeWindow;
    elements.amountTolerance.disabled = !elements.useAmountFilter.checked;
}

async function runButtonAction(elements: ReceiptMoneyMovementsModalElements, button: HTMLButtonElement, action: () => Promise<void>): Promise<void> {
    const originalText = button.textContent;
    button.disabled = true;
    button.textContent = '...';

    try {
        hideAlert(elements);
        await action();
    } catch (error) {
        showAlert(elements, getErrorMessage(error));
    } finally {
        button.disabled = false;
        button.textContent = originalText;
    }
}

function renderMovementList(container: HTMLElement, movements: ReceiptMoneyMovementDto[], action: string, actionText: string, emptyText: string, formatCurrency: (value: number) => string): void {
    clearElement(container);

    if (movements.length === 0) {
        const empty = document.createElement('div');
        empty.classList.add('text-muted', 'py-2');
        empty.textContent = emptyText;
        container.append(empty);
        return;
    }

    for (const movement of movements) {
        container.append(createMovementCard(movement, action, actionText, formatCurrency));
    }
}

function createMovementCard(movement: ReceiptMoneyMovementDto, action: string, actionText: string, formatCurrency: (value: number) => string): HTMLElement {
    const wrapper = document.createElement('div');
    wrapper.classList.add('border', 'rounded-3', 'p-2', 'd-flex', 'flex-wrap', 'justify-content-between', 'gap-2', 'align-items-start');

    if (movement.isLinkedToOtherReceipt && action === 'link-money-movement') {
        wrapper.classList.add('border-warning');
    }

    const left = document.createElement('div');
    left.classList.add('d-flex', 'flex-column', 'gap-1');

    const title = document.createElement('div');
    title.classList.add('fw-semibold');
    title.textContent = movement.comment || movement.importComment || 'Без комментария';

    const meta = document.createElement('div');
    meta.classList.add('small', 'text-muted');
    const accountText = movement.accountName ? ` · ${movement.accountName}` : '';
    meta.textContent = `${formatDateTime(movement.occurredAt)}${accountText}`;

    const amount = document.createElement('div');
    amount.classList.add('small');
    amount.textContent = formatCurrency(movement.amount);

    left.append(title, meta, amount);

    if (movement.isLinkedToOtherReceipt && action === 'link-money-movement') {
        const warning = document.createElement('div');
        warning.classList.add('small', 'text-warning');
        warning.textContent = 'Уже связана с чеком';
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

function getMoneyMovementActionButton(event: MouseEvent, action: string): HTMLButtonElement | null {
    const target = event.target;
    if (!(target instanceof Element)) {
        return null;
    }

    return target.closest<HTMLButtonElement>(`[data-action="${action}"]`);
}

function buildInfoText(receipt: ReceiptDto, formatCurrency: (value: number) => string): string {
    return `${formatDateTime(receipt.dateTime)} · ${formatCurrency(receipt.totalSum)} · ${normalizeSingleLineTextValue(receipt.retailPlace)}`;
}

function findReceiptById(receipts: ReceiptDto[], receiptId: string): ReceiptDto | null {
    return receipts.find(function (receipt) {
        return receipt.id === receiptId;
    }) || null;
}

function showAlert(elements: ReceiptMoneyMovementsModalElements, message: string): void {
    elements.alert.textContent = message || '';
    elements.alert.classList.toggle('d-none', !message);
}

function hideAlert(elements: ReceiptMoneyMovementsModalElements): void {
    showAlert(elements, '');
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

    return 'Ошибка операции.';
}
