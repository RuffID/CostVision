import { RoleDto, UserDto } from "./models.js";

const USERS_TABLE_HEADERS = [
    "Имя",
    "Логин",
    "Активен",
    "Создан",
    "Последняя активность",
    "Действия"
];

export function renderUsersTable(head: HTMLTableSectionElement, body: HTMLTableSectionElement, users: UserDto[]): void {
    head.replaceChildren();
    body.replaceChildren();

    head.appendChild(createUsersTableHeader());

    if (users.length === 0) {
        body.appendChild(createEmptyUsersRow());
        return;
    }

    for (const user of users) {
        body.appendChild(createUserRow(user));
    }
}

export function renderRoleCheckboxes(container: HTMLElement, roles: RoleDto[], selectedRoleIds: string[]): void {
    container.replaceChildren();

    const selectedSet = new Set<string>();
    for (const id of selectedRoleIds) {
        selectedSet.add(String(id));
    }

    if (roles.length === 0) {
        const empty = document.createElement("div");
        empty.classList.add("text-muted");
        empty.textContent = "Роли не найдены";
        container.appendChild(empty);
        return;
    }

    for (const role of roles) {
        container.appendChild(createRoleCheckbox(role, selectedSet));
    }
}

export function getSelectedRoleIds(container: HTMLElement): string[] {
    const checkboxes = Array.from(container.querySelectorAll<HTMLInputElement>('input[name="editUserRoleIds"][type="checkbox"]'));
    const result: string[] = [];

    for (const checkbox of checkboxes) {
        if (checkbox.checked === true) {
            result.push(checkbox.value);
        }
    }

    return result;
}

function createUsersTableHeader(): HTMLTableRowElement {
    const row = document.createElement("tr");

    for (const header of USERS_TABLE_HEADERS) {
        const th = document.createElement("th");
        th.classList.add("align-middle");

        if (header === "Действия") {
            th.classList.add("text-center");
        } else {
            th.classList.add("text-start");
        }

        const span = document.createElement("span");
        span.classList.add("d-block");
        span.style.whiteSpace = "nowrap";
        span.style.overflow = "hidden";
        span.style.textOverflow = "ellipsis";
        span.textContent = header;

        th.appendChild(span);
        row.appendChild(th);
    }

    return row;
}

function createEmptyUsersRow(): HTMLTableRowElement {
    const row = document.createElement("tr");
    const cell = document.createElement("td");
    cell.colSpan = USERS_TABLE_HEADERS.length;
    cell.classList.add("text-center");
    cell.textContent = "Нет данных";
    row.appendChild(cell);

    return row;
}

function createUserRow(user: UserDto): HTMLTableRowElement {
    const row = document.createElement("tr");

    row.appendChild(createTextCell(user.name));
    row.appendChild(createTextCell(user.login));
    row.appendChild(createTextCell(user.isActive ? "Да" : "Нет"));
    row.appendChild(createTextCell(formatUtcDate(user.createdAtUtc)));
    row.appendChild(createTextCell(formatNullableUtcDate(user.lastLoginAtUtc)));
    row.appendChild(createUserActionsCell(user));

    return row;
}

function createTextCell(text: string | number | boolean): HTMLTableCellElement {
    const cell = document.createElement("td");
    cell.classList.add("align-middle");

    const span = document.createElement("span");
    span.classList.add("d-block");
    span.style.width = "100%";
    span.style.whiteSpace = "nowrap";
    span.style.overflow = "hidden";
    span.style.textOverflow = "ellipsis";
    span.textContent = String(text);

    cell.appendChild(span);
    return cell;
}

function createUserActionsCell(user: UserDto): HTMLTableCellElement {
    const cell = document.createElement("td");
    cell.classList.add("align-middle", "text-center");
    cell.style.whiteSpace = "nowrap";

    cell.append(
        createUserActionButton("edit", user.id, "Редактировать", "btn-outline-primary", "me-2"),
        createUserActionButton("toggle", user.id, user.isActive ? "Деактивировать" : "Активировать", user.isActive ? "btn-outline-danger" : "btn-outline-success")
    );

    return cell;
}

function createUserActionButton(action: string, userId: string, text: string, ...buttonClasses: string[]): HTMLButtonElement {
    const button = document.createElement("button");
    button.type = "button";
    button.classList.add("btn", "btn-sm", ...buttonClasses);
    button.dataset.userAction = action;
    button.dataset.userId = userId;
    button.textContent = text;

    return button;
}

function createRoleCheckbox(role: RoleDto, selectedSet: Set<string>): HTMLDivElement {
    const wrapper = document.createElement("div");
    wrapper.classList.add("form-check");

    const input = document.createElement("input");
    input.type = "checkbox";
    input.classList.add("form-check-input");
    input.value = role.id;
    input.id = `editUserRole_${role.id}`;
    input.name = "editUserRoleIds";
    input.checked = selectedSet.has(String(role.id));

    const label = document.createElement("label");
    label.classList.add("form-check-label");
    label.htmlFor = input.id;
    label.textContent = role.name;

    wrapper.append(input, label);
    return wrapper;
}

function formatNullableUtcDate(utcString: string | null): string {
    if (utcString === null) {
        return "";
    }

    return formatUtcDate(utcString);
}

function formatUtcDate(utcString: string): string {
    const date = new Date(normalizeUtcDateString(utcString));
    if (Number.isNaN(date.getTime())) {
        throw new Error("Некорректная дата пользователя.");
    }

    return date.toLocaleString("ru-RU");
}

function normalizeUtcDateString(utcString: string): string {
    if (/[zZ]|[+-]\d{2}:\d{2}$/.test(utcString)) {
        return utcString;
    }

    return `${utcString}Z`;
}
