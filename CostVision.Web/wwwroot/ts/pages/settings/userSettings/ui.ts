import { clearElement, requireElementById } from "../../../shared/dom.js";
import { ACCOUNT_ACCESS_ROLE_EDITOR, ACCOUNT_ACCESS_ROLE_VIEWER, AccountAccessRole, AccountViewModel, ShareAssignableRole, ShareCandidateDto, ShareMemberPayload } from "./models.js";
import { normalizeColorHex } from "./state.js";

export function renderAccountsList(accountsListElement: HTMLElement, accountsEmptyElement: HTMLElement, accounts: AccountViewModel[]): void {
    clearElement(accountsListElement);

    if (!accounts.length) {
        accountsEmptyElement.classList.remove("d-none");
        return;
    }

    accountsEmptyElement.classList.add("d-none");

    accounts.forEach(account => {
        accountsListElement.appendChild(createAccountListItem(account));
    });
}

export function renderShareCandidates(shareUsersContainer: HTMLElement, shareEmptyElement: HTMLElement, users: ShareCandidateDto[]): void {
    shareUsersContainer.replaceChildren();

    if (!users.length) {
        shareEmptyElement.classList.remove("d-none");
        return;
    }

    shareEmptyElement.classList.add("d-none");

    users.forEach(user => {
        shareUsersContainer.appendChild(createShareCandidateRow(user));
    });
}

export function getSelectedSharedMembers(shareUsersContainer: HTMLElement): ShareMemberPayload[] {
    const selected = Array.from(shareUsersContainer.querySelectorAll<HTMLInputElement>('input[name="sharedUserIds"]:checked'));
    const members: ShareMemberPayload[] = [];
    selected.forEach(input => {
        const roleSelect = requireElementById<HTMLSelectElement>("share-role-" + input.value);
        members.push({
            userId: input.value,
            role: normalizeShareRole(roleSelect.value)
        });
    });

    return members;
}

export function syncShareRoleSelectState(input: HTMLInputElement): void {
    const roleSelect = requireElementById<HTMLSelectElement>("share-role-" + input.value);
    roleSelect.disabled = !input.checked;
}

export function setColorPickerValue(input: HTMLInputElement, button: HTMLButtonElement, valueElement: HTMLElement, colorHex: string): void {
    const normalizedColorHex = normalizeColorHex(colorHex);

    input.value = normalizedColorHex;
    button.style.backgroundColor = normalizedColorHex;
    valueElement.textContent = normalizedColorHex;
}

export function getColorPickerValue(input: HTMLInputElement): string {
    return normalizeColorHex(input.value);
}

function createAccountListItem(account: AccountViewModel): HTMLLIElement {
    const item = document.createElement("li");
    item.classList.add("list-group-item", "d-flex", "justify-content-between", "align-items-start");
    if (!account.isActive) {
        item.classList.add("text-muted");
    }

    item.appendChild(createAccountInfo(account));

    if (account.canManage) {
        item.appendChild(createAccountActions(account));
    }

    return item;
}

function createAccountInfo(account: AccountViewModel): HTMLDivElement {
    const info = document.createElement("div");
    info.classList.add("me-3");

    const title = document.createElement("div");
    title.classList.add("fw-bold", "d-flex", "align-items-center", "gap-2");

    const colorBadge = document.createElement("span");
    colorBadge.classList.add("d-inline-block", "rounded-1", "border", "flex-shrink-0");
    colorBadge.style.width = "0.9rem";
    colorBadge.style.height = "0.9rem";
    colorBadge.style.backgroundColor = account.colorHex;

    const titleText = document.createElement("span");
    titleText.textContent = account.name;

    title.append(colorBadge, titleText);

    if (!account.canManage) {
        title.appendChild(createSharedAccountBadge(account));
    }

    const description = document.createElement("div");
    description.classList.add("small");
    description.textContent = account.description;

    info.append(title, description);
    return info;
}

function createSharedAccountBadge(account: AccountViewModel): HTMLSpanElement {
    const sharedBadge = document.createElement("span");
    sharedBadge.classList.add("badge", "bg-light", "text-dark", "border", "border-dark", "ms-2");
    sharedBadge.textContent = "Владелец: " + requireNonEmptyText(account.ownerName, "Не найден владелец общего счёта.");

    return sharedBadge;
}

function createAccountActions(account: AccountViewModel): HTMLDivElement {
    const actions = document.createElement("div");
    actions.classList.add("btn-group", "btn-group-sm");

    actions.append(
        createAccountActionButton("edit", account.id, "Изменить", "btn-outline-primary"),
        createAccountActionButton("toggle", account.id, account.isActive ? "Удалить" : "Восстановить", account.isActive ? "btn-outline-danger" : "btn-outline-success")
    );

    return actions;
}

function createAccountActionButton(action: string, accountId: string, text: string, buttonClass: string): HTMLButtonElement {
    const button = document.createElement("button");
    button.type = "button";
    button.classList.add("btn", buttonClass);
    button.dataset.accountAction = action;
    button.dataset.accountId = accountId;
    button.textContent = text;

    return button;
}

function createShareCandidateRow(user: ShareCandidateDto): HTMLDivElement {
    const row = document.createElement("div");
    row.classList.add("border", "rounded", "px-3", "py-2", "d-flex", "align-items-center", "gap-2");

    const input = document.createElement("input");
    input.classList.add("form-check-input", "m-0", "flex-shrink-0");
    input.type = "checkbox";
    input.id = "share-user-" + user.id;
    input.name = "sharedUserIds";
    input.value = user.id;
    input.checked = user.isSelected;

    const label = document.createElement("label");
    label.classList.add("mb-0", "flex-grow-1");
    label.htmlFor = input.id;
    label.textContent = getShareCandidateTitle(user);

    const roleSelect = createShareRoleSelect(user, input.checked);

    row.append(input, label, roleSelect);
    return row;
}

function createShareRoleSelect(user: ShareCandidateDto, isEnabled: boolean): HTMLSelectElement {
    const roleSelect = document.createElement("select");
    roleSelect.classList.add("form-select", "form-select-sm", "flex-shrink-0");
    roleSelect.id = "share-role-" + user.id;
    roleSelect.name = "sharedUserRole";
    roleSelect.style.width = "140px";
    roleSelect.disabled = !isEnabled;

    roleSelect.append(
        createShareRoleOption(ACCOUNT_ACCESS_ROLE_VIEWER, "Только просмотр"),
        createShareRoleOption(ACCOUNT_ACCESS_ROLE_EDITOR, "Редактирование")
    );
    roleSelect.value = String(normalizeShareRole(user.role));

    return roleSelect;
}

function createShareRoleOption(role: ShareAssignableRole, text: string): HTMLOptionElement {
    const option = document.createElement("option");
    option.value = String(role);
    option.textContent = text;

    return option;
}

function getShareCandidateTitle(user: ShareCandidateDto): string {
    const name = requireNonEmptyText(user.name, "Не найдено имя пользователя для настройки доступа.");
    if (!user.login) {
        return name;
    }

    return name + " (" + user.login + ")";
}

function normalizeShareRole(role: string | number | AccountAccessRole | null): ShareAssignableRole {
    if (role === null) {
        return ACCOUNT_ACCESS_ROLE_VIEWER;
    }

    const numericRole = Number(role);
    if (numericRole === ACCOUNT_ACCESS_ROLE_VIEWER) {
        return ACCOUNT_ACCESS_ROLE_VIEWER;
    }

    if (numericRole === ACCOUNT_ACCESS_ROLE_EDITOR) {
        return ACCOUNT_ACCESS_ROLE_EDITOR;
    }

    throw new Error("Некорректная роль доступа к счёту.");
}

function requireNonEmptyText(value: string | null | undefined, errorMessage: string): string {
    const text = value?.trim();
    if (!text) {
        throw new Error(errorMessage);
    }

    return text;
}
