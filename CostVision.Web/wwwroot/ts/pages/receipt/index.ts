import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { clearElement, requireElementById, requireInputById, requireSelectById } from "../../shared/dom.js";
import { initFileDropzone } from "../../shared/dropzone.js";
import { loadReceiptAccountsAsync } from "./accountsApi.js";
import { setCameraButtonState } from "./cameraUi.js";
import { getFilesFromFileList } from "./fileScanner.js";
import { isImageFile } from "./imageProcessing.js";
import { submitManualReceiptAsync } from "./manualReceipt.js";
import { getStatusClassName } from "./receiptStatusRender.js";
import { submitQrScanAsync } from "./qrScanner.js";
import { DecodedQrFileResult, ImageDebugInfo, ImageSize, ManualReceiptPayload, QrScanPayload, QrScanResult } from "./types.js";

let qrScanForm;
let qrScanResultsContainer;
let qrScanFileInput;
let qrScanDropzone;

let qrScanResultIndex = 0;
let qrScanHtml5QrcodeScanner;
let qrScanFileScanner;
let qrScanLastCameraText = null;
let qrScanLastCameraTs = 0;
let qrScanAvailableCameras = [];
let qrScanCurrentCameraId = null;

let qrScanOverlay = null;
let antiForgeryToken = null;
let accountSelect = null;
let accountSelectError = null;
const ACCOUNT_STORAGE_KEY = "qrScan.selectedAccountId";
const MANUAL_RECEIPT_DRAFT_STORAGE_KEY = "qrScan.manualReceiptDraft";
const CAMERA_STORAGE_KEY = "qrScan.selectedCameraId";
let qrScanResults = [];
let qrScanLastSubmitSource = null;
let qrScanCameraPaused = false;
let qrScanCameraModal = null;
let qrScanCameraModalInstance = null;

document.addEventListener("DOMContentLoaded", function () {
    initQrScan().catch(function (error) {
        console.error("QR scan: ошибка инициализации страницы", error);
    });
    initManualCheckValidation();
    initCollapseSections();
    initGlobalPasteHandler(); // глобальный Ctrl+V по всей странице
});

async function initQrScan() {
    qrScanForm = requireElementById<HTMLFormElement>("qrForm");
    qrScanResultsContainer = requireElementById<HTMLElement>("decoded-inputs");
    qrScanFileInput = requireInputById("multiFileInput");
    qrScanDropzone = requireElementById<HTMLElement>("qr-dropzone");

    const qrReaderElement = requireElementById<HTMLElement>("qr-reader");
    const fileScanRootElement = requireElementById<HTMLElement>("file-scan-root");
    accountSelect = requireSelectById("accountSelect");
    accountSelectError = requireElementById<HTMLElement>("accountSelectError");
    antiForgeryToken = getRequestVerificationToken();

    const mobile = isMobileDevice();

    if (accountSelect) {
        // при смене — сохранять выбор
        accountSelect.addEventListener("change", function () {
            localStorage.setItem(ACCOUNT_STORAGE_KEY, accountSelect.value);
        });

        await loadAvailableAccountsAsync();
    }

    // Перехватываем submit формы, чтобы всегда работать через AJAX
    qrScanForm.addEventListener("submit", function (e) {
        e.preventDefault();
        qrScanSubmitFormAjax();
    });

    // ====== Камера: только мобильные ======
    if (mobile && qrReaderElement) {
        document.body.classList.add("qr-mobile-scan-mode");
        if (qrScanDropzone) {
            qrScanDropzone.closest(".qr-upload-block")?.classList.add("d-none");
        }
        await initQrScanMobileScannerUiAsync(qrReaderElement);
    } else if (qrReaderElement) {
        // На ПК виджет камеры не показываем вообще
        qrReaderElement.style.display = "none";
    }

    // ====== Сканер файлов (общий для всех устройств) ======
    qrScanFileScanner = new Html5Qrcode("file-scan-root");

    // Инициализация drag-and-drop холста
    if (qrScanDropzone)
        initQrScanDropzone();
}

async function loadAvailableAccountsAsync() {
    if (!accountSelect) {
        throw new Error("Не найден список счетов.");
    }

    accountSelect.disabled = true;
    renderAvailableAccounts([]);

    try {
        const accountItems = (await loadReceiptAccountsAsync(antiForgeryToken)).map(normalizeReceiptAccountDto);

        if (accountItems.length === 0) {
            throw new Error("Нет доступных счетов для сохранения чека.");
        }

        renderAvailableAccounts(accountItems);
        restoreSelectedAccount(accountItems);
        accountSelect.disabled = false;
        hideAccountSelectError();
    } catch (error) {
        renderAccountSelectError(error);
        throw error;
    }
}

function renderAvailableAccounts(accounts) {
    if (!accountSelect) {
        return;
    }

    accountSelect.replaceChildren();

    if (!Array.isArray(accounts) || accounts.length === 0) {
        const loadingOption = document.createElement("option");
        loadingOption.value = "";
        loadingOption.textContent = "Загрузка счетов...";
        accountSelect.appendChild(loadingOption);
        return;
    }

    accounts.forEach(function (account) {
        const option = document.createElement("option");
        option.value = account.id;
        option.textContent = account.name;
        accountSelect.appendChild(option);
    });
}

function restoreSelectedAccount(accounts) {
    if (!accountSelect || !Array.isArray(accounts) || accounts.length === 0) {
        return;
    }

    const savedId = localStorage.getItem(ACCOUNT_STORAGE_KEY);
    if (savedId && hasOption(accountSelect, savedId)) {
        accountSelect.value = savedId;
        return;
    }

    accountSelect.value = accounts[0].id;
    localStorage.setItem(ACCOUNT_STORAGE_KEY, accountSelect.value);
}

function normalizeReceiptAccountDto(dto) {
    if (!dto) {
        throw new Error("Счёт недоступен.");
    }

    const id = dto.id || "";
    const name = dto.name || "";

    if (!id || !name) {
        throw new Error("Счёт получен без обязательных полей.");
    }

    return {
        id: id,
        name: name
    };
}

function renderAccountSelectError(error) {
    if (accountSelect) {
        accountSelect.disabled = true;
        accountSelect.replaceChildren();

        const errorOption = document.createElement("option");
        errorOption.value = "";
        errorOption.textContent = "Не удалось загрузить счета";
        accountSelect.appendChild(errorOption);
    }

    if (!accountSelectError) {
        return;
    }

    const message = error && error.message ? error.message : "Не удалось загрузить счета.";
    accountSelectError.textContent = message;
    accountSelectError.classList.remove("d-none");
}

function hideAccountSelectError() {
    if (!accountSelectError) {
        return;
    }

    accountSelectError.textContent = "";
    accountSelectError.classList.add("d-none");
}

function qrScanSetCollapseState(button, body, isCollapsed) {
    if (!button || !body) {
        return;
    }

    button.classList.toggle("qr-section__header--collapsed", isCollapsed);
    button.setAttribute("aria-expanded", (!isCollapsed).toString());
    body.classList.toggle("qr-section__body--collapsed", isCollapsed);
    body.classList.toggle("d-none", isCollapsed);

    const arrow = button.querySelector(".qr-section__arrow");
    if (arrow) {
        arrow.textContent = isCollapsed ? "▸" : "▾";
    }
}

// ===================== AJAX-отправка формы =====================

async function qrScanSubmitFormAjax() {
    if (!qrScanForm) {
        return;
    }

    const url = qrScanForm.getAttribute("action") || window.location.href;

    try {
        const payload: QrScanPayload = {
            accountId: getSelectedAccountId(),
            results: qrScanResults
        };

        const data = await submitQrScanAsync(url, payload, antiForgeryToken);
        qrScanApplyServerResponse(data);
    } catch (err) {
        console.error("QR scan: ошибка AJAX-запроса", err);
        qrScanResumeCameraAfterFeedback();
    } finally {
        // любой поток, который инициировал запрос (drag&drop, выбор файла, Ctrl+V, камера, ручной ввод через AJAX)
        // должен снять оверлей здесь
        qrScanHideOverlay();
    }
}

function qrScanApplyServerResponse(responseData) {
    const scannedCount = typeof responseData.scannedCount === "number" ? responseData.scannedCount : 0;
    const addedToDbCount = typeof responseData.addedToDbCount === "number" ? responseData.addedToDbCount : 0;
    const errorCount = typeof responseData.errorCount === "number" ? responseData.errorCount : 0;
    const errorMessage = typeof responseData.message === "string" ? responseData.message : "";
    const results = Array.isArray(responseData.results) ? responseData.results : [];

    qrScanRenderStatus(scannedCount, addedToDbCount, errorCount, errorMessage, results);
    qrScanRenderResults(results);

    if (document.body.classList.contains("qr-mobile-scan-mode") &&
        (qrScanLastSubmitSource === "camera" || qrScanLastSubmitSource === "files")) {
        qrScanShowCameraReceiptFeedback(results, addedToDbCount, errorCount, errorMessage, qrScanLastSubmitSource);
    }
}

function qrScanEnsureStatusContainer() {
    if (document.body.classList.contains("qr-mobile-scan-mode")) {
        return null;
    }

    let container = document.querySelector(".qr-status");
    if (container) {
        return container;
    }

    container = document.createElement("div");
    container.className = "qr-status d-flex flex-column align-items-start gap-2 mt-3 mb-4";

    // Вставить над выбором счёта
    if (accountSelect) {
        // Берём не сам select, а его "строку" / form-group, если есть
        let anchor = accountSelect.closest(".form-group, .mb-3, .mb-4, .row") || accountSelect;

        if (anchor && anchor.parentNode) {
            anchor.parentNode.insertBefore(container, anchor);
            return container;
        }
    }

    return container;
}

function qrScanRenderStatus(scannedCount, addedToDbCount, errorCount, errorMessage, results) {
    const container = qrScanEnsureStatusContainer();
    if (!container) {
        return;
    }

    clearElement(container);

    const normalizedResults = Array.isArray(results) ? results : [];
    const invalidQrCount = normalizedResults.filter(function (result) {
        return !!result &&
            typeof result.decodedText === "string" &&
            result.decodedText.trim().length > 0 &&
            typeof result.errorMessage === "string" &&
            result.errorMessage.trim().length > 0;
    }).length;
    const duplicateCount = Math.max(0, scannedCount - addedToDbCount - invalidQrCount);

    if (addedToDbCount > 0) {
        const addedItem = document.createElement("div");
        addedItem.className = "qr-status__item alert alert-success py-2 px-3 mb-0 d-inline-block w-auto";

        const addedText = document.createElement("div");
        addedText.className = "qr-status__text mb-0";
        if (addedToDbCount === 1) {
            const addedReceipt = qrScanFindFirstSuccessfulParsedResult(normalizedResults);
            addedText.textContent = addedReceipt
                ? qrScanBuildAddedReceiptStatusText(addedReceipt.parsed)
                : "Добавлен чек";
        } else {
            addedText.textContent = "Добавлено чеков: " + addedToDbCount;
        }

        addedItem.appendChild(addedText);
        container.appendChild(addedItem);
    }

    if (duplicateCount > 0) {
        const duplicateItem = document.createElement("div");
        duplicateItem.className = "qr-status__item alert alert-warning py-2 px-3 mb-0 d-inline-block w-auto";

        const duplicateText = document.createElement("div");
        duplicateText.className = "qr-status__text mb-0";
        duplicateText.textContent = "Уже добавлено: " + duplicateCount;

        duplicateItem.appendChild(duplicateText);
        container.appendChild(duplicateItem);
    }

    if (errorCount > 0) {
        const errorItem = document.createElement("div");
        errorItem.className = "qr-status__item alert alert-danger py-2 px-3 mb-0 d-inline-block w-auto";

        const errorText = document.createElement("div");
        errorText.className = "qr-status__text mb-0";
        if (errorCount === 1) {
            const failedReceipt = qrScanFindFirstFailedParsedResult(normalizedResults);
            errorText.textContent = failedReceipt
                ? qrScanBuildFailedReceiptStatusText(failedReceipt.parsed)
                : "Не удалось добавить чек";
        } else {
            errorText.textContent = "Ошибок: " + errorCount;
        }

        errorItem.appendChild(errorText);
        container.appendChild(errorItem);
    }

    if (errorMessage && errorMessage.length > 0) {
        const err = document.createElement("div");
        err.className = "qr-status__item alert alert-danger py-2 px-3 mb-0 d-inline-block w-auto";

        const title = document.createElement("div");
        title.className = "qr-status__title fw-semibold";
        title.textContent = "Ошибка при сканировании";

        const text = document.createElement("div");
        text.className = "qr-status__text";
        text.textContent = errorMessage;

        err.appendChild(title);
        err.appendChild(text);
        container.appendChild(err);
    }
}

function qrScanRenderManualStatus(message, statusType) {
    const container = qrScanEnsureStatusContainer();
    if (!container) {
        return;
    }

    clearElement(container);

    const item = document.createElement("div");
    item.className = "qr-status__item alert py-2 px-3 mb-0 d-inline-block w-auto " + qrScanGetStatusClassName(statusType);

    const title = document.createElement("div");
    title.className = "qr-status__title fw-semibold";
    title.textContent = "Ручной ввод чека";

    const text = document.createElement("div");
    text.className = "qr-status__text";
    text.textContent = message;

    item.appendChild(title);
    item.appendChild(text);
    container.appendChild(item);
}

function qrScanGetStatusClassName(statusType) {
    return getStatusClassName(statusType);
}

function qrScanRenderResults(results) {
    if (document.body.classList.contains("qr-mobile-scan-mode")) {
        const existingSection = document.getElementById("decoded-results-section");
        if (existingSection) {
            const wrapper = existingSection.closest(".qr-section");
            if (wrapper) {
                wrapper.remove();
            }
        }

        return;
    }

    if (!results || results.length === 0) {
        return;
    }

    let sectionBody = document.getElementById("decoded-results-section");
    let ul;

    if (!sectionBody) {
        const wrapper = document.createElement("div");
        wrapper.className = "qr-section mt-4 rounded-4 border overflow-hidden";
        wrapper.style.backgroundColor = "#F9FAFB";
        wrapper.style.borderColor = "#d1d5db";
        wrapper.style.borderRadius = "1rem";
        wrapper.style.width = "min(100%, 41rem)";

        const headerBtn = document.createElement("button");
        headerBtn.type = "button";
        headerBtn.className = "qr-section__header qr-section__header--collapsed btn w-100 d-flex align-items-center justify-content-between text-start px-4 py-3 border-0 rounded-top-4 bg-transparent fw-bold fs-6";
        headerBtn.setAttribute("data-collapse-target", "#decoded-results-section");
        headerBtn.setAttribute("aria-expanded", "false");
        headerBtn.style.backgroundColor = "transparent";
        headerBtn.style.border = "0";

        const headerTextSpan = document.createElement("span");
        headerTextSpan.textContent = "Результат обработки";

        const headerArrowSpan = document.createElement("span");
        headerArrowSpan.className = "qr-section__arrow text-body-secondary fs-5";
        headerArrowSpan.setAttribute("aria-hidden", "true");
        headerArrowSpan.textContent = "▸";

        headerBtn.appendChild(headerTextSpan);
        headerBtn.appendChild(headerArrowSpan);

        sectionBody = document.createElement("div");
        sectionBody.id = "decoded-results-section";
        sectionBody.className = "qr-section__body qr-section__body--collapsed card-body d-none bg-body-tertiary";
        sectionBody.style.boxSizing = "border-box";
        sectionBody.style.width = "100%";
        sectionBody.style.backgroundColor = "#F9FAFB";
        sectionBody.style.borderTop = "1px solid #d1d5db";

        ul = document.createElement("ul");
        ul.className = "list-group list-group-flush bg-transparent";
        sectionBody.appendChild(ul);

        wrapper.appendChild(headerBtn);
        wrapper.appendChild(sectionBody);

        const manualRoot = document.getElementById("manual-request-root");
        const manualSection = manualRoot ? manualRoot.closest(".qr-section") || manualRoot : null;

        if (manualSection && manualSection.parentNode) {
            manualSection.parentNode.insertBefore(wrapper, manualSection.nextSibling);
        } else if (qrScanForm && qrScanForm.parentNode) {
            qrScanForm.parentNode.insertBefore(wrapper, qrScanForm.nextSibling);
        } else {
            document.body.appendChild(wrapper);
        }
    } else {
        ul = sectionBody.querySelector("ul");
        if (!ul) {
            ul = document.createElement("ul");
            ul.className = "list-group list-group-flush bg-transparent";
            sectionBody.appendChild(ul);
        }
    }

    clearElement(ul);

    for (let i = 0; i < results.length; i++) {
        const r = results[i];
        const li = document.createElement("li");
        li.className = "list-group-item px-0 bg-transparent";

        const fileName = document.createElement("strong");
        fileName.textContent = r.fileName || "Файл";

        const headerSpan = document.createElement("span");
        headerSpan.className = "text-nowrap";

        let dateText = "Без даты";
        if (r.photoDateTime) {
            try {
                const dt = new Date(r.photoDateTime);
                if (!Number.isNaN(dt.getTime())) {
                    dateText = dt.toLocaleString();
                }
            } catch {
                // игнорировать ошибки парсинга даты
            }
        }

        const dateStrong = document.createElement("strong");
        dateStrong.textContent = dateText;

        headerSpan.appendChild(dateStrong);

        li.appendChild(fileName);
        li.appendChild(document.createTextNode(", "));
        li.appendChild(headerSpan);
        li.appendChild(document.createElement("br"));

        qrScanAppendProcessingResultDetails(li, r);

        ul.appendChild(li);
    }
}

function createStrongText(text) {
    const element = document.createElement("strong");
    element.textContent = text;
    return element;
}

function qrScanFindFirstSuccessfulParsedResult(results) {
    return results.find(function (result) {
        return !!result && !result.errorMessage && !!result.parsed;
    }) || null;
}

function qrScanFindFirstFailedParsedResult(results) {
    return results.find(function (result) {
        return !!result && !!result.errorMessage && !!result.parsed;
    }) || null;
}

function qrScanBuildAddedReceiptStatusText(parsed) {
    return "[" + qrScanFormatReceiptDateForStatus(parsed.dateTime) + "] Добавлен чек на сумму: " + qrScanFormatReceiptSumForStatus(parsed.sum);
}

function qrScanBuildFailedReceiptStatusText(parsed) {
    return "Не удалось добавить чек на сумму: " + qrScanFormatReceiptSumForStatus(parsed.sum) + ", от " + qrScanFormatReceiptDateForStatus(parsed.dateTime);
}

function qrScanAppendProcessingResultDetails(container, result) {
    const statusDiv = document.createElement("div");
    statusDiv.className = result.errorMessage ? "fw-semibold text-danger" : "fw-semibold text-success";
    statusDiv.textContent = result.errorMessage
        ? "Не загружен: " + result.errorMessage
        : "Загружен";
    container.appendChild(statusDiv);

    if (!result.parsed) {
        return;
    }

    const parsedDiv = document.createElement("div");
    parsedDiv.className = "small text-body-secondary mt-1";
    parsedDiv.append(
        document.createTextNode("Чек от: "),
        createStrongText(qrScanFormatReceiptDateForStatus(result.parsed.dateTime)),
        document.createTextNode(", сумма: "),
        createStrongText(qrScanFormatReceiptSumForStatus(result.parsed.sum)),
        document.createTextNode(", ФН: "),
        createStrongText(result.parsed.fiscalDriveNumber || "не указан"),
        document.createTextNode(", ФД: "),
        createStrongText(result.parsed.fiscalDocumentNumber || "не указан"),
        document.createTextNode(", ФП: "),
        createStrongText(result.parsed.fiscalSign || "не указан")
    );

    container.appendChild(parsedDiv);
}

function qrScanFormatReceiptDateForStatus(value) {
    if (!value) {
        return "дата не указана";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    const day = String(date.getDate()).padStart(2, "0");
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const year = date.getFullYear();
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");

    return `${hours}:${minutes} ${day}.${month}.${year}`;
}

function qrScanFormatReceiptSumForStatus(value) {
    const numericValue = typeof value === "number" ? value : parseFloat(value);
    if (Number.isNaN(numericValue)) {
        return "сумма не указана";
    }

    return new Intl.NumberFormat("ru-RU", {
        minimumFractionDigits: 1,
        maximumFractionDigits: 2
    }).format(numericValue) + " ₽";
}

// ===================== Работа с результатами (Results) =====================

function qrScanClearResults() {
    if (qrScanResultsContainer) {
        clearElement(qrScanResultsContainer);
    }

    qrScanResults = [];
    qrScanResultIndex = 0;
}

function qrScanAddResult(fileName, decodedText, errorMessage, debugInfo, photoDateTimeIso) {
    const result = {
        fileName: fileName || "",
        decodedText: decodedText || "",
        errorMessage: errorMessage || null,
        debugInfo: debugInfo || null,
        photoDateTime: photoDateTimeIso || null
    }

    qrScanResults.push(result);
    qrScanResultIndex = qrScanResults.length;
}

// ===================== Камера =====================
function qrScanOnScanSuccess(decodedText, decodedResult) {
    // анти-спам: если тот же самый текст уже сканили недавно — игнорируем
    const now = Date.now();
    const debounceMs = 5000; // окно, в течение которого повтор одного и того же текста не уходит на сервер

    if (qrScanLastCameraText === decodedText && (now - qrScanLastCameraTs) < debounceMs) {
        return;
    }

    qrScanLastCameraText = decodedText;
    qrScanLastCameraTs = now;

    qrScanClearResults();
    qrScanLastSubmitSource = "camera";
    qrScanPauseCameraForFeedback();

    const debugInfo = {
        fileName: "CAMERA",
        contentType: "camera",
        fileSizeBytes: 0,
        width: 0,
        height: 0
    };

    qrScanAddResult("CAMERA", decodedText, "", debugInfo, null);

    // один запрос на сервер на один успешный скан
    qrScanSubmitFormAjax();
}

function qrScanOnScanError(errorMessage) {
    // игнорировать ошибки сканирования
    // console.warn("QR scan error:", errorMessage);
}

// ===================== Файлы (множественный выбор и DnD) =====================
async function qrScanScanFilesAndSubmit(fileList: FileList | File[]) {
    qrScanClearResults();
    qrScanLastSubmitSource = "files";

    const files = Array.isArray(fileList) ? fileList : getFilesFromFileList(fileList);

    for (let i = 0; i < files.length; i++) {
        const file = files[i];

        const baseDebug = {
            fileName: file.name,
            contentType: file.type || "",
            fileSizeBytes: file.size || 0,
            width: 0,
            height: 0
        };

        let sizeInfo = null;
        try {
            sizeInfo = await getImageSizeFromFile(file);
            baseDebug.width = sizeInfo.width;
            baseDebug.height = sizeInfo.height;
        } catch (e) {
            console.warn(`Не удалось определить размер для ${file.name}`, e);
        }

        let photoDateTimeIso = null;
        try {
            photoDateTimeIso = (await qrScanGetPhotoDateFromFile(file)) || new Date(file.lastModified).toISOString();
        } catch (e) {
            console.warn(`Не удалось прочитать EXIF для ${file.name}`, e);
        }

        try {
            const result = await qrScanDecodeFile(file);
            qrScanAddResult(file.name, result.decodedText, "", baseDebug, photoDateTimeIso);
        } catch (err) {
            console.warn(`Файл ${file.name} не распознан:`, err);
            qrScanAddResult(file.name, "", "Не удалось распознать QR-код", baseDebug, photoDateTimeIso);
        }
    }

    try {
        await qrScanFileScanner.clear();
    } catch (e) {
        console.warn("Не удалось очистить fileScanner", e);
    }

    if (qrScanResultIndex > 0) {
        qrScanSubmitFormAjax();
    } else {
        console.warn("Ни один файл не содержит читаемого кода");
        // если до этого показывали оверлей – скрыть его
        qrScanHideOverlay();
    }
}

// ===================== Drag-and-drop холст =====================

function initQrScanDropzone() {
    initFileDropzone({
        dropzone: qrScanDropzone,
        fileInput: qrScanFileInput,
        multiple: true,
        onFilesSelected: function (files) {
            qrScanShowOverlay();
            qrScanScanFilesAndSubmit(files);
        }
    });
}

function qrScanHandlePaste(event) {
    const clipboardData = event.clipboardData;
    if (!clipboardData || !clipboardData.items) {
        return;
    }

    // не перехватывать вставку в текстовые поля / textarea / contentEditable
    const target = event.target;
    const tagName = (target && target.tagName) ? target.tagName.toUpperCase() : "";
    const isEditable =
        tagName === "INPUT" ||
        tagName === "TEXTAREA" ||
        (target && target.isContentEditable);

    if (isEditable) {
        // даём обычной вставке отработать
        return;
    }

    const files = [];

    for (let i = 0; i < clipboardData.items.length; i++) {
        const item = clipboardData.items[i];
        if (item.kind === "file") {
            const file = item.getAsFile();
            if (file) {
                files.push(file);
            }
        }
    }

    if (files.length === 0) {
        return;
    }

    event.preventDefault();
    event.stopPropagation();

    // запускаем обработку вставленных файлов с оверлеем
    qrScanShowOverlay();
    qrScanScanFilesAndSubmit(files);
}

// ===================== Оверлей загрузки =====================
function qrScanEnsureOverlay() {
    if (qrScanOverlay) {
        return qrScanOverlay;
    }

    const overlay = document.createElement("div");
    overlay.className = "qr-overlay position-fixed top-0 start-0 w-100 h-100 d-none align-items-center justify-content-center flex-column gap-3";
    overlay.style.backgroundColor = "rgba(15, 23, 42, 0.55)";
    overlay.style.zIndex = "1080";

    const spinner = document.createElement("div");
    spinner.className = "spinner-border text-light";
    spinner.setAttribute("role", "status");
    spinner.setAttribute("aria-hidden", "true");

    const text = document.createElement("div");
    text.className = "qr-overlay__text text-white fw-semibold";
    text.textContent = "Обработка данных...";

    overlay.append(spinner, text);

    document.body.appendChild(overlay);
    qrScanOverlay = overlay;
    return overlay;
}

function qrScanShowOverlay() {
    const overlay = qrScanEnsureOverlay();
    overlay.classList.remove("d-none");
    overlay.classList.add("d-flex");
    document.body.classList.add("overflow-hidden");
}

function qrScanHideOverlay() {
    if (!qrScanOverlay) {
        return;
    }

    qrScanOverlay.classList.add("d-none");
    qrScanOverlay.classList.remove("d-flex");
    document.body.classList.remove("overflow-hidden");
}

// ===================== Ручной ввод: валидация полей =====================

function initManualCheckValidation() {
    const root = requireElementById<HTMLElement>("manual-request-root");
    const btn = requireElementById<HTMLButtonElement>("manual-check-btn");
    const resultMessage = requireElementById<HTMLElement>("manual-result-message");

    const fnInput = requireInputById("manual-fn");
    const fdInput = requireInputById("manual-fd");
    const fpInput = requireInputById("manual-fp");
    const sumInput = requireInputById("manual-sum");
    const dateInput = requireInputById("manual-date");
    const typeSelect = requireSelectById("manual-type");

    qrScanRestoreManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
    qrScanSetDefaultManualDate(dateInput);
    qrScanBindManualDraftPersistence(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);

    btn.addEventListener("click", async function () {
        const isValid = validateManualFields(
            root,
            fnInput,
            fdInput,
            fpInput,
            sumInput,
            dateInput,
            typeSelect,
            resultMessage
        );

        if (!isValid) {
            return;
        }

        const fnDigits = (fnInput?.value || "").replace(/\D/g, "");
        const fdDigits = (fdInput?.value || "").replace(/\D/g, "");
        const fpDigits = (fpInput?.value || "").replace(/\D/g, "");
        const sumRaw = (sumInput?.value || "").trim();
        const dateValue = (dateInput?.value || "").trim();
        const typeValue = (typeSelect?.value || "").trim();

        // сбрасывает локальное сообщение
        if (resultMessage) {
            resultMessage.textContent = "";
            resultMessage.classList.remove("text-danger", "text-success");
        }

        qrScanLastSubmitSource = "manual";

        qrScanShowOverlay();
        try {
            const payload: ManualReceiptPayload = {
                receipt: {
                    fiscalDriveNumber: fnDigits,
                    fiscalDocumentNumber: fdDigits,
                    fiscalSign: fpDigits,
                    sum: parseFloat(sumRaw.replace(",", ".")),
                    dateTime: dateValue,
                    operationType: parseInt(typeValue, 10)
                },
                accountId: getSelectedAccountId()
            };

            const responseData = await submitManualReceiptAsync(payload, antiForgeryToken);
            if (responseData.isCreated) {
                qrScanClearManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
                qrScanRenderManualStatus("Чек добавлен в систему", "success");
                if (resultMessage) {
                    resultMessage.textContent = "Чек добавлен в систему";
                    resultMessage.classList.remove("text-danger");
                    resultMessage.classList.add("text-success");
                }
            } else if (responseData.isCreated === false) {
                // Ошибка создания: выводит текст из message, если он есть
                const msg =
                    (typeof responseData.message === "string" && responseData.message.trim().length > 0)
                        ? responseData.message
                        : "Указаны некорректные данные чека";

                const statusType = /уже\s+(есть|существ)/i.test(msg) ? "warning" : "error";
                qrScanRenderManualStatus(msg, statusType);
                if (resultMessage) {
                    resultMessage.textContent = msg;
                    resultMessage.classList.remove("text-success");
                    resultMessage.classList.add("text-danger");
                }
            } else {
                // Непредвиденный формат ответа
                qrScanRenderManualStatus("Указаны некорректные данные чека", "error");
                if (resultMessage) {
                    resultMessage.textContent = "Указаны некорректные данные чека";
                    resultMessage.classList.remove("text-success");
                    resultMessage.classList.add("text-danger");
                }
            }
        } catch (e) {
            console.error("QR scan: ошибка ручного запроса", e);
            const errorMessage = e instanceof Error ? e.message : "Указаны некорректные данные чека";
            qrScanRenderManualStatus(errorMessage, "error");
            if (resultMessage) {
                resultMessage.textContent = errorMessage;
                resultMessage.classList.remove("text-success");
                resultMessage.classList.add("text-danger");
            }
        } finally {
            qrScanHideOverlay();
        }
    });
}

function validateManualFields(root, fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect, resultMessage) {
    clearManualFieldError(root, fnInput);
    clearManualFieldError(root, fdInput);
    clearManualFieldError(root, fpInput);
    clearManualFieldError(root, sumInput);
    clearManualFieldError(root, dateInput);
    clearManualFieldError(root, typeSelect);

    if (resultMessage) {
        resultMessage.textContent = "";
        resultMessage.classList.remove("text-danger", "text-success");
    }

    let hasError = false;

    const fnDigits = (fnInput?.value || "").replace(/\D/g, "");
    const fdDigits = (fdInput?.value || "").replace(/\D/g, "");
    const fpDigits = (fpInput?.value || "").replace(/\D/g, "");
    const sumRaw = (sumInput?.value || "").trim();
    const dateValue = (dateInput?.value || "").trim();
    const typeValue = (typeSelect?.value || "").trim();

    // ФН: строго 16 цифр
    if (!fnDigits || fnDigits.length !== 16) {
        setManualFieldError(root, fnInput, "ФН должен содержать 16 цифр");
        hasError = true;
    }

    // ФД: обязательно, 1–10 цифр
    if (!fdDigits || fdDigits.length > 10) {
        setManualFieldError(root, fdInput, "ФД должен содержать от 4 до 10 цифр");
        hasError = true;
    }

    // ФПД: обязательно, 8–10 цифр
    if (!fpDigits || fpDigits.length < 8 || fpDigits.length > 10) {
        setManualFieldError(root, fpInput, "ФПД должен содержать от 8 до 10 цифр");
        hasError = true;
    }

    // Сумма: обязательно, число (0 допустим)
    if (sumRaw === "" || Number.isNaN(parseFloat(sumRaw))) {
        setManualFieldError(root, sumInput, "Укажите сумму");
        hasError = true;
    }

    // Дата: обязательно
    if (!dateValue) {
        setManualFieldError(root, dateInput, "Укажите дату и время");
        hasError = true;
    }

    // Тип чека: обязательно
    // если добавишь в select пустую опцию с value="" или "0", это тоже отловится
    if (!typeValue || typeValue === "0") {
        setManualFieldError(root, typeSelect, "Выберите тип чека");
        hasError = true;
    }

    if (hasError) {
        return false;
    }

    return true;
}

function qrScanRestoreManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect) {
    try {
        const raw = localStorage.getItem(MANUAL_RECEIPT_DRAFT_STORAGE_KEY);
        if (!raw) {
            return;
        }

        const draft = JSON.parse(raw);
        if (!draft || typeof draft !== "object") {
            return;
        }

        if (fnInput && typeof draft.fn === "string") {
            fnInput.value = draft.fn;
        }

        if (fdInput && typeof draft.fd === "string") {
            fdInput.value = draft.fd;
        }

        if (fpInput && typeof draft.fp === "string") {
            fpInput.value = draft.fp;
        }

        if (sumInput && typeof draft.sum === "string") {
            sumInput.value = draft.sum;
        }

        if (dateInput && typeof draft.date === "string") {
            dateInput.value = draft.date;
        }

        if (typeSelect && typeof draft.type === "string" && draft.type.length > 0) {
            typeSelect.value = draft.type;
        }
    } catch (error) {
        console.warn("QR scan: не удалось восстановить черновик ручного ввода", error);
    }
}

function qrScanSetDefaultManualDate(dateInput): void {
    if (!dateInput || dateInput.value) {
        return;
    }

    dateInput.value = formatLocalDateTimeInputValue(new Date());
}

function formatLocalDateTimeInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = (date.getMonth() + 1).toString().padStart(2, "0");
    const day = date.getDate().toString().padStart(2, "0");
    const hours = date.getHours().toString().padStart(2, "0");
    const minutes = date.getMinutes().toString().padStart(2, "0");

    return `${year}-${month}-${day}T${hours}:${minutes}`;
}

function qrScanBindManualDraftPersistence(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect) {
    const persist = function () {
        qrScanSaveManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
    };

    const inputs = [fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect];
    for (let i = 0; i < inputs.length; i++) {
        const element = inputs[i];
        if (!element) {
            continue;
        }

        element.addEventListener("input", persist);
        element.addEventListener("change", persist);
    }
}

function qrScanSaveManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect) {
    try {
        const draft = {
            fn: fnInput ? (fnInput.value || "") : "",
            fd: fdInput ? (fdInput.value || "") : "",
            fp: fpInput ? (fpInput.value || "") : "",
            sum: sumInput ? (sumInput.value || "") : "",
            date: dateInput ? (dateInput.value || "") : "",
            type: typeSelect ? (typeSelect.value || "") : ""
        };

        localStorage.setItem(MANUAL_RECEIPT_DRAFT_STORAGE_KEY, JSON.stringify(draft));
    } catch (error) {
        console.warn("QR scan: не удалось сохранить черновик ручного ввода", error);
    }
}

function qrScanClearManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect) {
    try {
        localStorage.removeItem(MANUAL_RECEIPT_DRAFT_STORAGE_KEY);
    } catch (error) {
        console.warn("QR scan: не удалось очистить черновик ручного ввода", error);
    }

    if (fnInput) {
        fnInput.value = "";
    }

    if (fdInput) {
        fdInput.value = "";
    }

    if (fpInput) {
        fpInput.value = "";
    }

    if (sumInput) {
        sumInput.value = "";
    }

    if (dateInput) {
        dateInput.value = "";
    }

    if (typeSelect) {
        typeSelect.value = "1";
    }
}

function setManualFieldError(root, input, message) {
    if (!root || !input) {
        return;
    }

    const row = input.closest(".manual-row");
    if (!row) {
        return;
    }

    const label = row.querySelector("label");
    if (label) {
        label.classList.add("text-danger");
    }

    input.classList.add("is-invalid");

    let errorSpan = row.querySelector(".manual-field-error");
    if (!errorSpan) {
        errorSpan = document.createElement("div");
        errorSpan.className = "manual-field-error invalid-feedback d-block";
        row.appendChild(errorSpan);
    }

    errorSpan.textContent = message;
}

function clearManualFieldError(root, input) {
    if (!root || !input) {
        return;
    }

    const row = input.closest(".manual-row");
    if (!row) {
        return;
    }

    const label = row.querySelector("label");
    if (label) {
        label.classList.remove("text-danger");
    }

    input.classList.remove("is-invalid");

    const errorSpan = row.querySelector(".manual-field-error");
    if (errorSpan) {
        errorSpan.textContent = "";
    }
}

// ===================== Доп. функции ===================== 
function isMobileDevice() {
    try {
        const nav = typeof navigator !== "undefined" ? navigator : null;
        const win = typeof window !== "undefined" ? window : null;

        if (nav) {
            if (typeof nav.maxTouchPoints === "number" && nav.maxTouchPoints > 1) {
                return true;
            }

            const ua = nav.userAgent || "";
            if (/Android|iPhone|iPad|iPod/i.test(ua)) {
                return true;
            }
        }

        if (win && win.matchMedia) {
            const mq = win.matchMedia("(pointer: coarse)");
            if (mq && mq.matches) {
                return true;
            }
        }
    } catch (e) {
        // Игнорировать ошибки окружения
    }

    return false;
}

function qrScanPauseCameraForFeedback() {
    if (!qrScanHtml5QrcodeScanner || qrScanCameraPaused) {
        return;
    }

    try {
        qrScanHtml5QrcodeScanner.pause(true);
        qrScanCameraPaused = true;
    } catch (error) {
        console.warn("QR scan: не удалось поставить камеру на паузу", error);
    }
}

function qrScanResumeCameraAfterFeedback() {
    if (!qrScanHtml5QrcodeScanner || !qrScanCameraPaused) {
        return;
    }

    try {
        qrScanHtml5QrcodeScanner.resume();
        qrScanCameraPaused = false;
    } catch (error) {
        console.warn("QR scan: не удалось возобновить камеру", error);
    }
}

function qrScanShowCameraReceiptFeedback(results, addedToDbCount, errorCount, errorMessage, source) {
    const receiptResult = Array.isArray(results) && results.length > 0 ? results[0] : null;
    const modal = qrScanEnsureCameraModal();
    const title = modal.querySelector(".qr-camera-modal__title");
    const subtitle = modal.querySelector(".qr-camera-modal__subtitle");
    const details = modal.querySelector(".qr-camera-modal__details");

    if (!title || !subtitle || !details) {
        return;
    }

    clearElement(details);
    title.classList.remove("text-success", "text-warning", "text-danger");

    if (!receiptResult) {
        title.textContent = "Сканирование завершено";
        title.classList.add("text-warning");
        subtitle.textContent = source === "files"
            ? "Файл обработан, но результат не найден."
            : "Данные чека получены.";
        qrScanAppendCameraModalDetail(details, "Статус", "Результат не найден");
    } else if (receiptResult.errorMessage) {
        title.textContent = source === "files" ? "Файл обработан с ошибкой" : "Чек считан с ошибкой";
        title.classList.add("text-danger");
        subtitle.textContent = receiptResult.errorMessage;
        qrScanAppendParsedReceiptDetails(details, receiptResult.parsed);
    } else {
        title.textContent = addedToDbCount > 0
            ? qrScanBuildAddedReceiptStatusText(receiptResult.parsed)
            : "Чек уже есть в системе";
        title.classList.add(addedToDbCount > 0 ? "text-success" : "text-warning");
        subtitle.textContent = addedToDbCount > 0
            ? (source === "files"
                ? "Файл успешно распознан. Проверьте данные чека."
                : "")
            : "Такой чек уже был добавлен ранее.";
        qrScanAppendParsedReceiptDetails(details, receiptResult.parsed);
    }

    if (errorCount > 0 && errorMessage) {
        qrScanAppendCameraModalDetail(details, "Ошибка", errorMessage);
    }

    qrScanShowCameraModal();
}

function qrScanEnsureCameraModal() {
    if (qrScanCameraModal) {
        return qrScanCameraModal;
    }

    if (!window.bootstrap || !window.bootstrap.Modal) {
        throw new Error("Bootstrap Modal недоступен.");
    }

    const modal = document.createElement("div");
    modal.className = "qr-camera-modal modal fade";
    modal.tabIndex = -1;
    modal.setAttribute("aria-hidden", "true");

    const dialog = document.createElement("div");
    dialog.className = "modal-dialog modal-dialog-centered modal-dialog-scrollable";

    const content = document.createElement("div");
    content.className = "modal-content border-0 shadow";

    const header = document.createElement("div");
    header.className = "modal-header";

    const headingContainer = document.createElement("div");

    const title = document.createElement("div");
    title.id = "qr-camera-modal-title";
    title.className = "qr-camera-modal__title h4 mb-1";

    const subtitle = document.createElement("div");
    subtitle.className = "qr-camera-modal__subtitle text-body-secondary";

    headingContainer.append(title, subtitle);

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "btn-close";
    closeButton.dataset.bsDismiss = "modal";
    closeButton.setAttribute("aria-label", "Закрыть");

    header.append(headingContainer, closeButton);

    const body = document.createElement("div");
    body.className = "modal-body";

    const details = document.createElement("div");
    details.className = "qr-camera-modal__details d-grid gap-2";
    body.appendChild(details);

    const footer = document.createElement("div");
    footer.className = "modal-footer";

    const continueButton = document.createElement("button");
    continueButton.type = "button";
    continueButton.className = "btn btn-primary";
    continueButton.dataset.bsDismiss = "modal";
    continueButton.textContent = "Продолжить сканирование";
    footer.appendChild(continueButton);

    content.append(header, body, footer);
    dialog.appendChild(content);
    modal.appendChild(dialog);

    modal.addEventListener("hidden.bs.modal", function () {
        qrScanResumeCameraAfterFeedback();
    });

    document.body.appendChild(modal);
    qrScanCameraModal = modal;
    qrScanCameraModalInstance = window.bootstrap.Modal.getOrCreateInstance(modal);
    return modal;
}

function qrScanShowCameraModal() {
    qrScanEnsureCameraModal();

    if (!qrScanCameraModalInstance) {
        throw new Error("Не удалось инициализировать модальное окно результата сканирования.");
    }

    if (qrScanOverlay) {
        qrScanHideOverlay();
    }

    qrScanCameraModalInstance.show();
}

function qrScanHideCameraReceiptFeedback() {
    if (!qrScanCameraModal || !qrScanCameraModalInstance) {
        qrScanResumeCameraAfterFeedback();
        return;
    }

    qrScanCameraModalInstance.hide();
}

function qrScanAppendParsedReceiptDetails(container, parsed) {
    if (!parsed) {
        qrScanAppendCameraModalDetail(container, "Данные", "Не удалось разобрать реквизиты чека");
        return;
    }

    qrScanAppendCameraModalDetail(container, "Сумма", qrScanFormatReceiptSum(parsed.sum));
    qrScanAppendCameraModalDetail(container, "Дата", qrScanFormatReceiptDate(parsed.dateTime));
    qrScanAppendCameraModalDetail(container, "ФН", parsed.fiscalDriveNumber || "Не указан");
    qrScanAppendCameraModalDetail(container, "ФД", parsed.fiscalDocumentNumber || "Не указан");
    qrScanAppendCameraModalDetail(container, "ФП", parsed.fiscalSign || "Не указан");
    qrScanAppendCameraModalDetail(container, "Тип", qrScanFormatOperationType(parsed.operationType));
}

function qrScanAppendCameraModalDetail(container, label, value) {
    const item = document.createElement("div");
    item.className = "qr-camera-modal__detail border rounded-3 px-3 py-2 bg-body-tertiary";

    const labelElement = document.createElement("div");
    labelElement.className = "qr-camera-modal__detail-label small text-uppercase text-body-secondary fw-semibold";
    labelElement.textContent = label;

    const valueElement = document.createElement("div");
    valueElement.className = "qr-camera-modal__detail-value fw-semibold";
    valueElement.textContent = value;

    item.appendChild(labelElement);
    item.appendChild(valueElement);
    container.appendChild(item);
}

function qrScanFormatReceiptSum(value) {
    const numericValue = typeof value === "number" ? value : parseFloat(value);
    if (Number.isNaN(numericValue)) {
        return "Не указана";
    }

    return new Intl.NumberFormat("ru-RU", {
        style: "currency",
        currency: "RUB",
        minimumFractionDigits: 2
    }).format(numericValue);
}

function qrScanFormatReceiptDate(value) {
    if (!value) {
        return "Не указана";
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return String(value);
    }

    return date.toLocaleString("ru-RU");
}

function qrScanFormatOperationType(value) {
    switch (value) {
        case 1:
        case "1":
            return "Приход";
        case 2:
        case "2":
            return "Возврат прихода";
        case 3:
        case "3":
            return "Расход";
        case 4:
        case "4":
            return "Возврат расхода";
        default:
            return "Не указан";
    }
}

async function initQrScanMobileScannerUiAsync(qrReaderElement) {
    if (!qrReaderElement) {
        return;
    }

    if (qrReaderElement.dataset.mobileUiInitialized === "1" && qrScanHtml5QrcodeScanner) {
        return;
    }

    qrReaderElement.dataset.mobileUiInitialized = "1";
    qrReaderElement.className = "qr-reader-shell card shadow-lg border overflow-hidden";
    qrReaderElement.style.borderRadius = "2rem";
    qrReaderElement.style.borderColor = "#d9e2ec";
    qrReaderElement.style.backgroundColor = "#ffffff";
    qrReaderElement.style.boxShadow = "0 24px 48px rgba(15, 23, 42, 0.10)";
    qrReaderElement.replaceChildren();

    const panel = document.createElement("div");
    panel.className = "qr-reader-panel";

    const scanRegion = document.createElement("div");
    scanRegion.className = "qr-reader-panel__scan-region card-body";
    scanRegion.style.backgroundColor = "#ffffff";
    scanRegion.style.padding = "1.5rem 1.5rem 1rem 1.5rem";

    const viewport = document.createElement("div");
    viewport.id = "qr-reader-viewport";
    viewport.className = "qr-reader-panel__viewport rounded-4 overflow-hidden border-0";
    viewport.style.minHeight = "340px";
    viewport.style.position = "relative";
    viewport.style.backgroundColor = "#dbeafe";
    viewport.style.borderRadius = "1.75rem";
    scanRegion.appendChild(viewport);

    const hint = document.createElement("div");
    hint.className = "qr-reader-panel__hint text-center mt-3";
    hint.style.color = "#4b5563";
    hint.style.fontSize = "1rem";
    hint.textContent = "Наведите камеру на QR-код чека";
    scanRegion.appendChild(hint);

    const dashboard = document.createElement("div");
    dashboard.className = "qr-reader-panel__dashboard card-body pt-0";
    dashboard.style.padding = "0 1.5rem 1.5rem 1.5rem";

    const header = document.createElement("div");
    header.className = "qr-reader-panel__header d-flex flex-column gap-1";

    const title = document.createElement("div");
    title.className = "qr-reader-panel__title mb-0";
    title.style.fontSize = "1rem";
    title.style.fontWeight = "700";
    title.style.lineHeight = "1.1";
    title.style.color = "#111827";
    title.textContent = "Сканирование чека";
    header.appendChild(title);

    const status = document.createElement("div");
    status.className = "qr-reader-panel__status";
    status.style.fontSize = "1rem";
    status.style.color = "#6b7280";
    status.style.minHeight = "1.75rem";
    status.textContent = "Запрашиваем доступ к камере...";
    header.appendChild(status);

    dashboard.appendChild(header);

    const controls = document.createElement("div");
    controls.className = "qr-reader-panel__controls d-grid gap-3 mt-3";

    const cameraGroup = document.createElement("div");
    cameraGroup.className = "qr-reader-panel__camera-group d-grid gap-2";

    const cameraLabel = document.createElement("label");
    cameraLabel.className = "qr-reader-panel__label form-label fw-semibold mb-0";
    cameraLabel.style.fontSize = "1rem";
    cameraLabel.style.color = "#374151";
    cameraLabel.htmlFor = "qr-reader-camera-select";
    cameraLabel.textContent = "Камера";
    cameraGroup.appendChild(cameraLabel);

    const cameraSelect = document.createElement("select");
    cameraSelect.id = "qr-reader-camera-select";
    cameraSelect.name = "qr-reader-camera-select";
    cameraSelect.className = "qr-reader-panel__select form-select";
    cameraSelect.disabled = true;
    cameraSelect.style.minHeight = "2.85rem";
    cameraSelect.style.borderRadius = "1rem";
    cameraSelect.style.fontSize = "0.95rem";
    cameraSelect.style.paddingLeft = "1rem";
    cameraSelect.style.borderColor = "#d1d5db";
    cameraSelect.style.boxShadow = "0 4px 16px rgba(15, 23, 42, 0.06)";
    cameraSelect.addEventListener("change", function () {
        localStorage.setItem(CAMERA_STORAGE_KEY, cameraSelect.value);
        qrScanStartCameraByIdAsync(cameraSelect.value).catch(function (error) {
            qrScanUpdateCameraStatus(error && error.message ? error.message : "Не удалось переключить камеру.", true);
        });
    });
    cameraGroup.appendChild(cameraSelect);

    controls.appendChild(cameraGroup);

    const buttonGroup = document.createElement("div");
    buttonGroup.className = "qr-reader-panel__button-group d-grid gap-2";

    const startButton = document.createElement("button");
    startButton.type = "button";
    startButton.className = "qr-reader-panel__button btn btn-primary";
    startButton.style.minHeight = "2.75rem";
    startButton.style.borderRadius = "1rem";
    startButton.style.background = "linear-gradient(135deg, #0f766e 0%, #0f5d78 100%)";
    startButton.style.border = "0";
    startButton.style.fontSize = "0.95rem";
    startButton.style.fontWeight = "700";
    startButton.style.boxShadow = "0 14px 28px rgba(15, 118, 110, 0.24)";
    startButton.textContent = "Сканировать камерой";
    startButton.addEventListener("click", function () {
        const cameraId = cameraSelect.value || qrScanGetPreferredCameraId(qrScanAvailableCameras);
        qrScanStartCameraByIdAsync(cameraId).catch(function (error) {
            qrScanUpdateCameraStatus(error && error.message ? error.message : "Не удалось запустить камеру.", true);
        });
    });
    buttonGroup.appendChild(startButton);

    const stopButton = document.createElement("button");
    stopButton.type = "button";
    stopButton.className = "qr-reader-panel__button btn btn-outline-secondary";
    stopButton.style.minHeight = "2.75rem";
    stopButton.style.borderRadius = "1rem";
    stopButton.style.fontSize = "0.95rem";
    stopButton.style.fontWeight = "600";
    stopButton.style.border = "0";
    stopButton.style.backgroundColor = "#eef2ff";
    stopButton.style.color = "#6b7280";
    stopButton.textContent = "Остановить";
    stopButton.addEventListener("click", function () {
        qrScanStopCameraAsync().catch(function (error) {
            qrScanUpdateCameraStatus(error && error.message ? error.message : "Не удалось остановить камеру.", true);
        });
    });
    buttonGroup.appendChild(stopButton);

    const galleryButton = document.createElement("button");
    galleryButton.type = "button";
    galleryButton.className = "qr-reader-panel__button btn btn-outline-primary";
    galleryButton.style.minHeight = "2.75rem";
    galleryButton.style.borderRadius = "1rem";
    galleryButton.style.fontSize = "0.95rem";
    galleryButton.style.fontWeight = "700";
    galleryButton.style.borderColor = "#d1d5db";
    galleryButton.style.backgroundColor = "#ffffff";
    galleryButton.style.color = "#0f5d78";
    galleryButton.style.boxShadow = "0 10px 24px rgba(15, 23, 42, 0.06)";
    galleryButton.textContent = "Выбрать из галереи";
    galleryButton.addEventListener("click", function () {
        if (qrScanFileInput) {
            qrScanFileInput.click();
        }
    });
    buttonGroup.appendChild(galleryButton);

    controls.appendChild(buttonGroup);
    dashboard.appendChild(controls);
    panel.appendChild(scanRegion);
    panel.appendChild(dashboard);
    qrReaderElement.appendChild(panel);

    qrReaderElement.dataset.cameraStatusElementId = "";
    qrReaderElement.dataset.cameraStartButtonId = "";
    qrReaderElement.dataset.cameraStopButtonId = "";

    status.id = "qr-reader-camera-status";
    startButton.id = "qr-reader-camera-start";
    stopButton.id = "qr-reader-camera-stop";
    qrReaderElement.dataset.cameraStatusElementId = status.id;
    qrReaderElement.dataset.cameraStartButtonId = startButton.id;
    qrReaderElement.dataset.cameraStopButtonId = stopButton.id;
    qrScanSetCameraButtonsState(false);

    qrScanHtml5QrcodeScanner = new Html5Qrcode("qr-reader-viewport");

    try {
        qrScanAvailableCameras = await qrScanLoadAvailableCamerasAsync();
        qrScanRenderCameraOptions(cameraSelect, qrScanAvailableCameras);

        const preferredCameraId = qrScanGetPreferredCameraId(qrScanAvailableCameras);
        if (preferredCameraId) {
            cameraSelect.value = preferredCameraId;
            await qrScanStartCameraByIdAsync(preferredCameraId);
            return;
        }

        qrScanUpdateCameraStatus("Камера не найдена.", true);
    } catch (error) {
        qrScanUpdateCameraStatus(error && error.message ? error.message : "Не удалось получить доступ к камере.", true);
    }
}

async function qrScanLoadAvailableCamerasAsync() {
    const cameras = await Html5Qrcode.getCameras();
    if (!Array.isArray(cameras) || cameras.length === 0) {
        return [];
    }

    const normalizedCameras = cameras.map(function (camera, index) {
        return {
            id: camera.id,
            label: qrScanNormalizeCameraLabel(camera.label, index + 1)
        };
    });

    const backCameras = normalizedCameras.filter(function (camera) {
        return qrScanIsBackCamera(camera.label);
    });

    const camerasForList = backCameras.length > 0
        ? backCameras.filter(function (camera) { return !qrScanIsFrontCamera(camera.label); })
        : normalizedCameras;

    return camerasForList.sort(function (left, right) {
        return qrScanGetCameraPriority(right.label) - qrScanGetCameraPriority(left.label);
    });
}

function qrScanRenderCameraOptions(select, cameras) {
    if (!select) {
        return;
    }

    select.replaceChildren();

    const availableCameras = Array.isArray(cameras) ? cameras : [];
    for (let i = 0; i < availableCameras.length; i++) {
        const camera = availableCameras[i];
        const option = document.createElement("option");
        option.value = camera.id;
        option.textContent = camera.label;
        select.appendChild(option);
    }

    select.disabled = availableCameras.length === 0;
}

function qrScanNormalizeCameraLabel(label, fallbackIndex) {
    const value = (label || "").trim();
    if (!value) {
        return `Камера ${fallbackIndex}`;
    }

    if (/anonymous camera/i.test(value)) {
        return `Камера ${fallbackIndex}`;
    }

    return value
        .replace(/camera/gi, "Камера")
        .replace(/back/gi, "задняя")
        .replace(/rear/gi, "задняя")
        .replace(/front/gi, "фронтальная")
        .replace(/facing/gi, "")
        .replace(/\s+/g, " ")
        .trim();
}

function qrScanIsFrontCamera(label) {
    return /front|user|selfie|frontal|фронт|перед/i.test(label || "");
}

function qrScanIsBackCamera(label) {
    return /back|rear|environment|world|задн|тыл/i.test(label || "");
}

function qrScanGetCameraPriority(label) {
    const value = (label || "").toLowerCase();
    let priority = 0;

    if (qrScanIsBackCamera(value)) {
        priority += 200;
    }

    if (/main|wide|1x|standard|default|основ/i.test(value)) {
        priority += 80;
    }

    if (/ultra|macro|tele|zoom|depth|0\.5x|2x/i.test(value)) {
        priority -= 40;
    }

    if (qrScanIsFrontCamera(value)) {
        priority -= 200;
    }

    return priority;
}

function qrScanGetPreferredCameraId(cameras) {
    if (!Array.isArray(cameras) || cameras.length === 0) {
        return null;
    }

    const savedCameraId = localStorage.getItem(CAMERA_STORAGE_KEY);
    if (savedCameraId && cameras.some(function (camera) { return camera.id === savedCameraId; })) {
        return savedCameraId;
    }

    return cameras[0].id;
}

function qrScanGetMobileCameraElements() {
    const qrReaderElement = document.getElementById("qr-reader");
    if (!qrReaderElement) {
        return {
            statusElement: null,
            startButton: null,
            stopButton: null
        };
    }

    const statusElementId = qrReaderElement.dataset.cameraStatusElementId || "";
    const startButtonId = qrReaderElement.dataset.cameraStartButtonId || "";
    const stopButtonId = qrReaderElement.dataset.cameraStopButtonId || "";

    return {
        statusElement: statusElementId ? document.getElementById(statusElementId) : null,
        startButton: startButtonId ? document.getElementById(startButtonId) as HTMLButtonElement | null : null,
        stopButton: stopButtonId ? document.getElementById(stopButtonId) as HTMLButtonElement | null : null
    };
}

function qrScanUpdateCameraStatus(message, isError) {
    const elements = qrScanGetMobileCameraElements();
    if (!elements.statusElement) {
        return;
    }

    elements.statusElement.textContent = message;
    elements.statusElement.classList.toggle("text-danger", !!isError);
    elements.statusElement.classList.toggle("text-body-secondary", !isError);
    elements.statusElement.style.color = isError ? "#b91c1c" : "#6b7280";
}

function qrScanSetCameraButtonsState(isRunning) {
    const elements = qrScanGetMobileCameraElements();
    setCameraButtonState(elements.startButton, !!isRunning);
    setCameraButtonState(elements.stopButton, !isRunning);
}

function qrScanBuildCameraConfig() {
    return {
        fps: 10,
        aspectRatio: 1.3333333,
        qrbox: function (viewfinderWidth, viewfinderHeight) {
            const minEdge = Math.min(viewfinderWidth, viewfinderHeight);
            const boxSize = Math.max(180, Math.min(280, Math.floor(minEdge * 0.72)));
            return {
                width: boxSize,
                height: boxSize
            };
        }
    };
}

async function qrScanStartCameraByIdAsync(cameraId) {
    if (!qrScanHtml5QrcodeScanner) {
        throw new Error("Сканер камеры не инициализирован.");
    }

    const targetCameraId = cameraId || qrScanGetPreferredCameraId(qrScanAvailableCameras);
    if (!targetCameraId) {
        throw new Error("Не найдена подходящая камера.");
    }

    qrScanUpdateCameraStatus("Открываем камеру...", false);

    const scannerState = typeof qrScanHtml5QrcodeScanner.getState === "function"
        ? qrScanHtml5QrcodeScanner.getState()
        : Html5QrcodeScannerState.NOT_STARTED;

    if (scannerState !== Html5QrcodeScannerState.NOT_STARTED) {
        await qrScanStopCameraAsync();
    }

    await qrScanHtml5QrcodeScanner.start(
        targetCameraId,
        qrScanBuildCameraConfig(),
        qrScanOnScanSuccess,
        qrScanOnScanError
    );
    qrScanApplyCameraViewportStyles();

    qrScanCurrentCameraId = targetCameraId;
    qrScanCameraPaused = false;
    localStorage.setItem(CAMERA_STORAGE_KEY, targetCameraId);
    qrScanSetCameraButtonsState(true);
    qrScanUpdateCameraStatus("Сканирование активно", false);
}

function qrScanApplyCameraViewportStyles() {
    const viewport = document.getElementById("qr-reader-viewport");
    if (!viewport) {
        return;
    }

    const video = viewport.querySelector("video");
    if (video) {
        video.style.width = "100%";
        video.style.height = "100%";
        video.style.objectFit = "cover";
    }

    const section = viewport.querySelector("section");
    if (section) {
        section.style.borderRadius = "1rem";
        section.style.overflow = "hidden";
    }
}

async function qrScanStopCameraAsync() {
    if (!qrScanHtml5QrcodeScanner || typeof qrScanHtml5QrcodeScanner.getState !== "function") {
        return;
    }

    const scannerState = qrScanHtml5QrcodeScanner.getState();
    if (scannerState === Html5QrcodeScannerState.NOT_STARTED) {
        qrScanCameraPaused = false;
        qrScanSetCameraButtonsState(false);
        qrScanUpdateCameraStatus("Камера остановлена", false);
        return;
    }

    if (qrScanCameraPaused) {
        try {
            qrScanHtml5QrcodeScanner.resume();
            qrScanCameraPaused = false;
        } catch (error) {
            console.warn("QR scan: не удалось снять паузу перед остановкой камеры", error);
        }
    }

    await qrScanHtml5QrcodeScanner.stop();
    qrScanCameraPaused = false;
    qrScanCurrentCameraId = null;
    qrScanSetCameraButtonsState(false);
    qrScanUpdateCameraStatus("Камера остановлена", false);
}

// получить дату фото из EXIF (DateTimeOriginal / Digitized / DateTime)
// вернуть ISO-строку или null
async function qrScanGetPhotoDateFromFile(file) {
    return new Promise(function (resolve) {
        if (!window.EXIF || !file) {
            resolve(null);
            return;
        }

        try {
            EXIF.getData(file, function () {
                try {
                    // this — внутренний объект exif-js, связанный с file
                    const raw =
                        EXIF.getTag(this, "DateTimeOriginal") ||
                        EXIF.getTag(this, "DateTimeDigitized") ||
                        EXIF.getTag(this, "DateTime");

                    if (!raw || typeof raw !== "string") {
                        resolve(null);
                        return;
                    }

                    // ожидаемый формат: "2024:11:27 18:03:15"
                    const parts = raw.split(" ");
                    if (parts.length !== 2) {
                        resolve(null);
                        return;
                    }

                    const datePart = parts[0].split(":");
                    const timePart = parts[1].split(":");
                    if (datePart.length !== 3 || timePart.length < 2) {
                        resolve(null);
                        return;
                    }

                    const year = parseInt(datePart[0], 10);
                    const month = parseInt(datePart[1], 10);
                    const day = parseInt(datePart[2], 10);
                    const hour = parseInt(timePart[0], 10);
                    const minute = parseInt(timePart[1], 10);
                    const second = timePart.length > 2 ? parseInt(timePart[2], 10) : 0;

                    if (Number.isNaN(year) || Number.isNaN(month) || Number.isNaN(day)) {
                        resolve(null);
                        return;
                    }

                    const jsDate = new Date(year, month - 1, day, hour, minute, second);

                    resolve(jsDate.toISOString());
                } catch (err) {
                    console.warn("EXIF parse error", err);
                    resolve(null);
                }
            });
        } catch (err) {
            console.warn("EXIF reader error", err);
            resolve(null);
        }
    });
}

function getImageSizeFromFile(file) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        const url = URL.createObjectURL(file);

        img.onload = function () {
            const width = img.naturalWidth;
            const height = img.naturalHeight;
            URL.revokeObjectURL(url);
            resolve({ width, height });
        };

        img.onerror = function (e) {
            URL.revokeObjectURL(url);
            reject(e);
        };

        img.src = url;
    });
}

async function qrScanDecodeFile(file) {
    try {
        return await qrScanFileScanner.scanFileV2(file, false);
    } catch (primaryError) {
        const fallbackResult = await qrScanDecodeFileWithJsQr(file);
        if (fallbackResult) {
            return fallbackResult;
        }

        throw primaryError;
    }
}

async function qrScanDecodeFileWithJsQr(file) {
    if (typeof jsQR !== "function") {
        return null;
    }

    const variants = await qrScanBuildJsQrVariants(file);
    for (let i = 0; i < variants.length; i++) {
        const canvas = variants[i];
        const context = canvas.getContext("2d", { willReadFrequently: true });
        if (!context) {
            continue;
        }

        const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
        const decoded = jsQR(imageData.data, canvas.width, canvas.height, {
            inversionAttempts: "attemptBoth"
        });

        if (decoded && typeof decoded.data === "string" && decoded.data.length > 0) {
            return {
                decodedText: decoded.data
            };
        }
    }

    return null;
}

async function qrScanBuildJsQrVariants(file) {
    const image = await qrScanLoadImageFromFile(file);
    const sourceCanvas = qrScanCreateCanvasFromImage(image);
    const sourceCrop = qrScanDetectQrCropArea(sourceCanvas);
    const croppedCanvas = qrScanCropCanvas(sourceCanvas, sourceCrop);
    const variants = [
        sourceCanvas,
        qrScanScaleCanvas(sourceCanvas, 2),
        croppedCanvas,
        qrScanScaleCanvas(croppedCanvas, 2),
        qrScanCreateThresholdCanvas(croppedCanvas, 180),
        qrScanCreateThresholdCanvas(croppedCanvas, 210),
        qrScanCreateThresholdCanvas(qrScanScaleCanvas(croppedCanvas, 2), 200)
    ];

    return variants.filter(function (canvas) {
        return canvas && canvas.width > 0 && canvas.height > 0;
    });
}

function qrScanLoadImageFromFile(file) {
    return new Promise((resolve, reject) => {
        const image = new Image();
        const objectUrl = URL.createObjectURL(file);

        image.onload = function () {
            URL.revokeObjectURL(objectUrl);
            resolve(image);
        };

        image.onerror = function (event) {
            URL.revokeObjectURL(objectUrl);
            reject(event);
        };

        image.src = objectUrl;
    });
}

function qrScanCreateCanvasFromImage(image) {
    const canvas = document.createElement("canvas");
    canvas.width = image.naturalWidth || image.width;
    canvas.height = image.naturalHeight || image.height;

    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для подготовки изображения");
    }

    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return canvas;
}

function qrScanScaleCanvas(sourceCanvas, multiplier) {
    const safeMultiplier = multiplier > 0 ? multiplier : 1;
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(sourceCanvas.width * safeMultiplier));
    canvas.height = Math.max(1, Math.round(sourceCanvas.height * safeMultiplier));

    const context = canvas.getContext("2d");
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для масштабирования");
    }

    context.imageSmoothingEnabled = false;
    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(sourceCanvas, 0, 0, canvas.width, canvas.height);
    return canvas;
}

function qrScanCreateThresholdCanvas(sourceCanvas, threshold) {
    const canvas = document.createElement("canvas");
    canvas.width = sourceCanvas.width;
    canvas.height = sourceCanvas.height;

    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для бинаризации");
    }

    context.drawImage(sourceCanvas, 0, 0);
    const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
    const pixels = imageData.data;

    for (let i = 0; i < pixels.length; i += 4) {
        const luminance = (0.299 * pixels[i]) + (0.587 * pixels[i + 1]) + (0.114 * pixels[i + 2]);
        const value = luminance < threshold ? 0 : 255;
        pixels[i] = value;
        pixels[i + 1] = value;
        pixels[i + 2] = value;
        pixels[i + 3] = 255;
    }

    context.putImageData(imageData, 0, 0);
    return canvas;
}

function qrScanDetectQrCropArea(sourceCanvas) {
    const ANALYSIS_MAX_SIDE = 512;
    const DARK_THRESHOLD = 210;
    const MIN_COMPONENT_AREA = 16;
    const analysisScale = Math.min(1, ANALYSIS_MAX_SIDE / Math.max(sourceCanvas.width, sourceCanvas.height));
    const analysisWidth = Math.max(1, Math.round(sourceCanvas.width * analysisScale));
    const analysisHeight = Math.max(1, Math.round(sourceCanvas.height * analysisScale));
    const analysisCanvas = document.createElement("canvas");
    analysisCanvas.width = analysisWidth;
    analysisCanvas.height = analysisHeight;

    const analysisContext = analysisCanvas.getContext("2d", { willReadFrequently: true });
    if (!analysisContext) {
        return {
            x: 0,
            y: 0,
            width: sourceCanvas.width,
            height: sourceCanvas.height
        };
    }

    analysisContext.fillStyle = "#ffffff";
    analysisContext.fillRect(0, 0, analysisWidth, analysisHeight);
    analysisContext.drawImage(sourceCanvas, 0, 0, analysisWidth, analysisHeight);

    const imageData = analysisContext.getImageData(0, 0, analysisWidth, analysisHeight);
    const pixels = imageData.data;
    const mask = new Uint8Array(analysisWidth * analysisHeight);

    for (let y = 0; y < analysisHeight; y++) {
        for (let x = 0; x < analysisWidth; x++) {
            const pixelIndex = ((y * analysisWidth) + x) * 4;
            const luminance = (0.299 * pixels[pixelIndex]) + (0.587 * pixels[pixelIndex + 1]) + (0.114 * pixels[pixelIndex + 2]);
            if (luminance < DARK_THRESHOLD) {
                mask[(y * analysisWidth) + x] = 1;
            }
        }
    }

    const visited = new Uint8Array(mask.length);
    const queue = new Int32Array(mask.length);
    const centerX = analysisWidth / 2;
    const centerY = analysisHeight / 2;
    let bestComponent = null;

    for (let startIndex = 0; startIndex < mask.length; startIndex++) {
        if (mask[startIndex] === 0 || visited[startIndex] === 1) {
            continue;
        }

        let queueStart = 0;
        let queueEnd = 0;
        let area = 0;
        let minX = analysisWidth;
        let minY = analysisHeight;
        let maxX = 0;
        let maxY = 0;

        queue[queueEnd++] = startIndex;
        visited[startIndex] = 1;

        while (queueStart < queueEnd) {
            const current = queue[queueStart++];
            const currentX = current % analysisWidth;
            const currentY = Math.floor(current / analysisWidth);
            area++;

            if (currentX < minX) {
                minX = currentX;
            }

            if (currentY < minY) {
                minY = currentY;
            }

            if (currentX > maxX) {
                maxX = currentX;
            }

            if (currentY > maxY) {
                maxY = currentY;
            }

            if (currentX > 0) {
                const leftIndex = current - 1;
                if (mask[leftIndex] === 1 && visited[leftIndex] === 0) {
                    visited[leftIndex] = 1;
                    queue[queueEnd++] = leftIndex;
                }
            }

            if (currentX + 1 < analysisWidth) {
                const rightIndex = current + 1;
                if (mask[rightIndex] === 1 && visited[rightIndex] === 0) {
                    visited[rightIndex] = 1;
                    queue[queueEnd++] = rightIndex;
                }
            }

            if (currentY > 0) {
                const topIndex = current - analysisWidth;
                if (mask[topIndex] === 1 && visited[topIndex] === 0) {
                    visited[topIndex] = 1;
                    queue[queueEnd++] = topIndex;
                }
            }

            if (currentY + 1 < analysisHeight) {
                const bottomIndex = current + analysisWidth;
                if (mask[bottomIndex] === 1 && visited[bottomIndex] === 0) {
                    visited[bottomIndex] = 1;
                    queue[queueEnd++] = bottomIndex;
                }
            }
        }

        if (area < MIN_COMPONENT_AREA) {
            continue;
        }

        const width = maxX - minX + 1;
        const height = maxY - minY + 1;
        const maxSide = Math.max(width, height);
        const minSide = Math.max(1, Math.min(width, height));
        const aspectPenalty = maxSide / minSide;
        const fillRatio = area / (width * height);
        const componentCenterX = minX + (width / 2);
        const componentCenterY = minY + (height / 2);
        const distanceX = (componentCenterX - centerX) / analysisWidth;
        const distanceY = (componentCenterY - centerY) / analysisHeight;
        const centerPenalty = Math.sqrt((distanceX * distanceX) + (distanceY * distanceY));
        const score = area * (1 / aspectPenalty) * Math.max(fillRatio, 0.15) * (1 / (1 + (centerPenalty * 3)));

        if (!bestComponent || score > bestComponent.score) {
            bestComponent = {
                minX,
                minY,
                width,
                height,
                score
            };
        }
    }

    if (!bestComponent) {
        return {
            x: 0,
            y: 0,
            width: sourceCanvas.width,
            height: sourceCanvas.height
        };
    }

    const sourceX = bestComponent.minX / analysisScale;
    const sourceY = bestComponent.minY / analysisScale;
    const sourceWidth = bestComponent.width / analysisScale;
    const sourceHeight = bestComponent.height / analysisScale;
    const padding = Math.max(sourceWidth, sourceHeight) * 0.35;
    const cropX = Math.max(0, Math.floor(sourceX - padding));
    const cropY = Math.max(0, Math.floor(sourceY - padding));
    const cropRight = Math.min(sourceCanvas.width, Math.ceil(sourceX + sourceWidth + padding));
    const cropBottom = Math.min(sourceCanvas.height, Math.ceil(sourceY + sourceHeight + padding));

    return {
        x: cropX,
        y: cropY,
        width: Math.max(1, cropRight - cropX),
        height: Math.max(1, cropBottom - cropY)
    };
}

function qrScanCropCanvas(sourceCanvas, cropArea) {
    const canvas = document.createElement("canvas");
    canvas.width = cropArea.width;
    canvas.height = cropArea.height;

    const context = canvas.getContext("2d");
    if (!context) {
        throw new Error("Не удалось получить контекст canvas для обрезки");
    }

    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(
        sourceCanvas,
        cropArea.x,
        cropArea.y,
        cropArea.width,
        cropArea.height,
        0,
        0,
        cropArea.width,
        cropArea.height
    );

    return canvas;
}

function hasOption(select, value) {
    for (let i = 0; i < select.options.length; i++) {
        if (select.options[i].value === value) {
            return true;
        }
    }
    return false;
}

function getSelectedAccountId() {
    if (!accountSelect || accountSelect.disabled || !accountSelect.value) {
        throw new Error("Счёт для сохранения чека не выбран.");
    }

    return accountSelect.value;
}

// ===================== Глобальная обработка Ctrl+V =====================
function initGlobalPasteHandler() {
    // любой Ctrl+V на странице (кроме ввода в инпуты/textarea/contentEditable)
    document.addEventListener("paste", qrScanHandlePaste);
}

// ===================== Сворачиваемые секции (ручной ввод / результаты) =====================
function initCollapseSections() {
    document.addEventListener("click", function (e) {
        if (!(e.target instanceof Element)) {
            return;
        }

        const btn = e.target.closest(".qr-section__header");
        if (!btn) return;

        const targetSelector = btn.getAttribute("data-collapse-target");
        if (!targetSelector) return;

        const body = document.querySelector(targetSelector);
        if (!body) return;

        const isCollapsed = !btn.classList.contains("qr-section__header--collapsed");
        qrScanSetCollapseState(btn, body, isCollapsed);
    });
}
