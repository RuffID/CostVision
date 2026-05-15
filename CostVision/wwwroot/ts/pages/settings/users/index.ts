import { buildJsonHeaders, sendJsonRequest, ServiceResult, ServiceResultWithData, unwrapServiceResult, unwrapServiceSuccess } from "../../../shared/http.js";
import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { requireElementById, requireInputById } from "../../../shared/dom.js";
import { BootstrapModal, createBootstrapModal } from "../../../shared/bootstrap.js";

interface UserDto {
    id: string;
    name: string;
    login: string;
    isActive: boolean;
    createdAtUtc: string;
    lastLoginAtUtc: string | null;
}

interface RoleDto {
    id: string;
    name: string;
    roleType: number;
}

interface UserEditDto {
    id: string;
    name: string;
    login: string;
    roleIds: string[];
}

interface UserUpsertRequest {
    id: string | null;
    name: string;
    login: string;
    password: string;
    roleIds: string[];
}

type UserListResponse = ServiceResultWithData<UserDto[]>;
type RoleListResponse = ServiceResultWithData<RoleDto[]>;
type UserEditResponse = ServiceResultWithData<UserEditDto>;
type UserSaveResponse = ServiceResult;
type ToggleUserActiveResponse = ServiceResultWithData<boolean>;

interface UsersPageElements {
    modalElement: HTMLElement;
    tableHead: HTMLTableSectionElement;
    tableBody: HTMLTableSectionElement;
    createButton: HTMLButtonElement;
    saveButton: HTMLButtonElement;
    inactiveCheckbox: HTMLInputElement;
    searchInput: HTMLInputElement;
    modalTitle: HTMLElement;
    userIdInput: HTMLInputElement;
    nameInput: HTMLInputElement;
    loginInput: HTMLInputElement;
    passwordInput: HTMLInputElement;
    rolesContainer: HTMLElement;
    errorElement: HTMLElement;
}

let usersCache: UserDto[] = [];
let rolesCache: RoleDto[] = [];
let antiForgeryToken: string | null = null;
let pageElements: UsersPageElements | null = null;

let editUserModalInstance: BootstrapModal | null = null;
let isCreateMode = false;

const INACTIVE_CHECKBOX_ID = 'showInactiveUsers';
const INACTIVE_STORAGE_KEY = 'costvision_users_showInactive';
const SEARCH_STORAGE_KEY = 'costvision_users_search';

let showInactive = false;
let searchText = localStorage.getItem(SEARCH_STORAGE_KEY) || '';

document.addEventListener('DOMContentLoaded', function () {
    initializeUsersPage();
});

function initializeUsersPage() {
    antiForgeryToken = getRequestVerificationToken();
    pageElements = requireUsersPageElements();
    editUserModalInstance = createBootstrapModal(pageElements.modalElement);

    pageElements.modalElement.addEventListener('hide.bs.modal', function () {
        const active = document.activeElement;
        if (active instanceof HTMLElement && pageElements!.modalElement.contains(active)) {
            active.blur();
        }
    });

    pageElements.tableBody.addEventListener('click', onUsersTableClick);
    pageElements.createButton.addEventListener('click', openCreateModal);
    pageElements.saveButton.addEventListener('click', saveUser);

    const stored = localStorage.getItem(INACTIVE_STORAGE_KEY);
    if (stored !== null) {
        pageElements.inactiveCheckbox.checked = stored === 'true';
    }

    showInactive = pageElements.inactiveCheckbox.checked === true;

    pageElements.inactiveCheckbox.addEventListener('change', function () {
        showInactive = pageElements!.inactiveCheckbox.checked === true;
        localStorage.setItem(INACTIVE_STORAGE_KEY, showInactive ? 'true' : 'false');
        loadUsers();
    });

    pageElements.searchInput.value = searchText;

    pageElements.searchInput.addEventListener('input', function () {
        searchText = pageElements!.searchInput.value.trim().toLowerCase();
        localStorage.setItem(SEARCH_STORAGE_KEY, searchText);
        applyClientFilters();
    });

    ensureRolesLoaded().then(function () {
        loadUsers();
    });
}

function requireUsersPageElements(): UsersPageElements {
    return {
        modalElement: requireElementById<HTMLElement>('editUserModal'),
        tableHead: requireElementById<HTMLTableSectionElement>('usersHead'),
        tableBody: requireElementById<HTMLTableSectionElement>('usersBody'),
        createButton: requireElementById<HTMLButtonElement>('createUserButton'),
        saveButton: requireElementById<HTMLButtonElement>('saveEditUserButton'),
        inactiveCheckbox: requireInputById(INACTIVE_CHECKBOX_ID),
        searchInput: requireInputById('userSearch'),
        modalTitle: requireElementById<HTMLElement>('editUserModalLabel'),
        userIdInput: requireInputById('editUserId'),
        nameInput: requireInputById('editUserName'),
        loginInput: requireInputById('editUserLogin'),
        passwordInput: requireInputById('editUserPassword'),
        rolesContainer: requireElementById<HTMLElement>('editUserRolesContainer'),
        errorElement: requireElementById<HTMLElement>('editUserError')
    };
}

function requirePageElements(): UsersPageElements {
    if (pageElements === null) {
        throw new Error('Страница пользователей не инициализирована.');
    }

    return pageElements;
}

async function ensureRolesLoaded() {
    try {
        rolesCache = await loadRolesAsync();
    } catch (error) {
        console.error('Ошибка загрузки ролей:', error);
    }
}

async function loadUsers() {
    try {
        usersCache = await loadUsersAsync();
        applyClientFilters();
    } catch (error) {
        console.error('Ошибка при загрузке пользователей:', error);
    }
}

async function loadRolesAsync(): Promise<RoleDto[]> {
    const response = await sendJsonRequest<RoleListResponse>('?handler=RoleList', 'GET', buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

async function loadUsersAsync(): Promise<UserDto[]> {
    const url = showInactive ? '?handler=UserList&includeInactive=true' : '?handler=UserList';
    const response = await sendJsonRequest<UserListResponse>(url, 'GET', { 'Accept': 'application/json' });
    return unwrapServiceResult(response);
}

async function getUserAsync(userId: string): Promise<UserEditDto> {
    const response = await sendJsonRequest<UserEditResponse>(`?handler=User&id=${userId}`, 'GET', buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

async function saveUserAsync(dto: UserUpsertRequest): Promise<void> {
    const handler = isCreateMode ? 'Create' : 'Update';
    const result = await sendJsonRequest<UserSaveResponse>(`?handler=${handler}`, 'POST', buildJsonHeaders(antiForgeryToken), dto);
    unwrapServiceSuccess(result);
}

async function toggleUserActiveAsync(userId: string): Promise<boolean> {
    const response = await sendJsonRequest<ToggleUserActiveResponse>(`?handler=ToggleActive&id=${userId}`, 'POST', buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

function applyClientFilters(): void {
    let result = usersCache.slice();

    if (searchText) {
        result = result.filter(function (u: UserDto) {
            const name = (u.name || '').toLowerCase();
            const login = (u.login || '').toLowerCase();
            return name.includes(searchText) || login.includes(searchText);
        });
    }

    if (!showInactive) {
        result = result.filter(function (u) { return u.isActive === true; });
    }

    renderUsersTable(result);
}

function renderUsersTable(users: UserDto[]): void {
    const elements = requirePageElements();
    const head = elements.tableHead;
    const body = elements.tableBody;

    head.replaceChildren();
    body.replaceChildren();

    const headers = [
        'Имя',
        'Логин',
        'Активен',
        'Создан',
        'Последняя активность',
        'Действия'
    ];

    const trh = document.createElement('tr');
    for (const h of headers) {
        const th = document.createElement('th');
        th.classList.add('align-middle', 'text-center');

        const span = document.createElement('span');
        span.classList.add('d-block');
        span.style.whiteSpace = 'nowrap';
        span.style.overflow = 'hidden';
        span.style.textOverflow = 'ellipsis';
        span.textContent = h;

        th.appendChild(span);
        trh.appendChild(th);
    }

    head.appendChild(trh);

    if (users.length === 0) {
        const emptyRow = document.createElement('tr');
        const emptyCell = document.createElement('td');
        emptyCell.colSpan = headers.length;
        emptyCell.classList.add('text-center');
        emptyCell.textContent = 'Нет данных';
        emptyRow.appendChild(emptyCell);
        body.appendChild(emptyRow);
        return;
    }

    for (const user of users) {
        const row = document.createElement('tr');

        row.appendChild(createTextCell(user.name));
        row.appendChild(createTextCell(user.login));
        row.appendChild(createTextCell(user.isActive ? 'Да' : 'Нет'));
        row.appendChild(createTextCell(formatUtcDate(user.createdAtUtc)));
        row.appendChild(createTextCell(formatUtcDate(user.lastLoginAtUtc)));

        const actionsCell = document.createElement('td');
        actionsCell.classList.add('align-middle');
        actionsCell.style.whiteSpace = 'nowrap';

        const editButton = document.createElement('button');
        editButton.type = 'button';
        editButton.className = 'btn btn-outline-primary btn-sm me-2 js-edit-user';
        editButton.dataset.userId = user.id;
        editButton.textContent = 'Редактировать';
        actionsCell.appendChild(editButton);

        const toggleButton = document.createElement('button');
        toggleButton.type = 'button';
        toggleButton.className = 'btn btn-sm js-toggle-user';
        toggleButton.dataset.userId = user.id;

        if (user.isActive) {
            toggleButton.classList.add('btn-outline-danger');
            toggleButton.textContent = 'Деактивировать';
        } else {
            toggleButton.classList.add('btn-outline-success');
            toggleButton.textContent = 'Активировать';
        }

        actionsCell.appendChild(toggleButton);

        row.appendChild(actionsCell);
        body.appendChild(row);
    }
}

function createTextCell(text: string | number | boolean | null | undefined): HTMLTableCellElement {
    const td = document.createElement('td');
    td.className = 'align-middle';

    const span = document.createElement('span');
    span.classList.add('d-block');
    span.style.width = '100%';
    span.style.whiteSpace = 'nowrap';
    span.style.overflow = 'hidden';
    span.style.textOverflow = 'ellipsis';
    span.textContent = String(text ?? '');

    td.appendChild(span);
    return td;
}

function formatUtcDate(utcString: string | null): string {
    if (!utcString) {
        return '';
    }

    const date = new Date(utcString);
    if (Number.isNaN(date.getTime())) {
        return '';
    }

    return date.toLocaleString('ru-RU');
}

function onUsersTableClick(event: MouseEvent): void {
    if (!(event.target instanceof Element)) {
        return;
    }

    const editBtn = event.target.closest<HTMLButtonElement>('.js-edit-user');
    if (editBtn) {
        const userId = editBtn.dataset.userId;
        if (!userId) {
            throw new Error('Не указан идентификатор пользователя для редактирования.');
        }

        openEditModal(userId);
        return;
    }

    const toggleBtn = event.target.closest<HTMLButtonElement>('.js-toggle-user');
    if (toggleBtn) {
        const userId = toggleBtn.dataset.userId;
        if (!userId) {
            throw new Error('Не указан идентификатор пользователя для изменения активности.');
        }

        toggleUserActive(userId, toggleBtn);
        return;
    }
}

async function openEditModal(userId: string): Promise<void> {
    isCreateMode = false;
    clearEditUserError();

    try {
        const user = await getUserAsync(userId);
        fillModal(user);
        setModalTitle('Редактирование пользователя');

        editUserModalInstance?.show();
    } catch (error) {
        console.error('Ошибка загрузки пользователя:', error);
    }
}

function openCreateModal(): void {
    isCreateMode = true;
    clearEditUserError();

    fillModal({
        id: '',
        name: '',
        login: '',
        roleIds: []
    });

    setModalTitle('Создание пользователя');

    editUserModalInstance?.show();
}

function setModalTitle(text: string): void {
    requirePageElements().modalTitle.textContent = text;
}

function fillModal(user: UserEditDto): void {
    const elements = requirePageElements();

    elements.userIdInput.value = user.id;
    elements.nameInput.value = user.name;
    elements.loginInput.value = user.login;
    elements.passwordInput.value = '';

    renderRoleCheckboxes(elements.rolesContainer, rolesCache, user.roleIds);
}

async function saveUser(): Promise<void> {
    const elements = requirePageElements();
    const idValue = elements.userIdInput.value || null;

    const dto: UserUpsertRequest = {
        id: idValue,
        name: elements.nameInput.value,
        login: elements.loginInput.value,
        password: elements.passwordInput.value,
        roleIds: getSelectedRoleIds()
    };

    try {
        await saveUserAsync(dto);

        editUserModalInstance?.hide();

        await loadUsers();
    } catch (error) {
        console.error('Ошибка при сохранении пользователя:', error);
        showEditUserError(getErrorMessage(error));
    }
}

async function toggleUserActive(userId: string, buttonElement: HTMLButtonElement): Promise<void> {
    buttonElement.disabled = true;

    try {
        await toggleUserActiveAsync(userId);
        await loadUsers();
    } catch (error) {
        console.error('Ошибка при изменении активности:', error);
    } finally {
        buttonElement.disabled = false;
    }
}

function getErrorMessage(error: unknown): string {
    if (error instanceof Error) {
        return error.message;
    }

    return 'Ошибка';
}

function showEditUserError(message: string): void {
    const element = requirePageElements().errorElement;

    element.textContent = message;
    element.classList.remove('d-none');
}

function clearEditUserError(): void {
    const element = requirePageElements().errorElement;

    element.textContent = '';
    element.classList.add('d-none');
}

function renderRoleCheckboxes(container: HTMLElement, roles: RoleDto[], selectedRoleIds: string[]): void {
    container.replaceChildren();

    const selectedSet = new Set<string>();
    for (const id of selectedRoleIds) {
        selectedSet.add(String(id));
    }

    if (roles.length === 0) {
        const empty = document.createElement('div');
        empty.classList.add('text-muted');
        empty.textContent = 'Роли не найдены';
        container.appendChild(empty);
        return;
    }

    for (const role of roles) {
        const wrapper = document.createElement('div');
        wrapper.classList.add('form-check');

        const input = document.createElement('input');
        input.type = 'checkbox';
        input.classList.add('form-check-input', 'js-role-cb');
        input.value = role.id;
        input.id = `editUserRole_${role.id}`;
        input.name = 'editUserRoleIds';

        if (selectedSet.has(String(role.id))) {
            input.checked = true;
        }

        const label = document.createElement('label');
        label.classList.add('form-check-label');
        label.htmlFor = input.id;
        label.textContent = role.name;

        wrapper.append(input, label);
        container.appendChild(wrapper);
    }
}

function getSelectedRoleIds(): string[] {
    const container = requirePageElements().rolesContainer;

    const checkboxes = Array.from(container.querySelectorAll<HTMLInputElement>('input.js-role-cb[type="checkbox"]'));

    const result: string[] = [];
    for (const cb of checkboxes) {
        if (!(cb instanceof HTMLInputElement)) {
            throw new Error('Элемент роли должен быть checkbox.');
        }

        if (cb.checked === true) {
            result.push(cb.value);
        }
    }

    return result;
}
