import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { requireElementById, requireInputById } from "../../../shared/dom.js";
import { BootstrapModal, createBootstrapModal } from "../../../shared/bootstrap.js";
import { getUserAsync, loadRolesAsync, loadUsersAsync, saveUserAsync, toggleUserActiveAsync } from "./api.js";
import { INACTIVE_CHECKBOX_ID, INACTIVE_STORAGE_KEY, SEARCH_STORAGE_KEY, RoleDto, UserDto, UserEditDto, UserUpsertRequest } from "./models.js";
import { filterUsers } from "./state.js";
import { getSelectedRoleIds, renderRoleCheckboxes, renderUsersTable } from "./ui.js";

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
let showInactive = false;
let searchText = localStorage.getItem(SEARCH_STORAGE_KEY) || "";

document.addEventListener("DOMContentLoaded", function () {
    initializeUsersPage();
});

function initializeUsersPage(): void {
    antiForgeryToken = getRequestVerificationToken();
    pageElements = requireUsersPageElements();
    editUserModalInstance = createBootstrapModal(pageElements.modalElement);

    pageElements.modalElement.addEventListener("hide.bs.modal", onEditUserModalHide);
    pageElements.tableBody.addEventListener("click", onUsersTableClick);
    pageElements.createButton.addEventListener("click", openCreateModal);
    pageElements.saveButton.addEventListener("click", saveUser);

    initializeFilters(pageElements);

    void ensureRolesLoadedAndLoadUsers();
}

function requireUsersPageElements(): UsersPageElements {
    return {
        modalElement: requireElementById<HTMLElement>("editUserModal"),
        tableHead: requireElementById<HTMLTableSectionElement>("usersHead"),
        tableBody: requireElementById<HTMLTableSectionElement>("usersBody"),
        createButton: requireElementById<HTMLButtonElement>("createUserButton"),
        saveButton: requireElementById<HTMLButtonElement>("saveEditUserButton"),
        inactiveCheckbox: requireInputById(INACTIVE_CHECKBOX_ID),
        searchInput: requireInputById("userSearch"),
        modalTitle: requireElementById<HTMLElement>("editUserModalLabel"),
        userIdInput: requireInputById("editUserId"),
        nameInput: requireInputById("editUserName"),
        loginInput: requireInputById("editUserLogin"),
        passwordInput: requireInputById("editUserPassword"),
        rolesContainer: requireElementById<HTMLElement>("editUserRolesContainer"),
        errorElement: requireElementById<HTMLElement>("editUserError")
    };
}

function requirePageElements(): UsersPageElements {
    if (pageElements === null) {
        throw new Error("Страница пользователей не инициализирована.");
    }

    return pageElements;
}

function requireEditUserModalInstance(): BootstrapModal {
    if (editUserModalInstance === null) {
        throw new Error("Модальное окно пользователя не инициализировано.");
    }

    return editUserModalInstance;
}

function initializeFilters(elements: UsersPageElements): void {
    const stored = localStorage.getItem(INACTIVE_STORAGE_KEY);
    if (stored !== null) {
        elements.inactiveCheckbox.checked = stored === "true";
    }

    showInactive = elements.inactiveCheckbox.checked === true;
    elements.searchInput.value = searchText;

    elements.inactiveCheckbox.addEventListener("change", onInactiveFilterChange);
    elements.searchInput.addEventListener("input", onSearchInput);
}

function onEditUserModalHide(): void {
    const elements = requirePageElements();
    const active = document.activeElement;
    if (active instanceof HTMLElement && elements.modalElement.contains(active)) {
        active.blur();
    }
}

function onInactiveFilterChange(): void {
    const elements = requirePageElements();
    showInactive = elements.inactiveCheckbox.checked === true;
    localStorage.setItem(INACTIVE_STORAGE_KEY, showInactive ? "true" : "false");
    void loadUsers();
}

function onSearchInput(): void {
    const elements = requirePageElements();
    searchText = elements.searchInput.value.trim().toLowerCase();
    localStorage.setItem(SEARCH_STORAGE_KEY, searchText);
    applyClientFilters();
}

async function ensureRolesLoadedAndLoadUsers(): Promise<void> {
    await ensureRolesLoaded();
    await loadUsers();
}

async function ensureRolesLoaded(): Promise<void> {
    try {
        rolesCache = await loadRolesAsync(antiForgeryToken);
    } catch (error) {
        console.error("Ошибка загрузки ролей:", error);
    }
}

async function loadUsers(): Promise<void> {
    try {
        usersCache = await loadUsersAsync(showInactive);
        applyClientFilters();
    } catch (error) {
        console.error("Ошибка при загрузке пользователей:", error);
    }
}

function applyClientFilters(): void {
    const elements = requirePageElements();
    renderUsersTable(elements.tableHead, elements.tableBody, filterUsers(usersCache, showInactive, searchText));
}

function onUsersTableClick(event: MouseEvent): void {
    if (!(event.target instanceof Element)) {
        return;
    }

    const button = event.target.closest<HTMLButtonElement>("button[data-user-action][data-user-id]");
    if (button === null) {
        return;
    }

    const userId = requireNonEmptyText(button.dataset.userId, "Не указан идентификатор пользователя.");

    switch (button.dataset.userAction) {
        case "edit":
            void openEditModal(userId);
            return;
        case "toggle":
            void toggleUserActive(userId, button);
            return;
        default:
            throw new Error("Неизвестное действие с пользователем.");
    }
}

async function openEditModal(userId: string): Promise<void> {
    isCreateMode = false;
    clearEditUserError();

    try {
        const user = await getUserAsync(antiForgeryToken, userId);
        fillModal(user);
        setModalTitle("Редактирование пользователя");

        requireEditUserModalInstance().show();
    } catch (error) {
        console.error("Ошибка загрузки пользователя:", error);
    }
}

function openCreateModal(): void {
    isCreateMode = true;
    clearEditUserError();

    fillModal({
        id: "",
        name: "",
        login: "",
        roleIds: []
    });

    setModalTitle("Создание пользователя");
    requireEditUserModalInstance().show();
}

function setModalTitle(text: string): void {
    requirePageElements().modalTitle.textContent = text;
}

function fillModal(user: UserEditDto): void {
    const elements = requirePageElements();

    elements.userIdInput.value = user.id;
    elements.nameInput.value = user.name;
    elements.loginInput.value = user.login;
    elements.passwordInput.value = "";

    renderRoleCheckboxes(elements.rolesContainer, rolesCache, user.roleIds);
}

async function saveUser(): Promise<void> {
    const elements = requirePageElements();
    const dto: UserUpsertRequest = {
        id: elements.userIdInput.value || null,
        name: elements.nameInput.value,
        login: elements.loginInput.value,
        password: elements.passwordInput.value,
        roleIds: getSelectedRoleIds(elements.rolesContainer)
    };

    try {
        await saveUserAsync(antiForgeryToken, isCreateMode, dto);

        requireEditUserModalInstance().hide();
        await loadUsers();
    } catch (error) {
        console.error("Ошибка при сохранении пользователя:", error);
        showEditUserError(getErrorMessage(error));
    }
}

async function toggleUserActive(userId: string, buttonElement: HTMLButtonElement): Promise<void> {
    buttonElement.disabled = true;

    try {
        await toggleUserActiveAsync(antiForgeryToken, userId);
        await loadUsers();
    } catch (error) {
        console.error("Ошибка при изменении активности:", error);
    } finally {
        buttonElement.disabled = false;
    }
}

function showEditUserError(message: string): void {
    const element = requirePageElements().errorElement;

    element.textContent = message;
    element.classList.remove("d-none");
}

function clearEditUserError(): void {
    const element = requirePageElements().errorElement;

    element.textContent = "";
    element.classList.add("d-none");
}

function requireNonEmptyText(value: string | null | undefined, errorMessage: string): string {
    const text = value?.trim();
    if (!text) {
        throw new Error(errorMessage);
    }

    return text;
}

function getErrorMessage(error: unknown): string {
    if (error instanceof Error) {
        return error.message;
    }

    return "Ошибка";
}
