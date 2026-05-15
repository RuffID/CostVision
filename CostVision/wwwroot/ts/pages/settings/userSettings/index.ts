import { buildJsonHeaders, sendJsonRequest, ServiceResultWithData, unwrapServiceResult } from "../../../shared/http.js";
import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { clearElement, requireElementById, requireInputById } from "../../../shared/dom.js";
import { BootstrapModal, getOrCreateBootstrapModal } from "../../../shared/bootstrap.js";

interface AccountDto {
    id: string;
    name: string;
    description: string | null;
    colorHex: string;
    isActive: boolean;
    canManage: boolean;
    ownerName: string;
    accessRole: AccountAccessRole;
}

interface ShareCandidateDto {
    id: string;
    name: string;
    login: string;
    isSelected: boolean;
    role: AccountAccessRole | null;
}

interface AccountPayload {
    name: string;
    description: string;
    colorHex: string;
}

interface AccountUpdatePayload extends AccountPayload {
    accountId: string;
    isActive: boolean;
}

interface ShareMemberPayload {
    userId: string;
    role: ShareAssignableRole;
}

interface ShareMembersPayload {
    accountId: string;
    members: ShareMemberPayload[];
}

interface UserSettingsPageElements {
    newAccountForm: HTMLFormElement;
    accountNameInput: HTMLInputElement;
    accountDescriptionInput: HTMLInputElement;
    accountColorInput: HTMLInputElement;
    accountColorButton: HTMLButtonElement;
    accountColorValueElement: HTMLElement;
    newAccountErrorElement: HTMLElement;
    accountNameErrorElement: HTMLElement;
    showHiddenCheckbox: HTMLInputElement;
    accountsListElement: HTMLElement;
    accountsEmptyElement: HTMLElement;
    modalElement: HTMLElement;
    modalAccountIdInput: HTMLInputElement;
    modalAccountNameInput: HTMLInputElement;
    modalAccountDescriptionInput: HTMLInputElement;
    modalAccountColorInput: HTMLInputElement;
    modalAccountColorButton: HTMLButtonElement;
    modalAccountColorValueElement: HTMLElement;
    modalAccountErrorElement: HTMLElement;
    modalSaveButton: HTMLButtonElement;
    modalShareButton: HTMLButtonElement;
    shareModalElement: HTMLElement;
    shareAccountIdInput: HTMLInputElement;
    shareAccountTitleElement: HTMLElement;
    shareUsersContainer: HTMLElement;
    shareEmptyElement: HTMLElement;
    shareErrorElement: HTMLElement;
    shareSaveButton: HTMLButtonElement;
}

type AccountsResponse = ServiceResultWithData<AccountDto[]>;
type AccountResponse = ServiceResultWithData<AccountDto>;
type ShareCandidatesResponse = ServiceResultWithData<ShareCandidateDto[]>;
type UpdateMembersResponse = ServiceResultWithData<boolean>;

const enum AccountAccessRole {
    Viewer = 0,
    Editor = 1,
    Owner = 2
}

type ShareAssignableRole = AccountAccessRole.Viewer | AccountAccessRole.Editor;

let accounts: AccountDto[] = [];
let pageElements: UserSettingsPageElements | null = null;

const DEFAULT_ACCOUNT_COLOR_HEX = "#0D6EFD";

let modalInstance: BootstrapModal | null = null;
let shareModalInstance: BootstrapModal | null = null;
const ACCOUNT_ACCESS_ROLE_EDITOR = AccountAccessRole.Editor;
const ACCOUNT_ACCESS_ROLE_VIEWER = AccountAccessRole.Viewer;

let antiForgeryToken: string | null = null;

document.addEventListener("DOMContentLoaded", function () {
    initUserSettingsPage();
});

function initUserSettingsPage() {
    pageElements = requireUserSettingsPageElements();
    antiForgeryToken = getRequestVerificationToken();

    modalInstance = getOrCreateBootstrapModal(pageElements.modalElement);
    shareModalInstance = getOrCreateBootstrapModal(pageElements.shareModalElement);

    pageElements.newAccountForm.addEventListener("submit", onCreateAccountFormSubmit);
    pageElements.showHiddenCheckbox.addEventListener("change", loadAccountsAsync);
    pageElements.modalSaveButton.addEventListener("click", onModalSaveClick);
    pageElements.modalShareButton.addEventListener("click", onShareButtonClick);
    pageElements.shareSaveButton.addEventListener("click", onShareSaveClick);
    initAccountColorPicker(pageElements.accountColorButton, pageElements.accountColorInput, pageElements.accountColorValueElement);
    initAccountColorPicker(pageElements.modalAccountColorButton, pageElements.modalAccountColorInput, pageElements.modalAccountColorValueElement);

    loadAccountsAsync();
}

function requireUserSettingsPageElements(): UserSettingsPageElements {
    return {
        newAccountForm: requireElementById<HTMLFormElement>("new-account-form"),
        accountNameInput: requireInputById("account-name"),
        accountDescriptionInput: requireInputById("account-description"),
        accountColorInput: requireInputById("account-color-input"),
        accountColorButton: requireElementById<HTMLButtonElement>("account-color-button"),
        accountColorValueElement: requireElementById<HTMLElement>("account-color-value"),
        newAccountErrorElement: requireElementById<HTMLElement>("new-account-error"),
        accountNameErrorElement: requireElementById<HTMLElement>("account-name-error"),
        showHiddenCheckbox: requireInputById("show-hidden-accounts"),
        accountsListElement: requireElementById<HTMLElement>("accountsList"),
        accountsEmptyElement: requireElementById<HTMLElement>("accounts-empty"),
        modalElement: requireElementById<HTMLElement>("account-modal"),
        modalAccountIdInput: requireInputById("modal-account-id"),
        modalAccountNameInput: requireInputById("modal-account-name"),
        modalAccountDescriptionInput: requireInputById("modal-account-description"),
        modalAccountColorInput: requireInputById("modal-account-color-input"),
        modalAccountColorButton: requireElementById<HTMLButtonElement>("modal-account-color-button"),
        modalAccountColorValueElement: requireElementById<HTMLElement>("modal-account-color-value"),
        modalAccountErrorElement: requireElementById<HTMLElement>("modal-account-error"),
        modalSaveButton: requireElementById<HTMLButtonElement>("modal-save-btn"),
        modalShareButton: requireElementById<HTMLButtonElement>("modal-share-btn"),
        shareModalElement: requireElementById<HTMLElement>("account-share-modal"),
        shareAccountIdInput: requireInputById("share-account-id"),
        shareAccountTitleElement: requireElementById<HTMLElement>("accountShareModalLabel"),
        shareUsersContainer: requireElementById<HTMLElement>("account-share-users"),
        shareEmptyElement: requireElementById<HTMLElement>("account-share-empty"),
        shareErrorElement: requireElementById<HTMLElement>("account-share-error"),
        shareSaveButton: requireElementById<HTMLButtonElement>("account-share-save-btn")
    };
}

function requirePageElements(): UserSettingsPageElements {
    if (pageElements === null) {
        throw new Error("Страница настроек пользователя не инициализирована.");
    }

    return pageElements;
}

function requireModalInstance(): BootstrapModal {
    if (modalInstance === null) {
        throw new Error("Модальное окно счёта не инициализировано.");
    }

    return modalInstance;
}

function requireShareModalInstance(): BootstrapModal {
    if (shareModalInstance === null) {
        throw new Error("Модальное окно доступа не инициализировано.");
    }

    return shareModalInstance;
}

async function loadAccountsAsync() {
    const elements = requirePageElements();

    try {
        accounts = (await loadAccountsRequestAsync(elements.showHiddenCheckbox.checked)).map(normalizeAccountDto);
        renderAccountsList();
    } catch (err) {
        console.error("Error loading acounts.", err);
    }
}

async function loadAccountsRequestAsync(includeInactive: boolean): Promise<AccountDto[]> {
    const url = "?handler=Accounts&includeInactive=" + (includeInactive ? "true" : "false");
    const response = await sendJsonRequest<AccountsResponse>(url, "GET", { "Accept": "application/json" });
    return unwrapServiceResult(response);
}

async function createAccountAsync(payload: AccountPayload): Promise<AccountDto> {
    const response = await sendJsonRequest<AccountResponse>("?handler=CreateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response);
}

async function updateAccountAsync(payload: AccountUpdatePayload): Promise<AccountDto> {
    const response = await sendJsonRequest<AccountResponse>("?handler=UpdateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);
    return unwrapServiceResult(response);
}

async function loadShareCandidatesRequestAsync(accountId: string): Promise<ShareCandidateDto[]> {
    const response = await sendJsonRequest<ShareCandidatesResponse>("?handler=ShareCandidates&accountId=" + encodeURIComponent(accountId), "GET", buildJsonHeaders(antiForgeryToken));
    return unwrapServiceResult(response);
}

async function updateMembersAsync(payload: ShareMembersPayload): Promise<void> {
    const response = await sendJsonRequest<UpdateMembersResponse>("?handler=UpdateMembers", "POST", buildJsonHeaders(antiForgeryToken), payload);
    unwrapServiceResult(response);
}

async function onCreateAccountFormSubmit(e: SubmitEvent): Promise<void> {
    e.preventDefault();

    const elements = requirePageElements();
    clearCreateAccountErrors();

    let name = elements.accountNameInput.value.trim();
    let description = elements.accountDescriptionInput.value.trim();
    let colorHex = getColorPickerValue(elements.accountColorInput);

    let payload: AccountPayload = { name: name, description: description, colorHex: colorHex };

    try {
        let created = normalizeAccountDto(await createAccountAsync(payload));
        accounts.push(created);
        renderAccountsList();

        elements.accountNameInput.value = "";
        elements.accountDescriptionInput.value = "";
        setColorPickerValue(elements.accountColorInput, elements.accountColorButton, elements.accountColorValueElement, DEFAULT_ACCOUNT_COLOR_HEX);
    } catch (err) {
        console.error("Error creating invoice.", err);
        elements.newAccountErrorElement.textContent = getErrorMessage(err, "Ошибка при создании счёта.");
    }
}

function renderAccountsList(): void {
    const elements = requirePageElements();

    clearElement(elements.accountsListElement);

    if (!accounts.length) {
        elements.accountsEmptyElement.classList.remove("d-none");
        return;
    }

    elements.accountsEmptyElement.classList.add("d-none");

    accounts.forEach(acc => {
        let li = document.createElement("li");
        li.className = "list-group-item d-flex justify-content-between align-items-start";
        if (!acc.isActive) li.classList.add("text-muted");

        let info = document.createElement("div");
        info.className = "me-3";

        let title = document.createElement("div");
        title.className = "fw-bold d-flex align-items-center gap-2";

        let colorBadge = document.createElement("span");
        colorBadge.className = "d-inline-block rounded-1 border flex-shrink-0";
        colorBadge.style.width = "0.9rem";
        colorBadge.style.height = "0.9rem";
        colorBadge.style.backgroundColor = acc.colorHex;

        let titleText = document.createElement("span");
        titleText.textContent = acc.name;

        title.appendChild(colorBadge);
        title.appendChild(titleText);

        if (!acc.canManage) {
            let sharedBadge = document.createElement("span");
            sharedBadge.className = "badge bg-light text-dark border border-dark ms-2";
            sharedBadge.textContent = "Владелец: " + (acc.ownerName || "—");
            title.appendChild(sharedBadge);
        }

        let desc = document.createElement("div");
        desc.className = "small";
        desc.textContent = acc.description;

        info.appendChild(title);
        info.appendChild(desc);

        let actions = document.createElement("div");
        actions.className = "btn-group btn-group-sm";

        if (acc.canManage) {
            let editBtn = document.createElement("button");
            editBtn.type = "button";
            editBtn.className = "btn btn-outline-primary";
            editBtn.textContent = "Изменить";
            editBtn.addEventListener("click", () => openEditModal(acc.id));

            let toggleBtn = document.createElement("button");
            toggleBtn.type = "button";
            toggleBtn.className = acc.isActive ? "btn btn-outline-danger" : "btn btn-outline-success";
            toggleBtn.textContent = acc.isActive ? "Удалить" : "Восстановить";
            toggleBtn.addEventListener("click", () => toggleAccountActiveAsync(acc.id, !acc.isActive));

            actions.appendChild(editBtn);
            actions.appendChild(toggleBtn);
        }

        li.appendChild(info);
        if (actions.childElementCount > 0) {
            li.appendChild(actions);
        }

        elements.accountsListElement.appendChild(li);
    });
}

function openEditModal(id: string): void {
    const elements = requirePageElements();
    let account = accounts.find(a => a.id === id);
    if (!account) {
        throw new Error("Счёт не найден в локальном списке.");
    }

    elements.modalAccountIdInput.value = account.id;
    elements.modalAccountNameInput.value = account.name;
    elements.modalAccountDescriptionInput.value = account.description ?? "";
    setColorPickerValue(elements.modalAccountColorInput, elements.modalAccountColorButton, elements.modalAccountColorValueElement, account.colorHex);
    elements.modalAccountErrorElement.textContent = "";
    elements.modalShareButton.disabled = !account.canManage;

    requireModalInstance().show();
}

async function onModalSaveClick(): Promise<void> {
    const elements = requirePageElements();

    let accountId = elements.modalAccountIdInput.value;
    let name = elements.modalAccountNameInput.value.trim();
    let description = elements.modalAccountDescriptionInput.value.trim();
    let colorHex = getColorPickerValue(elements.modalAccountColorInput);

    if (!name) {
        elements.modalAccountErrorElement.textContent = "Название счёта обязательно.";
        return;
    }

    let existing = accounts.find(a => a.id === accountId);
    if (!existing) {
        throw new Error("Счёт не найден в локальном списке.");
    }

    let payload: AccountUpdatePayload = { accountId: accountId, name: name, description: description, colorHex: colorHex, isActive: existing.isActive };

    try {
        let updated = normalizeAccountDto(await updateAccountAsync(payload));
        replaceAccount(updated);
        renderAccountsList();

        requireModalInstance().hide();
    } catch (err) {
        elements.modalAccountErrorElement.textContent = getErrorMessage(err, "Ошибка при сохранении счёта.");
    }
}

async function onShareButtonClick(): Promise<void> {
    const elements = requirePageElements();

    let accountId = elements.modalAccountIdInput.value;
    let account = accounts.find(a => a.id === accountId);
    if (!account || !account.canManage) {
        throw new Error("Счёт недоступен для управления доступом.");
    }

    elements.shareAccountIdInput.value = account.id;
    elements.shareAccountTitleElement.textContent = `Доступ к счёту "${account.name}"`;

    await loadShareCandidatesAsync(account.id);

    requireModalInstance().hide();

    requireShareModalInstance().show();
}

async function loadShareCandidatesAsync(accountId: string): Promise<void> {
    const elements = requirePageElements();

    elements.shareErrorElement.textContent = "";
    elements.shareUsersContainer.replaceChildren();
    elements.shareEmptyElement.classList.add("d-none");

    try {
        let users = await loadShareCandidatesRequestAsync(accountId);
        renderShareCandidates(users);
    } catch (err) {
        elements.shareErrorElement.textContent = getErrorMessage(err, "Ошибка при загрузке списка пользователей.");
    }
}

function renderShareCandidates(users: ShareCandidateDto[]): void {
    const elements = requirePageElements();

    elements.shareUsersContainer.replaceChildren();

    if (!users.length) {
        elements.shareEmptyElement.classList.remove("d-none");
        return;
    }

    elements.shareEmptyElement.classList.add("d-none");

    users.forEach(user => {
        let row = document.createElement("div");
        row.className = "border rounded px-3 py-2 d-flex align-items-center gap-2";

        let input = document.createElement("input");
        input.className = "form-check-input m-0 flex-shrink-0";
        input.type = "checkbox";
        input.id = "share-user-" + user.id;
        input.name = "sharedUserIds";
        input.value = user.id;
        input.checked = !!user.isSelected;

        let label = document.createElement("label");
        label.className = "mb-0 flex-grow-1";
        label.htmlFor = input.id;

        let displayName = user.name || user.login || "Без имени";
        let loginText = user.login ? " (" + user.login + ")" : "";
        label.textContent = displayName + loginText;

        let roleSelect = document.createElement("select");
        roleSelect.className = "form-select form-select-sm flex-shrink-0";
        roleSelect.id = "share-role-" + user.id;
        roleSelect.name = "sharedUserRole";
        roleSelect.style.width = "140px";
        roleSelect.disabled = !input.checked;

        let viewerOption = document.createElement("option");
        viewerOption.value = String(ACCOUNT_ACCESS_ROLE_VIEWER);
        viewerOption.textContent = "Только просмотр";

        let editorOption = document.createElement("option");
        editorOption.value = String(ACCOUNT_ACCESS_ROLE_EDITOR);
        editorOption.textContent = "Редактирование";

        roleSelect.appendChild(viewerOption);
        roleSelect.appendChild(editorOption);
        roleSelect.value = String(normalizeShareRole(user.role));

        input.addEventListener("change", function () {
            roleSelect.disabled = !input.checked;
        });

        row.appendChild(input);
        row.appendChild(label);
        row.appendChild(roleSelect);

        elements.shareUsersContainer.appendChild(row);
    });
}

async function onShareSaveClick(): Promise<void> {
    const elements = requirePageElements();

    let payload: ShareMembersPayload = {
        accountId: elements.shareAccountIdInput.value,
        members: getSelectedSharedMembers()
    };

    elements.shareErrorElement.textContent = "";

    try {
        await updateMembersAsync(payload);

        requireShareModalInstance().hide();
    } catch (err) {
        elements.shareErrorElement.textContent = getErrorMessage(err, "Ошибка при сохранении доступа.");
    }
}

function getSelectedSharedMembers(): ShareMemberPayload[] {
    const elements = requirePageElements();

    let selected = Array.from(elements.shareUsersContainer.querySelectorAll<HTMLInputElement>('input[name="sharedUserIds"]:checked'));
    let members: ShareMemberPayload[] = [];
    selected.forEach(input => {
        if (input.value) {
            let roleSelect = requireElementById<HTMLSelectElement>("share-role-" + input.value);
            members.push({
                userId: input.value,
                role: normalizeShareRole(roleSelect.value)
            });
        }
    });

    return members;
}

async function toggleAccountActiveAsync(accountId: string, newIsActive: boolean): Promise<void> {
    let existing = accounts.find(a => a.id === accountId);
    if (!existing) {
        throw new Error("Счёт не найден в локальном списке.");
    }

    let payload: AccountUpdatePayload = {
        accountId: existing.id,
        name: existing.name,
        description: existing.description ?? "",
        colorHex: existing.colorHex,
        isActive: newIsActive
    };

    try {
        let updated = normalizeAccountDto(await updateAccountAsync(payload));
        replaceAccount(updated);
        renderAccountsList();
    } catch (err) {
        console.error("Failed to change account status.", err);
    }
}

function replaceAccount(account: AccountDto): void {
    let idx = accounts.findIndex(a => a.id === account.id);
    if (idx < 0) {
        throw new Error("Счёт не найден в локальном списке.");
    }

    accounts[idx] = {
        ...account,
        colorHex: normalizeColorHex(account.colorHex),
        description: account.description ?? ""
    };
}

function normalizeAccountDto(dto: AccountDto): AccountDto {
    return {
        ...dto,
        description: dto.description ?? "",
        colorHex: normalizeColorHex(dto.colorHex)
    };
}

function clearCreateAccountErrors(): void {
    const elements = requirePageElements();
    elements.newAccountErrorElement.textContent = "";
    elements.accountNameErrorElement.textContent = "";
}

function initAccountColorPicker(button: HTMLButtonElement, input: HTMLInputElement, valueElement: HTMLElement): void {
    button.addEventListener("click", function () {
        input.click();
    });

    input.addEventListener("input", function () {
        setColorPickerValue(input, button, valueElement, input.value);
    });

    setColorPickerValue(input, button, valueElement, input.value || DEFAULT_ACCOUNT_COLOR_HEX);
}

function setColorPickerValue(input: HTMLInputElement, button: HTMLButtonElement, valueElement: HTMLElement, colorHex: string): void {
    let normalizedColorHex = normalizeColorHex(colorHex);

    input.value = normalizedColorHex;
    button.style.backgroundColor = normalizedColorHex;
    valueElement.textContent = normalizedColorHex;
}

function getColorPickerValue(input: HTMLInputElement): string {
    return normalizeColorHex(input.value);
}

function normalizeColorHex(colorHex: string | null | undefined): string {
    let value = (colorHex || "").trim().toUpperCase();
    if (!/^#[0-9A-F]{6}$/.test(value)) {
        throw new Error("Некорректный HEX-цвет счёта.");
    }

    return value;
}

function normalizeShareRole(role: string | number | AccountAccessRole | null): ShareAssignableRole {
    if (role === null) {
        return ACCOUNT_ACCESS_ROLE_VIEWER;
    }

    let numericRole = Number(role);
    if (numericRole === ACCOUNT_ACCESS_ROLE_VIEWER) {
        return ACCOUNT_ACCESS_ROLE_VIEWER;
    }

    if (numericRole === ACCOUNT_ACCESS_ROLE_EDITOR) {
        return ACCOUNT_ACCESS_ROLE_EDITOR;
    }

    throw new Error("Некорректная роль доступа к счёту.");
}

function getErrorMessage(error: unknown, defaultMessage: string): string {
    if (error instanceof Error) {
        return error.message;
    }

    return defaultMessage;
}
