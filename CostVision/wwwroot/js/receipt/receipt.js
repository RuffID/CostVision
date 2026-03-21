let qrScanForm;
let qrScanResultsContainer;
let qrScanFileInput;
let qrScanDropzone;

let qrScanResultIndex = 0;
let qrScanHtml5QrcodeScanner;
let qrScanFileScanner;
let qrScanLastCameraText = null;
let qrScanLastCameraTs = 0;

let qrScanOverlay = null;     // DOM-элемент оверлея загрузки
let antiForgeryToken = null;
let accountSelect = null;
const ACCOUNT_STORAGE_KEY = "qrScan.selectedAccountId";
const MANUAL_RECEIPT_DRAFT_STORAGE_KEY = "qrScan.manualReceiptDraft";
let qrScanResults = [];
let qrScanLastSubmitSource = null;
let qrScanCameraPaused = false;
let qrScanCameraModal = null;

document.addEventListener("DOMContentLoaded", function () {
    initQrScan();
    initManualCheckValidation();
    initCollapseSections();
    initGlobalPasteHandler(); // глобальный Ctrl+V по всей странице
});

function initQrScan() {
    qrScanForm = document.getElementById("qrForm");
    qrScanResultsContainer = document.getElementById("decoded-inputs");
    qrScanFileInput = document.getElementById("multiFileInput");
    qrScanDropzone = document.getElementById("qr-dropzone");

    const qrReaderElement = document.getElementById("qr-reader");
    const fileScanRootElement = document.getElementById("file-scan-root");
    accountSelect = document.getElementById("accountSelect");
    antiForgeryToken = getRequestVerificationToken();

    const mobile = isMobileDevice();

    if (!qrScanForm || !qrScanResultsContainer || !qrScanFileInput ||
        !qrReaderElement || !fileScanRootElement) {
        console.warn("QR scan: необходимые элементы не найдены в DOM");
        return;
    }

    if (accountSelect) {
        // восстановить выбор из localStorage, если он есть и валиден
        let savedId = localStorage.getItem(ACCOUNT_STORAGE_KEY);
        if (savedId && hasOption(accountSelect, savedId)) {
            accountSelect.value = savedId;
        }

        // при смене — сохранять выбор
        accountSelect.addEventListener("change", function () {
            localStorage.setItem(ACCOUNT_STORAGE_KEY, accountSelect.value);
        });
    }

    // Перехватываем submit формы, чтобы всегда работать через AJAX
    qrScanForm.addEventListener("submit", function (e) {
        e.preventDefault();
        qrScanSubmitFormAjax();
    });

    // ====== Камера: только мобильные ======
    if (mobile && qrReaderElement) {
        document.body.classList.add("qr-mobile-scan-mode");
        qrScanHtml5QrcodeScanner = new Html5QrcodeScanner(
            "qr-reader",
            {
                fps: 10,
                qrbox: 250,
                rememberLastUsedCamera: false,
                supportedScanTypes: [Html5QrcodeScanType.SCAN_TYPE_CAMERA]
            },
            false
        );
        qrScanHtml5QrcodeScanner.render(qrScanOnScanSuccess, qrScanOnScanError);
        initQrScanMobileScannerUi(qrReaderElement);
    } else if (qrReaderElement) {
        // На ПК виджет камеры не показываем вообще
        qrReaderElement.style.display = "none";
    }

    // ====== Сканер файлов (общий для всех устройств) ======
    qrScanFileScanner = new Html5Qrcode("file-scan-root");

    // Обработка выбора файлов через input
    qrScanFileInput.addEventListener("change", function (e) {
        const files = e.target.files;
        if (!files || files.length === 0) {
            return;
        }

        // показать анимацию загрузки и отправить файлы
        qrScanShowOverlay();
        qrScanScanFilesAndSubmit(files);
    });

    // Инициализация drag-and-drop холста
    if (qrScanDropzone)
        initQrScanDropzone();
}

// ===================== AJAX-отправка формы =====================

async function qrScanSubmitFormAjax() {
    if (!qrScanForm) {
        return;
    }

    const url = qrScanForm.getAttribute("action") || window.location.href;

    const payload = {
        accountId: getSelectedAccountId(),
        results: qrScanResults
    }

    try {
        const data = await sendJsonRequest(url, "POST", buildJsonHeaders(antiForgeryToken), payload);

        if (!data.success) {
            console.error("QR scan: запрос завершился с ошибкой", data.errorMessage);
            qrScanResumeCameraAfterFeedback();
            return;
        }

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

function qrScanApplyServerResponse(data) {
    if (!data) {
        return;
    }

    const scannedCount = typeof data.scannedCount === "number" ? data.scannedCount : 0;
    const addedToDbCount = typeof data.addedToDbCount === "number" ? data.addedToDbCount : 0;
    const errorCount = typeof data.errorCount === "number" ? data.errorCount : 0;
    const errorMessage = typeof data.errorMessage === "string" ? data.errorMessage : "";
    const results = Array.isArray(data.results) ? data.results : [];

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
    container.className = "qr-status";

    // Вставить над выбором счёта
    if (accountSelect) {
        // Берём не сам select, а его "строку" / form-group, если есть
        let anchor = accountSelect.closest(".form-group, .mb-3, .row") || accountSelect;

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

    container.innerHTML = "";

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
        addedItem.className = "qr-status__item qr-status__item--info";

        const addedText = document.createElement("div");
        addedText.className = "qr-status__text";
        addedText.textContent = "Добавлено: " + addedToDbCount;

        addedItem.appendChild(addedText);
        container.appendChild(addedItem);
    }

    if (duplicateCount > 0) {
        const duplicateItem = document.createElement("div");
        duplicateItem.className = "qr-status__item qr-status__item--warning";

        const duplicateText = document.createElement("div");
        duplicateText.className = "qr-status__text";
        duplicateText.textContent = "Уже добавлено: " + duplicateCount;

        duplicateItem.appendChild(duplicateText);
        container.appendChild(duplicateItem);
    }

    if (errorCount > 0) {
        const errorItem = document.createElement("div");
        errorItem.className = "qr-status__item qr-status__item--error";

        const errorText = document.createElement("div");
        errorText.className = "qr-status__text";
        errorText.textContent = "Ошибок: " + errorCount;

        errorItem.appendChild(errorText);
        container.appendChild(errorItem);
    }

    if (errorMessage && errorMessage.length > 0) {
        const err = document.createElement("div");
        err.className = "qr-status__item qr-status__item--error";

        const title = document.createElement("div");
        title.className = "qr-status__title";
        title.textContent = "Ошибка при сканировании";

        const text = document.createElement("div");
        text.className = "qr-status__text";
        text.textContent = errorMessage;

        err.appendChild(title);
        err.appendChild(text);
        container.appendChild(err);
    }
}

function qrScanRenderManualStatus(message, isSuccess) {
    const container = qrScanEnsureStatusContainer();
    if (!container) {
        return;
    }

    container.innerHTML = "";

    const item = document.createElement("div");
    item.className = "qr-status__item " + (isSuccess ? "qr-status__item--info" : "qr-status__item--error");

    const title = document.createElement("div");
    title.className = "qr-status__title";
    title.textContent = "Ручной ввод чека";

    const text = document.createElement("div");
    text.className = "qr-status__text";
    text.textContent = message;

    item.appendChild(title);
    item.appendChild(text);
    container.appendChild(item);
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
        wrapper.className = "qr-section";

        const headerBtn = document.createElement("button");
        headerBtn.type = "button";
        headerBtn.className = "qr-section__header qr-section__header--collapsed";
        headerBtn.setAttribute("data-collapse-target", "#decoded-results-section");

        const headerTextSpan = document.createElement("span");
        headerTextSpan.textContent = "Результаты сканирования";

        const headerArrowSpan = document.createElement("span");
        headerArrowSpan.className = "qr-section__arrow";
        headerArrowSpan.setAttribute("aria-hidden", "true");

        headerBtn.appendChild(headerTextSpan);
        headerBtn.appendChild(headerArrowSpan);

        sectionBody = document.createElement("div");
        sectionBody.id = "decoded-results-section";
        sectionBody.className = "qr-section__body qr-section__body--collapsed";

        ul = document.createElement("ul");
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
            sectionBody.appendChild(ul);
        }
    }

    ul.innerHTML = "";

    for (let i = 0; i < results.length; i++) {
        const r = results[i];
        const li = document.createElement("li");
        li.style.marginBottom = "12px";

        const fileName = document.createElement("strong");
        fileName.textContent = r.fileName || "Файл";

        const headerSpan = document.createElement("span");
        headerSpan.style.whiteSpace = "nowrap";

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

        if (r.errorMessage) {
            const errSpan = document.createElement("span");
            errSpan.style.color = "red";
            errSpan.textContent = r.errorMessage;
            li.appendChild(errSpan);
        } else if (r.parsed) {
            const parsedDiv = document.createElement("div");

            let dtText = "-";
            if (r.parsed.dateTime) {
                try {
                    const dt2 = new Date(r.parsed.dateTime);
                    if (!Number.isNaN(dt2.getTime())) {
                        const day = dt2.toLocaleDateString();
                        const time = dt2.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
                        dtText = `${day} ${time}`;
                    }
                } catch {
                    // игнорировать
                }
            }

            let sumText = "-";
            if (typeof r.parsed.sum === "number") {
                sumText = r.parsed.sum.toFixed(2) + " ₽";
            }

            parsedDiv.innerHTML =
                "Чек от: <strong>" + dtText + "</strong>, " +
                "сумма: <strong>" + sumText + "</strong>";

            li.appendChild(parsedDiv);
        }

        ul.appendChild(li);
    }
}

// ===================== Работа с результатами (Results) =====================

function qrScanClearResults() {
    if (qrScanResultsContainer) {
        qrScanResultsContainer.innerHTML = "";
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
async function qrScanScanFilesAndSubmit(fileList) {
    qrScanClearResults();
    qrScanLastSubmitSource = "files";

    const files = Array.from(fileList);

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
    // сделать dropzone фокусируемой, чтобы можно было вставлять Ctrl+V
    if (!qrScanDropzone.hasAttribute("tabindex")) {
        qrScanDropzone.setAttribute("tabindex", "0");
    }

    qrScanDropzone.addEventListener("click", function () {
        if (qrScanFileInput) {
            qrScanFileInput.click();
        }
    });

    qrScanDropzone.addEventListener("dragenter", qrScanHandleDragEnterOrOver);
    qrScanDropzone.addEventListener("dragover", qrScanHandleDragEnterOrOver);

    qrScanDropzone.addEventListener("dragleave", qrScanHandleDragLeaveOrEnd);
    qrScanDropzone.addEventListener("dragend", qrScanHandleDragLeaveOrEnd);

    qrScanDropzone.addEventListener("drop", qrScanHandleDrop);
}

function qrScanHandleDragEnterOrOver(event) {
    event.preventDefault();
    event.stopPropagation();

    qrScanDropzone.classList.add("qr-dropzone--active");
}

function qrScanHandleDragLeaveOrEnd(event) {
    event.preventDefault();
    event.stopPropagation();

    qrScanDropzone.classList.remove("qr-dropzone--active");
}

function qrScanHandleDrop(event) {
    event.preventDefault();
    event.stopPropagation();

    qrScanDropzone.classList.remove("qr-dropzone--active");

    const dt = event.dataTransfer;
    if (!dt) {
        return;
    }

    const files = dt.files;
    if (!files || files.length === 0) {
        return;
    }

    qrScanShowOverlay();
    qrScanScanFilesAndSubmit(files);
}

function qrScanHandlePaste(event) {
    const clipboardData = event.clipboardData || window.clipboardData;
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
    overlay.className = "qr-overlay";
    overlay.innerHTML =
        "<div class='qr-overlay__spinner'></div>" +
        "<div class='qr-overlay__text'>Обработка данных...</div>";

    document.body.appendChild(overlay);
    qrScanOverlay = overlay;
    return overlay;
}

function qrScanShowOverlay() {
    const overlay = qrScanEnsureOverlay();
    overlay.style.display = "flex";
    document.body.classList.add("qr-overlay-active");
}

function qrScanHideOverlay() {
    if (!qrScanOverlay) {
        return;
    }

    qrScanOverlay.style.display = "none";
    document.body.classList.remove("qr-overlay-active");
}

// ===================== Ручной ввод: валидация полей =====================

function initManualCheckValidation() {
    const root = document.getElementById("manual-request-root");
    const btn = document.getElementById("manual-check-btn");
    const resultMessage = document.getElementById("manual-result-message");

    if (!root || !btn) {
        return;
    }

    const fnInput = document.getElementById("manual-fn");
    const fdInput = document.getElementById("manual-fd");
    const fpInput = document.getElementById("manual-fp");
    const sumInput = document.getElementById("manual-sum");
    const dateInput = document.getElementById("manual-date");
    const typeSelect = document.getElementById("manual-type");

    qrScanRestoreManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
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
            resultMessage.classList.remove("manual-result--error", "manual-result--success");
        }

        const payload = {
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

        qrScanLastSubmitSource = "manual";

        qrScanShowOverlay();
        try {
            const data = await sendJsonRequest("?handler=Manual", "POST", buildJsonHeaders(antiForgeryToken), payload);

            if (!data.success) {
                const failureMessage =
                    (typeof data.errorMessage === "string" && data.errorMessage.trim().length > 0)
                        ? data.errorMessage
                        : "Указаны некорректные данные чека";

                qrScanRenderManualStatus(failureMessage, false);
                if (resultMessage) {
                    resultMessage.textContent = failureMessage;
                    resultMessage.classList.remove("manual-result--success");
                    resultMessage.classList.add("manual-result--error");
                }
                return;
            }

            if (data && data.isCreated) {
                qrScanClearManualDraft(fnInput, fdInput, fpInput, sumInput, dateInput, typeSelect);
                qrScanRenderManualStatus("Чек добавлен в систему", true);
                if (resultMessage) {
                    resultMessage.textContent = "Чек добавлен в систему";
                    resultMessage.classList.remove("manual-result--error");
                    resultMessage.classList.add("manual-result--success");
                }
            } else if (data && data.isCreated === false) {
                // Ошибка создания: выводит текст из ErrorMessage, если он есть
                const msg =
                    (typeof data.errorMessage === "string" && data.errorMessage.trim().length > 0)
                        ? data.errorMessage
                        : "Указаны некорректные данные чека";

                qrScanRenderManualStatus(msg, false);
                if (resultMessage) {
                    resultMessage.textContent = msg;
                    resultMessage.classList.remove("manual-result--success");
                    resultMessage.classList.add("manual-result--error");
                }
            } else {
                // Непредвиденный формат ответа
                qrScanRenderManualStatus("Указаны некорректные данные чека", false);
                if (resultMessage) {
                    resultMessage.textContent = "Указаны некорректные данные чека";
                    resultMessage.classList.remove("manual-result--success");
                    resultMessage.classList.add("manual-result--error");
                }
            }
        } catch (e) {
            console.error("QR scan: ошибка ручного запроса", e);
            qrScanRenderManualStatus("Указаны некорректные данные чека", false);
            if (resultMessage) {
                resultMessage.textContent = "Указаны некорректные данные чека";
                resultMessage.classList.remove("manual-result--success");
                resultMessage.classList.add("manual-result--error");
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
        resultMessage.classList.remove("manual-result--error", "manual-result--success");
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

    row.classList.add("manual-row--error");
    input.classList.add("manual-input--error");

    let errorSpan = row.querySelector(".manual-field-error");
    if (!errorSpan) {
        errorSpan = document.createElement("div");
        errorSpan.className = "manual-field-error";
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

    row.classList.remove("manual-row--error");
    input.classList.remove("manual-input--error");

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

    details.innerHTML = "";

    if (!receiptResult) {
        title.textContent = "Сканирование завершено";
        subtitle.textContent = source === "files"
            ? "Файл обработан, но результат не найден."
            : "Данные чека получены.";
        qrScanAppendCameraModalDetail(details, "Статус", "Результат не найден");
    } else if (receiptResult.errorMessage) {
        title.textContent = source === "files" ? "Файл обработан с ошибкой" : "Чек считан с ошибкой";
        subtitle.textContent = receiptResult.errorMessage;
        qrScanAppendParsedReceiptDetails(details, receiptResult.parsed);
    } else {
        title.textContent = addedToDbCount > 0 ? "Чек добавлен" : "Чек уже есть в системе";
        subtitle.textContent = addedToDbCount > 0
            ? (source === "files"
                ? "Файл успешно распознан. Проверьте данные чека."
                : "Проверьте данные и закройте окно для продолжения сканирования.")
            : "Такой чек уже был добавлен ранее.";
        qrScanAppendParsedReceiptDetails(details, receiptResult.parsed);
    }

    if (errorCount > 0 && errorMessage) {
        qrScanAppendCameraModalDetail(details, "Ошибка", errorMessage);
    }

    modal.style.display = "flex";
    document.body.classList.add("qr-camera-modal-active");
}

function qrScanEnsureCameraModal() {
    if (qrScanCameraModal) {
        return qrScanCameraModal;
    }

    const modal = document.createElement("div");
    modal.className = "qr-camera-modal";
    modal.innerHTML =
        "<div class='qr-camera-modal__backdrop' data-camera-modal-close='true'></div>" +
        "<div class='qr-camera-modal__dialog' role='dialog' aria-modal='true' aria-labelledby='qr-camera-modal-title'>" +
        "<div class='qr-camera-modal__header'>" +
        "<div>" +
        "<div id='qr-camera-modal-title' class='qr-camera-modal__title'></div>" +
        "<div class='qr-camera-modal__subtitle'></div>" +
        "</div>" +
        "<button type='button' class='qr-camera-modal__close' data-camera-modal-close='true' aria-label='Закрыть'>×</button>" +
        "</div>" +
        "<div class='qr-camera-modal__details'></div>" +
        "<div class='qr-camera-modal__footer'>" +
        "<button type='button' class='qr-camera-modal__button' data-camera-modal-close='true'>Продолжить сканирование</button>" +
        "</div>" +
        "</div>";

    modal.addEventListener("click", function (event) {
        const target = event.target;
        if (target instanceof Element && target.closest("[data-camera-modal-close='true']")) {
            qrScanHideCameraReceiptFeedback();
        }
    });

    document.body.appendChild(modal);
    qrScanCameraModal = modal;
    return modal;
}

function qrScanHideCameraReceiptFeedback() {
    if (!qrScanCameraModal) {
        qrScanResumeCameraAfterFeedback();
        return;
    }

    qrScanCameraModal.style.display = "none";
    document.body.classList.remove("qr-camera-modal-active");
    qrScanResumeCameraAfterFeedback();
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
    item.className = "qr-camera-modal__detail";

    const labelElement = document.createElement("div");
    labelElement.className = "qr-camera-modal__detail-label";
    labelElement.textContent = label;

    const valueElement = document.createElement("div");
    valueElement.className = "qr-camera-modal__detail-value";
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

function initQrScanMobileScannerUi(qrReaderElement) {
    if (!qrReaderElement) {
        return;
    }

    if (qrReaderElement.dataset.mobileUiInitialized === "1") {
        return;
    }

    qrReaderElement.dataset.mobileUiInitialized = "1";

    qrReaderElement.classList.add("qr-reader-shell");

    const refreshUi = function () {
        qrScanLocalizeScannerUi(qrReaderElement);
        qrScanInjectGalleryButton(qrReaderElement);
        qrScanSelectPreferredCamera(qrReaderElement);
        qrScanHideCameraSelector(qrReaderElement);
    };

    refreshUi();

    const observer = new MutationObserver(function () {
        observer.disconnect();

        try {
            refreshUi();
        } finally {
            observer.observe(qrReaderElement, {
                childList: true,
                subtree: true
            });
        }
    });

    observer.observe(qrReaderElement, {
        childList: true,
        subtree: true
    });
}

function qrScanLocalizeScannerUi(qrReaderElement) {
    const permissionButton = qrReaderElement.querySelector("#html5-qrcode-button-camera-permission");
    if (permissionButton) {
        permissionButton.textContent = "Разрешить доступ к камере";
    }

    const startButton = qrReaderElement.querySelector("#html5-qrcode-button-camera-start");
    if (startButton) {
        const currentText = (startButton.textContent || "").trim();
        if (/Launching Camera/i.test(currentText)) {
            startButton.textContent = "Открываем камеру...";
        } else {
            startButton.textContent = "Сканировать камерой";
        }
    }

    const stopButton = qrReaderElement.querySelector("#html5-qrcode-button-camera-stop");
    if (stopButton) {
        stopButton.textContent = "Остановить";
    }

    const galleryButton = qrReaderElement.querySelector(".qr-reader__gallery-button");
    if (galleryButton) {
        galleryButton.textContent = "Выбрать из галереи";
    }

    const select = qrReaderElement.querySelector("#html5-qrcode-select-camera");
    if (select && select.parentElement) {
        const optionElements = Array.from(select.options);
        for (let i = 0; i < optionElements.length; i++) {
            const option = optionElements[i];
            option.text = qrScanTranslateCameraOption(option.text, i + 1);
        }

        const parentTextNode = Array.from(select.parentElement.childNodes).find(function (node) {
            return node.nodeType === Node.TEXT_NODE && node.textContent && node.textContent.trim().length > 0;
        });

        if (parentTextNode) {
            parentTextNode.textContent = "Камера ";
        }
    }

    const headerMessage = qrReaderElement.querySelector("#qr-reader__header_message");
    if (headerMessage && headerMessage.textContent) {
        const text = headerMessage.textContent.trim();
        if (text === "Requesting camera permissions...") {
            headerMessage.textContent = "Запрашиваем доступ к камере...";
        } else if (text === "No camera found") {
            headerMessage.textContent = "Камера не найдена";
        } else if (text === "Scanning") {
            headerMessage.textContent = "Сканирование";
        } else if (text === "Idle") {
            headerMessage.textContent = "Ожидание";
        }
    }
}

function qrScanInjectGalleryButton(qrReaderElement) {
    if (!qrScanFileInput) {
        return;
    }

    const dashboard = qrReaderElement.querySelector("#qr-reader__dashboard");
    if (!dashboard) {
        return;
    }

    let galleryButton = dashboard.querySelector(".qr-reader__gallery-button");
    if (!galleryButton) {
        galleryButton = document.createElement("button");
        galleryButton.type = "button";
        galleryButton.className = "qr-reader__gallery-button";
        galleryButton.addEventListener("click", function () {
            qrScanFileInput.click();
        });
        dashboard.appendChild(galleryButton);
    }

    galleryButton.textContent = "Выбрать из галереи";
}

function qrScanTranslateCameraOption(text, fallbackIndex) {
    const value = (text || "").trim();
    const match = value.match(/camera\s+(\d+)\s*,\s*facing\s+(front|back)/i);
    if (match) {
        const cameraNumber = match[1];
        const facing = /back/i.test(match[2]) ? "задняя" : "фронтальная";
        return `Камера ${cameraNumber}, ${facing}`;
    }

    if (/anonymous camera/i.test(value)) {
        return `Камера ${fallbackIndex}`;
    }

    return value
        .replace(/camera/gi, "Камера")
        .replace(/facing back/gi, "задняя")
        .replace(/facing front/gi, "фронтальная");
}

function qrScanSelectPreferredCamera(qrReaderElement) {
    const select = qrReaderElement.querySelector("#html5-qrcode-select-camera");
    if (!select || select.options.length === 0) {
        return;
    }

    const optionElements = Array.from(select.options);
    const preferredOption = optionElements.find(function (option) {
        return /back|rear|environment|задняя/i.test(option.text);
    }) || optionElements[0];

    if (!preferredOption) {
        return;
    }

    if (select.value !== preferredOption.value) {
        select.value = preferredOption.value;
        select.dispatchEvent(new Event("change", { bubbles: true }));
    }

    qrReaderElement.dataset.preferredCameraId = preferredOption.value;
    qrReaderElement.dataset.preferredCameraFacing = /back|rear|environment|задняя/i.test(preferredOption.text) ? "back" : "other";
}

function qrScanHideCameraSelector(qrReaderElement) {
    const select = qrReaderElement.querySelector("#html5-qrcode-select-camera");
    if (!select || !select.parentElement) {
        return;
    }

    if (qrReaderElement.dataset.preferredCameraFacing === "back") {
        select.parentElement.classList.add("qr-reader__camera-select--hidden");
    } else {
        select.parentElement.classList.remove("qr-reader__camera-select--hidden");
    }
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
    return accountSelect ? accountSelect.value : null;
}

// ===================== Глобальная обработка Ctrl+V =====================
function initGlobalPasteHandler() {
    // любой Ctrl+V на странице (кроме ввода в инпуты/textarea/contentEditable)
    document.addEventListener("paste", qrScanHandlePaste);
}

// ===================== Сворачиваемые секции (ручной ввод / результаты) =====================
function initCollapseSections() {
    document.addEventListener("click", function (e) {
        const btn = e.target.closest(".qr-section__header");
        if (!btn) return;

        const targetSelector = btn.getAttribute("data-collapse-target");
        if (!targetSelector) return;

        const body = document.querySelector(targetSelector);
        if (!body) return;

        btn.classList.toggle("qr-section__header--collapsed");
        body.classList.toggle("qr-section__body--collapsed");
    });
}
