// @ts-nocheck
import { buildJsonHeaders, sendJsonRequest } from "../shared/http.js";
import { getRequestVerificationToken } from "../shared/verificationToken.js";
import { clearElement } from "../shared/dom.js";

let usersCache = [];
let rolesCache = [];
let antiForgeryToken = null;

let editUserModalInstance = null;
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

    const modalElement = document.getElementById('editUserModal');
    if (window.bootstrap && modalElement) {
        editUserModalInstance = new bootstrap.Modal(modalElement);

        modalElement.addEventListener('hide.bs.modal', function () {
            const active = document.activeElement;
            if (active && modalElement.contains(active)) {
                active.blur();
            }
        });
    }

    const tableBody = document.getElementById('usersBody');
    if (tableBody) {
        tableBody.addEventListener('click', onUsersTableClick);
    }

    const createBtn = document.getElementById('createUserButton');
    if (createBtn) {
        createBtn.addEventListener('click', openCreateModal);
    }

    const saveBtn = document.getElementById('saveEditUserButton');
    if (saveBtn) {
        saveBtn.addEventListener('click', saveUser);
    }

    const inactiveCheckbox = document.getElementById(INACTIVE_CHECKBOX_ID);
    if (inactiveCheckbox) {
        const stored = localStorage.getItem(INACTIVE_STORAGE_KEY);
        if (stored !== null) {
            inactiveCheckbox.checked = stored === 'true';
        }

        showInactive = inactiveCheckbox.checked === true;

        inactiveCheckbox.addEventListener('change', function () {
            showInactive = inactiveCheckbox.checked === true;
            localStorage.setItem(INACTIVE_STORAGE_KEY, showInactive ? 'true' : 'false');
            loadUsers();
        });
    }

    const searchInput = document.getElementById('userSearch');
    if (searchInput) {
        searchInput.value = searchText;

        searchInput.addEventListener('input', function () {
            searchText = (searchInput.value || '').trim().toLowerCase();
            localStorage.setItem(SEARCH_STORAGE_KEY, searchText);
            applyClientFilters();
        });
    }

    ensureRolesLoaded().then(function () {
        loadUsers();
    });
}

async function ensureRolesLoaded() {
    try {
        const response = await sendJsonRequest('?handler=RoleList', 'GET', buildJsonHeaders(antiForgeryToken));
        rolesCache = Array.isArray(response.data) ? response.data : [];
    } catch (error) {
        console.error('Ошибка загрузки ролей:', error);
        rolesCache = [];
    }
}

async function loadUsers() {
    try {
        const url = showInactive ? '?handler=UserList&includeInactive=true' : '?handler=UserList';
        const response = await sendJsonRequest(url, 'GET', { 'Accept': 'application/json' });
        usersCache = Array.isArray(response.data) ? response.data : [];
        applyClientFilters();
    } catch (error) {
        console.error('Ошибка при загрузке пользователей:', error);
    }
}

function applyClientFilters() {
    if (!Array.isArray(usersCache)) {
        return;
    }

    let result = usersCache.slice();

    if (searchText) {
        result = result.filter(function (u) {
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

function renderUsersTable(users) {
    const head = document.getElementById('usersHead');
    const body = document.getElementById('usersBody');

    if (!head || !body) {
        return;
    }

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

    if (!Array.isArray(users) || users.length === 0) {
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

function createTextCell(text) {
    const td = document.createElement('td');
    td.className = 'align-middle';

    const span = document.createElement('span');
    span.classList.add('d-block');
    span.style.width = '100%';
    span.style.whiteSpace = 'nowrap';
    span.style.overflow = 'hidden';
    span.style.textOverflow = 'ellipsis';
    span.textContent = text ?? '';

    td.appendChild(span);
    return td;
}

function formatUtcDate(utcString) {
    if (!utcString) {
        return '';
    }

    const date = new Date(utcString);
    if (Number.isNaN(date.getTime())) {
        return '';
    }

    return date.toLocaleString('ru-RU');
}

function onUsersTableClick(event) {
    const editBtn = event.target.closest('.js-edit-user');
    if (editBtn) {
        const userId = editBtn.dataset.userId;
        openEditModal(userId);
        return;
    }

    const toggleBtn = event.target.closest('.js-toggle-user');
    if (toggleBtn) {
        const userId = toggleBtn.dataset.userId;
        toggleUserActive(userId, toggleBtn);
        return;
    }
}

async function openEditModal(userId) {
    if (!userId) {
        return;
    }

    isCreateMode = false;
    clearEditUserError();

    try {
        const response = await sendJsonRequest(`?handler=User&id=${userId}`, 'GET', buildJsonHeaders(antiForgeryToken));
        const user = response.data;
        fillModal(user);
        setModalTitle('Редактирование пользователя');

        if (editUserModalInstance) {
            editUserModalInstance.show();
        }
    } catch (error) {
        console.error('Ошибка загрузки пользователя:', error);
    }
}

function openCreateModal() {
    isCreateMode = true;
    clearEditUserError();

    fillModal({
        id: '',
        name: '',
        login: '',
        roleIds: []
    });

    setModalTitle('Создание пользователя');

    if (editUserModalInstance) {
        editUserModalInstance.show();
    }
}

function setModalTitle(text) {
    const titleElement = document.getElementById('editUserModalLabel');
    if (titleElement) {
        titleElement.textContent = text;
    }
}

function fillModal(user) {
    const idInput = document.getElementById('editUserId');
    const nameInput = document.getElementById('editUserName');
    const loginInput = document.getElementById('editUserLogin');
    const passwordInput = document.getElementById('editUserPassword');
    const rolesContainer = document.getElementById('editUserRolesContainer');

    if (idInput) idInput.value = user && user.id ? user.id : '';
    if (nameInput) nameInput.value = user && user.name ? user.name : '';
    if (loginInput) loginInput.value = user && user.login ? user.login : '';
    if (passwordInput) passwordInput.value = '';

    const selectedRoleIds = user && Array.isArray(user.roleIds) ? user.roleIds : [];
    renderRoleCheckboxes(rolesContainer, rolesCache, selectedRoleIds);
}

async function saveUser() {
    const idInput = document.getElementById('editUserId');
    const nameInput = document.getElementById('editUserName');
    const loginInput = document.getElementById('editUserLogin');
    const passwordInput = document.getElementById('editUserPassword');

    const idValue = idInput && idInput.value ? idInput.value : null;

    const dto = {
        id: idValue,
        name: nameInput ? nameInput.value : '',
        login: loginInput ? loginInput.value : '',
        password: passwordInput ? passwordInput.value : '',
        roleIds: getSelectedRoleIds()
    };

    const handler = isCreateMode ? 'Create' : 'Update';

    try {
        const headers = buildJsonHeaders(antiForgeryToken);
        const result = await sendJsonRequest(`?handler=${handler}`, 'POST', headers, dto);

        if (result?.success === false) {
            showEditUserError(result.message || 'Ошибка при сохранении пользователя');
            return;
        }

        if (editUserModalInstance) {
            editUserModalInstance.hide();
        }

        await loadUsers();
    } catch (error) {
        console.error('Ошибка при сохранении пользователя:', error);
        showEditUserError(error.message);
    }
}

async function toggleUserActive(userId, buttonElement) {
    if (!userId) {
        return;
    }

    if (buttonElement) {
        buttonElement.disabled = true;
    }

    try {
        await sendJsonRequest(`?handler=ToggleActive&id=${userId}`, 'POST', buildJsonHeaders(antiForgeryToken));
        await loadUsers();
    } catch (error) {
        console.error('Ошибка при изменении активности:', error);
    } finally {
        if (buttonElement) {
            buttonElement.disabled = false;
        }
    }
}

function showEditUserError(message) {
    const element = document.getElementById('editUserError');
    if (!element) {
        return;
    }

    element.textContent = message || 'Ошибка';
    element.classList.remove('d-none');
}

function clearEditUserError() {
    const element = document.getElementById('editUserError');
    if (!element) {
        return;
    }

    element.textContent = '';
    element.classList.add('d-none');
}

function renderRoleCheckboxes(container, roles, selectedRoleIds) {
    if (!container) {
        return;
    }

    container.replaceChildren();

    const selectedSet = new Set();
    if (Array.isArray(selectedRoleIds)) {
        for (const id of selectedRoleIds) {
            if (id) {
                selectedSet.add(String(id));
            }
        }
    }

    if (!Array.isArray(roles) || roles.length === 0) {
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

function getSelectedRoleIds() {
    const container = document.getElementById('editUserRolesContainer');
    if (!container) {
        return [];
    }

    const checkboxes = container.querySelectorAll('input.js-role-cb[type="checkbox"]');

    const result = [];
    for (const cb of checkboxes) {
        if (cb.checked === true && cb.value) {
            result.push(cb.value);
        }
    }

    return result;
}
