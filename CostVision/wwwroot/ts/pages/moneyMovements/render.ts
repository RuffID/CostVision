import { clearElement } from "../../shared/dom.js";
import { formatMoneyRub } from "../../shared/formatters.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type MoneyMovementDto, type UserAccountViewModel } from "./types.js";

export function renderAccountOptions(select: HTMLSelectElement, accounts: UserAccountViewModel[], includeAllOption: boolean): void {
    clearElement(select);

    if (includeAllOption) {
        const allOption = document.createElement("option");
        allOption.value = "";
        allOption.textContent = "Все счета";
        select.append(allOption);
    }

    for (const account of accounts) {
        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;
        select.append(option);
    }

    select.disabled = accounts.length === 0;
}

export function renderMoneyMovementList(container: HTMLElement, movements: MoneyMovementDto[], accounts: UserAccountViewModel[]): void {
    clearElement(container);

    if (movements.length === 0) {
        const empty = document.createElement("div");
        empty.classList.add("text-muted", "py-3");
        empty.textContent = "Операций за выбранный период нет.";
        container.append(empty);
        return;
    }

    for (const movement of movements) {
        container.append(createMoneyMovementCard(movement, accounts));
    }
}

export function renderSummary(countElement: HTMLElement, incomeElement: HTMLElement, expenseElement: HTMLElement, movements: MoneyMovementDto[]): void {
    const incomeSum = movements
        .filter(movement => movement.type === MONEY_MOVEMENT_TYPE_INCOME)
        .reduce((sum, movement) => sum + Number(movement.amount || 0), 0);

    const expenseSum = movements
        .filter(movement => movement.type === MONEY_MOVEMENT_TYPE_EXPENSE)
        .reduce((sum, movement) => sum + Number(movement.amount || 0), 0);

    countElement.textContent = `Операций: ${movements.length}`;
    incomeElement.textContent = `Приход: ${formatMoneyRub(incomeSum)}`;
    expenseElement.textContent = `Расход: ${formatMoneyRub(expenseSum)}`;
}

function createMoneyMovementCard(movement: MoneyMovementDto, accounts: UserAccountViewModel[]): HTMLElement {
    const wrapper = document.createElement("div");
    wrapper.classList.add("border", "rounded-3", "p-3", "d-flex", "flex-column", "gap-2");
    wrapper.setAttribute("data-money-movement-id", movement.id);

    const header = document.createElement("div");
    header.classList.add("d-flex", "flex-wrap", "justify-content-between", "gap-2", "align-items-start");

    const left = document.createElement("div");
    left.classList.add("d-flex", "flex-column", "gap-1");

    const comment = document.createElement("div");
    comment.classList.add("fw-semibold");
    comment.textContent = movement.comment || "Без комментария";

    const meta = document.createElement("div");
    meta.classList.add("text-muted", "small");
    meta.textContent = formatDateTime(movement.occurredAt);

    const accountBadge = createAccountBadge(movement, accounts);

    left.append(comment, meta, accountBadge);

    const amount = document.createElement("div");
    amount.classList.add("fw-semibold");
    amount.classList.add(movement.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "text-danger" : "text-success");
    amount.textContent = `${movement.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "-" : "+"}${formatMoneyRub(movement.amount)}`;

    header.append(left, amount);

    wrapper.append(header);
    return wrapper;
}

function createAccountBadge(movement: MoneyMovementDto, accounts: UserAccountViewModel[]): HTMLButtonElement {
    const account = accounts.find(item => item.id === movement.accountId);
    const canEdit = account ? canEditAccount(account) : true;

    const badge = document.createElement("button");
    badge.type = "button";
    badge.className = "btn btn-sm px-2 py-1 rounded-pill align-self-start";
    badge.setAttribute("data-action", "edit-account-link");
    badge.setAttribute("data-money-movement-id", movement.id);
    badge.setAttribute("data-account-id", movement.accountId);
    badge.textContent = movement.accountName || "Счёт";
    badge.style.backgroundColor = "transparent";
    badge.style.color = "#212529";
    badge.style.border = "2px solid " + movement.accountColorHex;
    badge.disabled = !canEdit;

    if (!canEdit) {
        badge.title = "Недостаточно прав для изменения операции в этом счёте.";
        badge.style.opacity = "0.65";
        badge.style.cursor = "not-allowed";
    }

    return badge;
}

function canEditAccount(account: UserAccountViewModel): boolean {
    return account.canManage === true || account.accessRole === 1 || account.accessRole === 2;
}

function formatDateTime(value: string): string {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return date.toLocaleString("ru-RU", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
    });
}
