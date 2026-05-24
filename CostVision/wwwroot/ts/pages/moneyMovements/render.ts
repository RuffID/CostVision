import { clearElement } from "../../shared/dom.js";
import { formatMoneyRub } from "../../shared/formatters.js";
import { MONEY_MOVEMENT_TYPE_EXPENSE, MONEY_MOVEMENT_TYPE_INCOME, type BankStatementImportBankDto, type BankStatementImportLineErrorDto, type BankStatementImportPreviewRowDto, type MoneyMovementDto, type MoneyMovementReceiptDto, type UserAccountViewModel } from "./types.js";

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

export function renderImportBankOptions(select: HTMLSelectElement, banks: BankStatementImportBankDto[]): void {
    clearElement(select);

    for (const bank of banks) {
        const option = document.createElement("option");
        option.value = bank.id;
        option.textContent = bank.description ? `${bank.name} · ${bank.description}` : bank.name;
        select.append(option);
    }

    select.disabled = banks.length === 0;
}

export function renderMoneyMovementList(container: HTMLElement, movements: MoneyMovementDto[], accounts: UserAccountViewModel[], editingCommentMovementId: string | null = null): void {
    clearElement(container);

    if (movements.length === 0) {
        const empty = document.createElement("div");
        empty.classList.add("text-muted", "py-3");
        empty.textContent = "Операций за выбранный период нет.";
        container.append(empty);
        return;
    }

    for (const movement of movements) {
        container.append(createMoneyMovementCard(movement, accounts, movement.id === editingCommentMovementId));
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

export function renderImportPreview(container: HTMLElement, rows: BankStatementImportPreviewRowDto[]): void {
    clearElement(container);

    if (rows.length === 0) {
        const empty = document.createElement("div");
        empty.classList.add("text-muted", "py-3");
        empty.textContent = "После предпросмотра здесь появятся строки импорта.";
        container.append(empty);
        return;
    }

    for (const row of rows) {
        container.append(createImportPreviewRow(row));
    }
}

export function renderImportErrors(container: HTMLElement, errors: BankStatementImportLineErrorDto[]): void {
    clearElement(container);

    if (errors.length === 0) {
        return;
    }

    const title = document.createElement("div");
    title.classList.add("fw-semibold", "mb-2");
    title.textContent = "Ошибки распознавания";
    container.append(title);

    const list = document.createElement("div");
    list.classList.add("d-flex", "flex-column", "gap-2");

    for (const error of errors) {
        const item = document.createElement("div");
        item.classList.add("border", "border-danger", "rounded-3", "p-2", "small");

        const message = document.createElement("div");
        message.classList.add("text-danger", "fw-semibold");
        message.textContent = `Строка ${error.lineNumber}: ${error.message}`;

        const rawText = document.createElement("div");
        rawText.classList.add("text-muted");
        rawText.textContent = error.rawText;

        item.append(message, rawText);
        list.append(item);
    }

    container.append(list);
}

export function renderImportSummary(element: HTMLElement, rows: BankStatementImportPreviewRowDto[]): void {
    const duplicateCount = rows.filter(row => row.isDuplicate).length;
    element.textContent = `Строк: ${rows.length}. Повторы: ${duplicateCount}.`;
}

export function renderLinkedReceipts(container: HTMLElement, receipts: MoneyMovementReceiptDto[]): void {
    renderReceiptList(container, receipts, "unlink-receipt", "Отвязать", "Привязанных чеков нет.");
}

export function renderReceiptCandidates(container: HTMLElement, receipts: MoneyMovementReceiptDto[]): void {
    renderReceiptList(container, receipts, "link-receipt", "Привязать", "Подходящие чеки не найдены.");
}

function createMoneyMovementCard(movement: MoneyMovementDto, accounts: UserAccountViewModel[], isEditingComment: boolean): HTMLElement {
    const wrapper = document.createElement("div");
    wrapper.classList.add("border", "rounded-3", "p-3", "d-flex", "flex-column", "gap-2", "shadow-sm");
    wrapper.classList.add(movement.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "border-danger" : "border-success");
    wrapper.setAttribute("data-money-movement-id", movement.id);

    const header = document.createElement("div");
    header.classList.add("d-flex", "flex-wrap", "justify-content-between", "gap-2", "align-items-start");

    const left = document.createElement("div");
    left.classList.add("d-flex", "flex-column", "gap-1", "flex-grow-1");

    const account = accounts.find(item => item.id === movement.accountId);
    const canEditMovement = account ? canEditAccount(account) : true;
    const commentBlock = isEditingComment ? createEditableCommentBlock(movement) : createReadonlyCommentBlock(movement, canEditMovement);

    const meta = document.createElement("div");
    meta.classList.add("text-muted", "small");
    meta.textContent = formatDateTime(movement.occurredAt);

    const accountBadge = createAccountBadge(movement, accounts);

    const actions = document.createElement("div");
    actions.classList.add("d-flex", "flex-wrap", "gap-2", "align-items-center");
    actions.append(accountBadge, createReceiptsButton(movement));

    left.append(commentBlock, meta, actions);

    const amount = document.createElement("div");
    amount.classList.add("fw-semibold", "fs-5");
    amount.classList.add(movement.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "text-danger" : "text-success");
    amount.textContent = `${movement.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "-" : "+"}${formatMoneyRub(movement.amount)}`;

    header.append(left, amount);

    wrapper.append(header);
    return wrapper;
}

function createReceiptsButton(movement: MoneyMovementDto): HTMLButtonElement {
    const button = document.createElement("button");
    button.type = "button";
    button.classList.add("btn", "btn-sm", "btn-outline-secondary", "align-self-start");
    button.setAttribute("data-action", "open-receipts");
    button.setAttribute("data-money-movement-id", movement.id);
    if (movement.linkedReceiptCount > 0 || movement.availableReceiptCount > 0) {
        button.textContent = `Чеки: ${movement.linkedReceiptCount} привязано / ${movement.availableReceiptCount} доступно`;
        button.title = `Привязанные чеки: ${movement.linkedReceiptCount}. Доступные чеки: ${movement.availableReceiptCount}.`;
    } else {
        button.textContent = "Чеки";
        button.title = "Чеки операции";
    }
    return button;
}

function createReadonlyCommentBlock(movement: MoneyMovementDto, canEditMovement: boolean): HTMLElement {
    const wrapper = document.createElement("div");
    wrapper.classList.add("d-flex", "flex-column", "gap-1");

    const top = document.createElement("div");
    top.classList.add("d-flex", "align-items-start", "gap-2");

    const comment = document.createElement("div");
    comment.classList.add("fw-semibold");
    comment.textContent = movement.comment || "Без комментария";

    top.append(comment);

    if (canEditMovement) {
        const editButton = document.createElement("button");
        editButton.type = "button";
        editButton.classList.add("btn", "btn-sm", "btn-link", "p-0", "text-secondary", "lh-1", "flex-shrink-0");
        editButton.setAttribute("data-action", "edit-comment");
        editButton.setAttribute("data-money-movement-id", movement.id);
        editButton.title = "Редактировать комментарий";
        editButton.textContent = "✎";
        top.append(editButton);
    }
    wrapper.append(top);

    const importComment = createChangedImportCommentElement(movement);
    if (importComment) {
        wrapper.append(importComment);
    }

    return wrapper;
}

function createEditableCommentBlock(movement: MoneyMovementDto): HTMLElement {
    const wrapper = document.createElement("div");
    wrapper.classList.add("d-flex", "flex-column", "gap-2");

    const input = document.createElement("textarea");
    input.id = `moneyMovementEditComment_${movement.id}`;
    input.name = `moneyMovementEditComment_${movement.id}`;
    input.classList.add("form-control", "form-control-sm");
    input.rows = 2;
    input.maxLength = 1024;
    input.value = movement.comment || "";
    input.setAttribute("data-action", "edit-comment-input");
    input.setAttribute("data-money-movement-id", movement.id);

    const actions = document.createElement("div");
    actions.classList.add("d-flex", "gap-2");

    const saveButton = document.createElement("button");
    saveButton.type = "button";
    saveButton.classList.add("btn", "btn-sm", "btn-primary");
    saveButton.setAttribute("data-action", "save-comment");
    saveButton.setAttribute("data-money-movement-id", movement.id);
    saveButton.title = "Сохранить комментарий";
    saveButton.textContent = "✓";

    const cancelButton = document.createElement("button");
    cancelButton.type = "button";
    cancelButton.classList.add("btn", "btn-sm", "btn-outline-secondary");
    cancelButton.setAttribute("data-action", "cancel-comment");
    cancelButton.setAttribute("data-money-movement-id", movement.id);
    cancelButton.title = "Отменить";
    cancelButton.textContent = "×";

    actions.append(saveButton, cancelButton);
    wrapper.append(input, actions);

    const importComment = createChangedImportCommentElement(movement);
    if (importComment) {
        wrapper.append(importComment);
    }

    return wrapper;
}

function createChangedImportCommentElement(movement: MoneyMovementDto): HTMLElement | null {
    const importComment = normalizeComment(movement.importComment);
    if (!importComment || normalizeComment(movement.comment) === importComment) {
        return null;
    }

    const element = document.createElement("div");
    element.classList.add("text-muted", "small");
    element.textContent = `Комментарий из выписки: ${movement.importComment}`;
    return element;
}

function normalizeComment(value: string | null | undefined): string {
    return String(value || "").replace(/\s+/g, " ").trim();
}

function createImportPreviewRow(row: BankStatementImportPreviewRowDto): HTMLElement {
    const wrapper = document.createElement("div");
    wrapper.classList.add("border", "rounded-3", "p-3");
    wrapper.setAttribute("data-import-row-id", row.clientRowId);

    if (row.isDuplicate) {
        wrapper.classList.add("border-warning");
    }

    const header = document.createElement("div");
    header.classList.add("d-flex", "flex-wrap", "justify-content-between", "align-items-start", "gap-2", "mb-2");

    const meta = document.createElement("div");
    meta.classList.add("small", "text-muted");
    meta.textContent = `${formatDateTime(row.occurredAt)} · строка ${row.sourceLineNumber}`;

    const amount = document.createElement("div");
    amount.classList.add("fw-semibold");
    amount.classList.add(row.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "text-danger" : "text-success");
    amount.textContent = `${row.type === MONEY_MOVEMENT_TYPE_EXPENSE ? "-" : "+"}${formatMoneyRub(row.amount)}`;

    header.append(meta, amount);

    const label = document.createElement("label");
    label.classList.add("form-label", "small");
    label.htmlFor = `moneyMovementImportComment_${row.clientRowId}`;
    label.textContent = "Комментарий";

    const comment = document.createElement("textarea");
    comment.id = `moneyMovementImportComment_${row.clientRowId}`;
    comment.name = `moneyMovementImportComment_${row.clientRowId}`;
    comment.classList.add("form-control");
    comment.rows = 2;
    comment.maxLength = 1024;
    comment.value = row.comment || "";
    comment.setAttribute("data-action", "change-import-comment");
    comment.setAttribute("data-import-row-id", row.clientRowId);

    const importComment = document.createElement("div");
    importComment.classList.add("small", "text-muted", "mt-2");
    importComment.textContent = `Комментарий из выписки: ${row.importComment}`;

    const footer = document.createElement("div");
    footer.classList.add("d-flex", "flex-wrap", "justify-content-between", "align-items-center", "gap-2", "mt-2");

    const duplicateControl = createDuplicateControl(row);
    const removeButton = document.createElement("button");
    removeButton.type = "button";
    removeButton.classList.add("btn", "btn-sm", "btn-outline-danger");
    removeButton.setAttribute("data-action", "remove-import-row");
    removeButton.setAttribute("data-import-row-id", row.clientRowId);
    removeButton.textContent = "Удалить";

    footer.append(duplicateControl, removeButton);
    wrapper.append(header, label, comment, importComment, footer);
    return wrapper;
}

function createDuplicateControl(row: BankStatementImportPreviewRowDto): HTMLElement {
    const wrapper = document.createElement("div");

    if (!row.isDuplicate) {
        wrapper.classList.add("small", "text-muted");
        wrapper.textContent = "Новая операция";
        return wrapper;
    }

    wrapper.classList.add("form-check");

    const input = document.createElement("input");
    input.type = "checkbox";
    input.classList.add("form-check-input");
    input.id = `moneyMovementImportReplace_${row.clientRowId}`;
    input.name = `moneyMovementImportReplace_${row.clientRowId}`;
    input.checked = row.replaceDuplicate === true;
    input.setAttribute("data-action", "toggle-import-replace");
    input.setAttribute("data-import-row-id", row.clientRowId);

    const label = document.createElement("label");
    label.classList.add("form-check-label", "text-warning");
    label.htmlFor = input.id;
    label.textContent = "Повтор: заменить существующую";

    wrapper.append(input, label);
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
    badge.style.transition = "background-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease";
    badge.disabled = !canEdit;

    if (canEdit) {
        badge.addEventListener("mouseenter", () => {
            badge.style.backgroundColor = movement.accountColorHex + "14";
            badge.style.boxShadow = "0 0 0 0.2rem " + movement.accountColorHex + "22";
            badge.style.transform = "translateY(-1px)";
        });

        badge.addEventListener("mouseleave", () => {
            badge.style.backgroundColor = "transparent";
            badge.style.boxShadow = "none";
            badge.style.transform = "translateY(0)";
        });
    } else {
        badge.title = "Недостаточно прав для изменения операции в этом счёте.";
        badge.style.opacity = "0.65";
        badge.style.cursor = "not-allowed";
    }

    return badge;
}

function renderReceiptList(container: HTMLElement, receipts: MoneyMovementReceiptDto[], action: string, actionText: string, emptyText: string): void {
    clearElement(container);

    if (receipts.length === 0) {
        const empty = document.createElement("div");
        empty.classList.add("text-muted", "py-2");
        empty.textContent = emptyText;
        container.append(empty);
        return;
    }

    for (const receipt of receipts) {
        container.append(createReceiptCard(receipt, action, actionText));
    }
}

function createReceiptCard(receipt: MoneyMovementReceiptDto, action: string, actionText: string): HTMLElement {
    const wrapper = document.createElement("div");
    wrapper.classList.add("border", "rounded-3", "p-2", "d-flex", "flex-wrap", "justify-content-between", "gap-2", "align-items-start");

    if (receipt.isLinkedToOtherMoneyMovement && action === "link-receipt") {
        wrapper.classList.add("border-warning");
    }

    const left = document.createElement("div");
    left.classList.add("d-flex", "flex-column", "gap-1");

    const title = document.createElement("div");
    title.classList.add("fw-semibold");
    title.textContent = receipt.retailPlace || "Без названия";

    const meta = document.createElement("div");
    meta.classList.add("small", "text-muted");
    const accountText = receipt.accountName ? ` · ${receipt.accountName}` : "";
    meta.textContent = `${formatDateTime(receipt.dateTime)}${accountText}`;

    const amount = document.createElement("div");
    amount.classList.add("small");
    amount.textContent = formatMoneyRub(receipt.totalSum);

    left.append(title, meta, amount);

    if (receipt.isLinkedToOtherMoneyMovement && action === "link-receipt") {
        const warning = document.createElement("div");
        warning.classList.add("small", "text-warning");
        warning.textContent = "Уже связан с операцией";
        left.append(warning);
    }

    const button = document.createElement("button");
    button.type = "button";
    button.classList.add("btn", "btn-sm", action === "link-receipt" ? "btn-outline-primary" : "btn-outline-danger");
    button.setAttribute("data-action", action);
    button.setAttribute("data-receipt-id", receipt.receiptId);
    button.textContent = actionText;

    wrapper.append(left, button);
    return wrapper;
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
