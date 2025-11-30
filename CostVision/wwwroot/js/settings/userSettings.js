let accounts = [];

let accountNameInput = null;
let accountDescriptionInput = null;
let accountIsDefaultInput = null;
let newAccountErrorElement = null;
let accountNameErrorElement = null;

let showHiddenCheckbox = null;
let accountsListElement = null;
let accountsEmptyElement = null;

let modalInstance = null;
let modalAccountIdInput = null;
let modalAccountNameInput = null;
let modalAccountDescriptionInput = null;
let modalAccountIsDefaultInput = null;
let modalAccountErrorElement = null;

let antiForgeryToken = null;

document.addEventListener("DOMContentLoaded", function () {
    initUserSettingsPage();
});

function initUserSettingsPage() {
    const newAccountForm = document.getElementById("new-account-form");
    accountNameInput = document.getElementById("account-name");
    accountDescriptionInput = document.getElementById("account-description");
    accountIsDefaultInput = document.getElementById("account-isdefault");
    newAccountErrorElement = document.getElementById("new-account-error");
    accountNameErrorElement = document.getElementById("account-name-error");

    showHiddenCheckbox = document.getElementById("show-hidden-accounts");
    accountsListElement = document.getElementById("accountsList");
    accountsEmptyElement = document.getElementById("accounts-empty");

    const modalElement = document.getElementById("account-modal");
    modalAccountIsDefaultInput = document.getElementById("modal-account-isdefault");
    modalAccountIdInput = document.getElementById("modal-account-id");
    modalAccountNameInput = document.getElementById("modal-account-name");
    modalAccountDescriptionInput = document.getElementById("modal-account-description");
    modalAccountErrorElement = document.getElementById("modal-account-error");
    const modalSaveButton = document.getElementById("modal-save-btn");

    antiForgeryToken = getRequestVerificationToken();

    if (modalElement && window.bootstrap && window.bootstrap.Modal) {
        modalInstance = window.bootstrap.Modal.getOrCreateInstance(modalElement);
    }

    if (newAccountForm) newAccountForm.addEventListener("submit", onCreateAccountFormSubmit);
    if (showHiddenCheckbox) showHiddenCheckbox.addEventListener("change", loadAccountsAsync);
    if (modalSaveButton) modalSaveButton.addEventListener("click", onModalSaveClick);

    loadAccountsAsync();
}

async function loadAccountsAsync() {
    let includeInactive = showHiddenCheckbox && showHiddenCheckbox.checked;
    let url = "?handler=Accounts&includeInactive=" + (includeInactive ? "true" : "false");

    try {
        let data = await sendJsonRequest(url, "GET", { "Accept": "application/json" });

        accounts = Array.isArray(data.accounts) ? data.accounts.map(normalizeAccountDto).filter(x => x) : [];
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
    let isDefault = accountIsDefaultInput.checked ? accountIsDefaultInput.checked : false;

    let payload = { name: name, description: description, isDefault: isDefault };

    try {
        let data = await sendJsonRequest("?handler=CreateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);

        if (!data.success) {
            // Показываем ошибку под нужным полем
            if (data.errorMessage) {
                if (accountNameErrorElement) {
                    accountNameErrorElement.textContent = data.errorMessage;
                } else if (newAccountErrorElement) {
                    // fallback – общий контейнер
                    newAccountErrorElement.textContent = data.errorMessage;
                }
            } else if (newAccountErrorElement) {
                newAccountErrorElement.textContent = "Не удалось создать счёт.";
            }
            return;
        }

        if (data.account) {
            let created = normalizeAccountDto(data.account);
            if (created) {
                // Снять флаг по умолчанию со всех существующих
                if (created.isDefault) {
                    accounts.forEach(a => { a.isDefault = false; });
                }
                accounts.push(created);
            }
            renderAccountsList();
        }

        if (accountNameInput) accountNameInput.value = "";
        if (accountDescriptionInput) accountDescriptionInput.value = "";
    } catch (err) {
        console.error("Error creating invoice.", err);
        if (newAccountErrorElement)
            newAccountErrorElement.textContent = "Ошибка при создании счёта.";
    }
}

function renderAccountsList() {
    if (!accountsListElement || !accountsEmptyElement) return;

    accountsListElement.innerHTML = "";

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
        title.className = "fw-bold";
        title.textContent = acc.name;

        if (acc.isDefault) {
            let badge = document.createElement("span");
            badge.className = "badge bg-primary ms-2";
            badge.textContent = "По умолчанию";
            title.appendChild(badge);
        }

        let desc = document.createElement("div");
        desc.className = "small";
        desc.textContent = acc.description;

        info.appendChild(title);
        info.appendChild(desc);

        let actions = document.createElement("div");
        actions.className = "btn-group btn-group-sm";

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

        li.appendChild(info);
        li.appendChild(actions);

        accountsListElement.appendChild(li);
    });
}

function openEditModal(id) {
    let account = accounts.find(a => a.id === id);
    if (!account || !modalInstance)
        return;

    if (!modalAccountIdInput || !modalAccountNameInput || !modalAccountDescriptionInput || !modalAccountIsDefaultInput || !modalAccountErrorElement)
        return;

    modalAccountIdInput.value = account.id;
    modalAccountNameInput.value = account.name;
    modalAccountDescriptionInput.value = account.description;
    modalAccountIsDefaultInput.checked = !!account.isDefault;
    modalAccountErrorElement.textContent = "";

    modalInstance.show();
}

async function onModalSaveClick() {
    if (!modalAccountIdInput || !modalAccountNameInput || !modalAccountDescriptionInput || !modalAccountErrorElement) return;

    let accountId = modalAccountIdInput.value;
    let name = modalAccountNameInput.value.trim();
    let description = modalAccountDescriptionInput.value.trim();
    let isDefault = modalAccountIsDefaultInput ? modalAccountIsDefaultInput.checked : false;

    if (!name) {
        modalAccountErrorElement.textContent = "Название счёта обязательно.";
        return;
    }

    let existing = accounts.find(a => a.id === accountId);
    let isActive = existing ? existing.isActive : true;

    let payload = { accountId: accountId, name: name, description: description, isActive: isActive, isDefault: isDefault };

    try {
        let data = await sendJsonRequest("?handler=UpdateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);

        if (!data.success) {
            modalAccountErrorElement.textContent = data.errorMessage || "Не удалось сохранить изменения.";
            return;
        }

        if (data.account) {
            let updated = normalizeAccountDto(data.account);

            if (updated.isDefault) {
                // новый "по умолчанию" – снимаем флаг с остальных
                accounts.forEach(a => {
                    a.isDefault = (a.id === updated.id);
                });
            } else {
                let updated = normalizeAccountDto(data.account);
                let idx = accounts.findIndex(a => a.id === updated.id);
                if (idx >= 0)
                    accounts[idx] = updated;
            }
            renderAccountsList();
        }

        if (modalInstance) modalInstance.hide();
    } catch (err) {
        modalAccountErrorElement.textContent = "Ошибка при сохранении счёта.";
    }
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
        isActive: newIsActive,
        isDefault: existing.isDefault
    };

    try {
        let data = await sendJsonRequest("?handler=UpdateAccount", "POST", buildJsonHeaders(antiForgeryToken), payload);

        if (!data.success) {
            console.error("Failed to change account status.");
            return;
        }

        if (data.account) {
            let updated = normalizeAccountDto(data.account);
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
        isActive: dto.isActive ?? dto.IsActive ?? true,
        isDefault: dto.isDefault ?? dto.IsDefault ?? false
    };
}

function clearCreateAccountErrors() {
    if (newAccountErrorElement) newAccountErrorElement.textContent = "";
    if (accountNameErrorElement) accountNameErrorElement.textContent = "";
}