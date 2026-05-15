import { getRequestVerificationToken } from "../../../shared/verificationToken.js";
import { requireElementById, requireInputById } from "../../../shared/dom.js";
import { BootstrapModal, getOrCreateBootstrapModal } from "../../../shared/bootstrap.js";
import { createAccountAsync, loadAccountsRequestAsync, loadShareCandidatesRequestAsync, updateAccountAsync, updateMembersAsync } from "./api.js";
import { DEFAULT_ACCOUNT_COLOR_HEX, AccountPayload, AccountUpdatePayload, AccountViewModel, ShareMembersPayload } from "./models.js";
import { normalizeAccountDto, replaceAccount, requireAccountById } from "./state.js";
import { getColorPickerValue, getSelectedSharedMembers, renderAccountsList, renderShareCandidates, setColorPickerValue, syncShareRoleSelectState } from "./ui.js";

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

let accounts: AccountViewModel[] = [];
let pageElements: UserSettingsPageElements | null = null;
let modalInstance: BootstrapModal | null = null;
let shareModalInstance: BootstrapModal | null = null;
let antiForgeryToken: string | null = null;

document.addEventListener("DOMContentLoaded", function () {
    initUserSettingsPage();
});

function initUserSettingsPage(): void {
    pageElements = requireUserSettingsPageElements();
    antiForgeryToken = getRequestVerificationToken();

    modalInstance = getOrCreateBootstrapModal(pageElements.modalElement);
    shareModalInstance = getOrCreateBootstrapModal(pageElements.shareModalElement);

    pageElements.newAccountForm.addEventListener("submit", onCreateAccountFormSubmit);
    pageElements.showHiddenCheckbox.addEventListener("change", loadAccountsAsync);
    pageElements.accountsListElement.addEventListener("click", onAccountsListClick);
    pageElements.modalSaveButton.addEventListener("click", onModalSaveClick);
    pageElements.modalShareButton.addEventListener("click", onShareButtonClick);
    pageElements.shareUsersContainer.addEventListener("change", onShareUsersContainerChange);
    pageElements.shareSaveButton.addEventListener("click", onShareSaveClick);
    initAccountColorPicker(pageElements.accountColorButton, pageElements.accountColorInput, pageElements.accountColorValueElement);
    initAccountColorPicker(pageElements.modalAccountColorButton, pageElements.modalAccountColorInput, pageElements.modalAccountColorValueElement);

    void loadAccountsAsync();
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

async function loadAccountsAsync(): Promise<void> {
    const elements = requirePageElements();

    try {
        accounts = (await loadAccountsRequestAsync(elements.showHiddenCheckbox.checked)).map(normalizeAccountDto);
        renderAccountsList(elements.accountsListElement, elements.accountsEmptyElement, accounts);
    } catch (err) {
        console.error("Ошибка при загрузке счетов.", err);
    }
}

async function onCreateAccountFormSubmit(e: SubmitEvent): Promise<void> {
    e.preventDefault();

    const elements = requirePageElements();
    clearCreateAccountErrors();

    const name = elements.accountNameInput.value.trim();
    const description = elements.accountDescriptionInput.value.trim();
    const colorHex = getColorPickerValue(elements.accountColorInput);
    const payload: AccountPayload = { name: name, description: description, colorHex: colorHex };

    try {
        const created = normalizeAccountDto(await createAccountAsync(antiForgeryToken, payload));
        accounts.push(created);
        renderAccountsList(elements.accountsListElement, elements.accountsEmptyElement, accounts);

        elements.accountNameInput.value = "";
        elements.accountDescriptionInput.value = "";
        setColorPickerValue(elements.accountColorInput, elements.accountColorButton, elements.accountColorValueElement, DEFAULT_ACCOUNT_COLOR_HEX);
    } catch (err) {
        console.error("Ошибка при создании счёта.", err);
        elements.newAccountErrorElement.textContent = getErrorMessage(err, "Ошибка при создании счёта.");
    }
}

function onAccountsListClick(event: MouseEvent): void {
    if (!(event.target instanceof Element)) {
        return;
    }

    const button = event.target.closest<HTMLButtonElement>("button[data-account-action][data-account-id]");
    if (button === null) {
        return;
    }

    const accountId = requireNonEmptyText(button.dataset.accountId, "Не найден идентификатор счёта.");
    const account = requireAccountById(accounts, accountId);

    switch (button.dataset.accountAction) {
        case "edit":
            openEditModal(accountId);
            return;
        case "toggle":
            void toggleAccountActiveAsync(accountId, !account.isActive);
            return;
        default:
            throw new Error("Неизвестное действие со счётом.");
    }
}

function openEditModal(id: string): void {
    const elements = requirePageElements();
    const account = requireAccountById(accounts, id);

    elements.modalAccountIdInput.value = account.id;
    elements.modalAccountNameInput.value = account.name;
    elements.modalAccountDescriptionInput.value = account.description;
    setColorPickerValue(elements.modalAccountColorInput, elements.modalAccountColorButton, elements.modalAccountColorValueElement, account.colorHex);
    elements.modalAccountErrorElement.textContent = "";
    elements.modalShareButton.disabled = !account.canManage;

    requireModalInstance().show();
}

async function onModalSaveClick(): Promise<void> {
    const elements = requirePageElements();
    const accountId = elements.modalAccountIdInput.value;
    const name = elements.modalAccountNameInput.value.trim();
    const description = elements.modalAccountDescriptionInput.value.trim();
    const colorHex = getColorPickerValue(elements.modalAccountColorInput);

    if (!name) {
        elements.modalAccountErrorElement.textContent = "Название счёта обязательно.";
        return;
    }

    const existing = requireAccountById(accounts, accountId);
    const payload: AccountUpdatePayload = { accountId: accountId, name: name, description: description, colorHex: colorHex, isActive: existing.isActive };

    try {
        const updated = normalizeAccountDto(await updateAccountAsync(antiForgeryToken, payload));
        replaceAccount(accounts, updated);
        renderAccountsList(elements.accountsListElement, elements.accountsEmptyElement, accounts);

        requireModalInstance().hide();
    } catch (err) {
        elements.modalAccountErrorElement.textContent = getErrorMessage(err, "Ошибка при сохранении счёта.");
    }
}

async function onShareButtonClick(): Promise<void> {
    const elements = requirePageElements();
    const accountId = elements.modalAccountIdInput.value;
    const account = requireAccountById(accounts, accountId);
    if (!account.canManage) {
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
        const users = await loadShareCandidatesRequestAsync(antiForgeryToken, accountId);
        renderShareCandidates(elements.shareUsersContainer, elements.shareEmptyElement, users);
    } catch (err) {
        elements.shareErrorElement.textContent = getErrorMessage(err, "Ошибка при загрузке списка пользователей.");
    }
}

function onShareUsersContainerChange(event: Event): void {
    if (!(event.target instanceof HTMLInputElement) || event.target.name !== "sharedUserIds") {
        return;
    }

    syncShareRoleSelectState(event.target);
}

async function onShareSaveClick(): Promise<void> {
    const elements = requirePageElements();
    const payload: ShareMembersPayload = {
        accountId: elements.shareAccountIdInput.value,
        members: getSelectedSharedMembers(elements.shareUsersContainer)
    };

    elements.shareErrorElement.textContent = "";

    try {
        await updateMembersAsync(antiForgeryToken, payload);
        requireShareModalInstance().hide();
    } catch (err) {
        elements.shareErrorElement.textContent = getErrorMessage(err, "Ошибка при сохранении доступа.");
    }
}

async function toggleAccountActiveAsync(accountId: string, newIsActive: boolean): Promise<void> {
    const elements = requirePageElements();
    const existing = requireAccountById(accounts, accountId);
    const payload: AccountUpdatePayload = {
        accountId: existing.id,
        name: existing.name,
        description: existing.description,
        colorHex: existing.colorHex,
        isActive: newIsActive
    };

    try {
        const updated = normalizeAccountDto(await updateAccountAsync(antiForgeryToken, payload));
        replaceAccount(accounts, updated);
        renderAccountsList(elements.accountsListElement, elements.accountsEmptyElement, accounts);
    } catch (err) {
        console.error("Ошибка при изменении статуса счёта.", err);
    }
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

function requireNonEmptyText(value: string | null | undefined, errorMessage: string): string {
    const text = value?.trim();
    if (!text) {
        throw new Error(errorMessage);
    }

    return text;
}

function getErrorMessage(error: unknown, defaultMessage: string): string {
    if (error instanceof Error) {
        return error.message;
    }

    return defaultMessage;
}
