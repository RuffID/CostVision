// @ts-nocheck
import { buildJsonHeaders, sendJsonRequest } from "../shared/http.js";
import { getRequestVerificationToken } from "../shared/verificationToken.js";
import { clearElement } from "../shared/dom.js";

let accounts = [];

let accountNameInput = null;
let accountDescriptionInput = null;
const DEFAULT_ACCOUNT_COLOR_HEX = "#0D6EFD";
let accountColorInput = null;
let accountColorButton = null;
let accountColorValueElement = null;
let newAccountErrorElement = null;
let accountNameErrorElement = null;

let showHiddenCheckbox = null;
let accountsListElement = null;
let accountsEmptyElement = null;

let modalInstance = null;
let modalAccountIdInput = null;
let modalAccountNameInput = null;
let modalAccountDescriptionInput = null;
let modalAccountColorInput = null;
let modalAccountColorButton = null;
let modalAccountColorValueElement = null;
let modalAccountErrorElement = null;
let modalShareButton = null;

let shareModalInstance = null;
let shareAccountIdInput = null;
let shareAccountTitleElement = null;
let shareUsersContainer = null;
let shareEmptyElement = null;
let shareErrorElement = null;
let shareSaveButton = null;
const ACCOUNT_ACCESS_ROLE_EDITOR = 2;
const ACCOUNT_ACCESS_ROLE_VIEWER = 3;

let antiForgeryToken = null;

document.addEventListener("DOMContentLoaded", function () {
    initUserSettingsPage();
});

function initUserSettingsPage() {
    const newAccountForm = document.getElementById("new-account-form");
    accountNameInput = document.getElementById("account-name");
    accountDescriptionInput = document.getElementById("account-description");
    accountColorInput = document.getElementById("account-color-input");
    accountColorButton = document.getElementById("account-color-button");
    accountColorValueElement = document.getElementById("account-color-value");
    newAccountErrorElement = document.getElementById("new-account-error");
    accountNameErrorElement = document.getElementById("account-name-error");

    showHiddenCheckbox = document.getElementById("show-hidden-accounts");
    accountsListElement = document.getElementById("accountsList");
    accountsEmptyElement = document.getElementById("accounts-empty");

    const modalElement = document.getElementById("account-modal");
    modalAccountIdInput = document.getElementById("modal-account-id");
    modalAccountNameInput = document.getElementById("modal-account-name");
    modalAccountDescriptionInput = document.getElementById("modal-account-description");
    modalAccountColorInput = document.getElementById("modal-account-color-input");
    modalAccountColorButton = document.getElementById("modal-account-color-button");
    modalAccountColorValueElement = document.getElementById("modal-account-color-value");
    modalAccountErrorElement = document.getElementById("modal-account-error");
    const modalSaveButton = document.getElementById("modal-save-btn");
    modalShareButton = document.getElementById("modal-share-btn");

    const shareModalElement = document.getElementById("account-share-modal");
    shareAccountIdInput = document.getElementById("share-account-id");
    shareAccountTitleElement = document.getElementById("accountShareModalLabel");
    shareUsersContainer = document.getElementById("account-share-users");
    shareEmptyElement = document.getElementById("account-share-empty");
    shareErrorElement = document.getElementById("account-share-error");
    shareSaveButton = document.getElementById("account-share-save-btn");

    antiForgeryToken = getRequestVerificationToken();

    if (modalElement && window.bootstrap && window.bootstrap.Modal) {
        modalInstance = window.bootstrap.Modal.getOrCreateInstance(modalElement);
    }
    if (shareModalElement && window.bootstrap && window.bootstrap.Modal) {
        shareModalInstance = window.bootstrap.Modal.getOrCreateInstance(shareModalElement);
    }

    if (newAccountForm) newAccountForm.addEventListener("submit", onCreateAccountFormSubmit);
    if (showHiddenCheckbox) showHiddenCheckbox.addEventListener("change", loadAccountsAsync);
    if (modalSaveButton) modalSaveButton.addEventListener("click", onModalSaveClick);
    if (modalShareButton) modalShareButton.addEventListener("click", onShareButtonClick);
    if (shareSaveButton) shareSaveButton.addEventListener("click", onShareSaveClick);
    initAccountColorPicker(accountColorButton, accountColorInput, accountColorValueElement);
    initAccountColorPicker(modalAccountColorButton, modalAccountColorInput, modalAccountColorValueElement);

    loadAccountsAsync();
}

async function loadAccountsAsync() {
    let includeInactive = showHiddenCheckbox && showHiddenCheckbox.checked;
    let url = "?handler=Accounts&includeInactive=" + (includeInactive ? "true" : "false");

    try {
        let data = await sendJsonRequest(url, "GET", { "Accept": "application/json" });
        let accountItems = Array.isArray(data.data) ? data.data : [];
        accounts = accountItems.map(normalizeAccountDto).filter(x => x);
        renderAccountsList();
    } catch (err) {
        console.error("Error loading acounts.", err);
    }
}

async function onCreateAccountFormSubmit(e) {
    e.preventDefault();

    clearCreateAccountErrors();

    let name = accountNameInput ? accountNameInput.value.trim() : "";
    let description = accountDescriptionInput ? accountDescriptionInput.value.trim() : "";
    let colorHex = getColorPickerValue(accountColorInput);

    let payload = { name: name, description: description, colorHex: colorHex };

    try {
        let data = await sendJsonRequest("?handler=CreateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);
        if (data.data) {
            let created = normalizeAccountDto(data.data);
            if (created) {
                accounts.push(created);
            }
            renderAccountsList();
        }

        if (accountNameInput) accountNameInput.value = "";
        if (accountDescriptionInput) accountDescriptionInput.value = "";
        setColorPickerValue(accountColorInput, accountColorButton, accountColorValueElement, DEFAULT_ACCOUNT_COLOR_HEX);
    } catch (err) {
        console.error("Error creating invoice.", err);
        if (newAccountErrorElement)
            newAccountErrorElement.textContent = err && err.message ? err.message : "Ошибка при создании счёта.";
    }
}

function renderAccountsList() {
    if (!accountsListElement || !accountsEmptyElement) return;

    clearElement(accountsListElement);

    if (!accounts.length) {
        accountsEmptyElement.classList.remove("d-none");
        return;
    }

    accountsEmptyElement.classList.add("d-none");

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

        accountsListElement.appendChild(li);
    });
}

function openEditModal(id) {
    let account = accounts.find(a => a.id === id);
    if (!account || !modalInstance)
        return;

    if (!modalAccountIdInput || !modalAccountNameInput || !modalAccountDescriptionInput || !modalAccountColorInput || !modalAccountErrorElement)
        return;

    modalAccountIdInput.value = account.id;
    modalAccountNameInput.value = account.name;
    modalAccountDescriptionInput.value = account.description;
    setColorPickerValue(modalAccountColorInput, modalAccountColorButton, modalAccountColorValueElement, account.colorHex);
    modalAccountErrorElement.textContent = "";
    if (modalShareButton) {
        modalShareButton.disabled = !account.canManage;
    }

    modalInstance.show();
}

async function onModalSaveClick() {
    if (!modalAccountIdInput || !modalAccountNameInput || !modalAccountDescriptionInput || !modalAccountColorInput || !modalAccountErrorElement) return;

    let accountId = modalAccountIdInput.value;
    let name = modalAccountNameInput.value.trim();
    let description = modalAccountDescriptionInput.value.trim();
    let colorHex = getColorPickerValue(modalAccountColorInput);

    if (!name) {
        modalAccountErrorElement.textContent = "Название счёта обязательно.";
        return;
    }

    let existing = accounts.find(a => a.id === accountId);
    let isActive = existing ? existing.isActive : true;

    let payload = { accountId: accountId, name: name, description: description, colorHex: colorHex, isActive: isActive };

    try {
        let data = await sendJsonRequest("?handler=UpdateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);
        if (data.data) {
            let updated = normalizeAccountDto(data.data);
            let idx = accounts.findIndex(a => a.id === updated.id);
            if (idx >= 0)
                accounts[idx] = updated;
            renderAccountsList();
        }

        if (modalInstance) modalInstance.hide();
    } catch (err) {
        modalAccountErrorElement.textContent = err && err.message ? err.message : "Ошибка при сохранении счёта.";
    }
}

async function onShareButtonClick() {
    if (!modalAccountIdInput || !shareModalInstance || !shareAccountIdInput || !shareAccountTitleElement) {
        return;
    }

    let accountId = modalAccountIdInput.value;
    let account = accounts.find(a => a.id === accountId);
    if (!account || !account.canManage) {
        return;
    }

    shareAccountIdInput.value = account.id;
    shareAccountTitleElement.textContent = `Доступ к счёту "${account.name}"`;

    await loadShareCandidatesAsync(account.id);

    if (modalInstance) {
        modalInstance.hide();
    }

    shareModalInstance.show();
}

async function loadShareCandidatesAsync(accountId) {
    if (!shareUsersContainer || !shareEmptyElement || !shareErrorElement) {
        return;
    }

    shareErrorElement.textContent = "";
    shareUsersContainer.replaceChildren();
    shareEmptyElement.classList.add("d-none");

    try {
        let response = await sendJsonRequest("?handler=ShareCandidates&accountId=" + encodeURIComponent(accountId), "GET", buildJsonHeaders(antiForgeryToken));
        let users = Array.isArray(response.data) ? response.data : [];
        renderShareCandidates(users);
    } catch (err) {
        shareErrorElement.textContent = err && err.message ? err.message : "Ошибка при загрузке списка пользователей.";
    }
}

function renderShareCandidates(users) {
    if (!shareUsersContainer || !shareEmptyElement) {
        return;
    }

    shareUsersContainer.replaceChildren();

    if (!users.length) {
        shareEmptyElement.classList.remove("d-none");
        return;
    }

    shareEmptyElement.classList.add("d-none");

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
        roleSelect.value = String(normalizeShareRole(user.role ?? user.Role));

        input.addEventListener("change", function () {
            roleSelect.disabled = !input.checked;
        });

        row.appendChild(input);
        row.appendChild(label);
        row.appendChild(roleSelect);

        shareUsersContainer.appendChild(row);
    });
}

async function onShareSaveClick() {
    if (!shareAccountIdInput || !shareUsersContainer || !shareErrorElement) {
        return;
    }

    let payload = {
        accountId: shareAccountIdInput.value,
        members: getSelectedSharedMembers()
    };

    shareErrorElement.textContent = "";

    try {
        let data = await sendJsonRequest("?handler=UpdateMembers", "POST", buildJsonHeaders(antiForgeryToken), payload);
        if (!data.success) {
            shareErrorElement.textContent = data.message || "Не удалось сохранить доступ.";
            return;
        }

        if (shareModalInstance) {
            shareModalInstance.hide();
        }
    } catch (err) {
        shareErrorElement.textContent = err && err.message ? err.message : "Ошибка при сохранении доступа.";
    }
}

function getSelectedSharedMembers() {
    if (!shareUsersContainer) {
        return [];
    }

    let selected = shareUsersContainer.querySelectorAll('input[name="sharedUserIds"]:checked');
    let members = [];
    selected.forEach(input => {
        if (input.value) {
            let roleSelect = document.getElementById("share-role-" + input.value);
            members.push({
                userId: input.value,
                role: normalizeShareRole(roleSelect ? roleSelect.value : ACCOUNT_ACCESS_ROLE_VIEWER)
            });
        }
    });

    return members;
}

async function toggleAccountActiveAsync(accountId, newIsActive) {
    let existing = accounts.find(a => a.id === accountId);
    if (!existing) {
        console.error("Счёт не найден в локальном списке");
        return;
    }

    let payload = {
        accountId: existing.id,
        name: existing.name,
        description: existing.description,
        colorHex: existing.colorHex,
        isActive: newIsActive
    };

    try {
        let data = await sendJsonRequest("?handler=UpdateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);
        if (data.data) {
            let updated = normalizeAccountDto(data.data);
            let idx = accounts.findIndex(a => a.id === updated.id);
            if (idx >= 0) {
                accounts[idx] = updated;
            }
        }

        renderAccountsList();
    } catch (err) {
        console.error("Failed to change account status.", err);
    }
}

function normalizeAccountDto(dto) {
    if (!dto) return null;

    return {
        id: dto.id || dto.Id || "",
        name: dto.name || dto.Name || "",
        description: dto.description || dto.Description || "",
        colorHex: normalizeColorHex(dto.colorHex ?? dto.ColorHex),
        isActive: dto.isActive ?? dto.IsActive ?? true,
        canManage: dto.canManage ?? dto.CanManage ?? false,
        ownerName: dto.ownerName || dto.OwnerName || "",
        accessRole: dto.accessRole ?? dto.AccessRole ?? 0
    };
}

function clearCreateAccountErrors() {
    if (newAccountErrorElement) newAccountErrorElement.textContent = "";
    if (accountNameErrorElement) accountNameErrorElement.textContent = "";
}

function initAccountColorPicker(button, input, valueElement) {
    if (!button || !input) {
        return;
    }

    button.addEventListener("click", function () {
        input.click();
    });

    input.addEventListener("input", function () {
        setColorPickerValue(input, button, valueElement, input.value);
    });

    setColorPickerValue(input, button, valueElement, input.value || DEFAULT_ACCOUNT_COLOR_HEX);
}

function setColorPickerValue(input, button, valueElement, colorHex) {
    let normalizedColorHex = normalizeColorHex(colorHex);

    if (input) {
        input.value = normalizedColorHex;
    }

    if (button) {
        button.style.backgroundColor = normalizedColorHex;
    }

    if (valueElement) {
        valueElement.textContent = normalizedColorHex;
    }
}

function getColorPickerValue(input) {
    return normalizeColorHex(input ? input.value : DEFAULT_ACCOUNT_COLOR_HEX);
}

function normalizeColorHex(colorHex) {
    let value = (colorHex || "").trim().toUpperCase();
    return /^#[0-9A-F]{6}$/.test(value) ? value : DEFAULT_ACCOUNT_COLOR_HEX;
}

function normalizeShareRole(role) {
    let numericRole = Number(role);
    if (numericRole === ACCOUNT_ACCESS_ROLE_EDITOR) {
        return ACCOUNT_ACCESS_ROLE_EDITOR;
    }

    return ACCOUNT_ACCESS_ROLE_VIEWER;
}
