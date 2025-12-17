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
let qrScanResults = [];

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
        qrScanHtml5QrcodeScanner = new Html5QrcodeScanner(
            "qr-reader",
            {
                fps: 10,
                qrbox: 250
            },
            false
        );
        qrScanHtml5QrcodeScanner.render(qrScanOnScanSuccess, qrScanOnScanError);
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
            return;
        }

        qrScanApplyServerResponse(data);
    } catch (err) {
        console.error("QR scan: ошибка AJAX-запроса", err);
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

    qrScanRenderStatus(scannedCount, addedToDbCount, errorCount, errorMessage);
    qrScanRenderResults(results);
}

function qrScanEnsureStatusContainer() {
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

function qrScanRenderStatus(scannedCount, addedToDbCount, errorCount, errorMessage) {
    const container = qrScanEnsureStatusContainer();
    container.innerHTML = "";

    if (scannedCount > 0 || errorCount > 0) {
        const info = document.createElement("div");
        info.className = "qr-status__item qr-status__item--info";

        const title = document.createElement("div");
        title.className = "qr-status__title";
        title.textContent = "Сканирование завершено";

        const text = document.createElement("div");
        text.className = "qr-status__text";
        text.innerHTML =
            "Отсканировано: <strong>" + scannedCount + "</strong>, " +
            "добавлено в систему: <strong>" + addedToDbCount + "</strong>.";

        if (errorCount > 0) {
            const span = document.createElement("span");
            span.style.color = "#991b1b";
            span.style.fontWeight = "600";
            span.textContent = " | Ошибок: " + errorCount;
            text.appendChild(span);
        }

        info.appendChild(title);
        info.appendChild(text);
        container.appendChild(info);
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
            const result = await qrScanFileScanner.scanFileV2(file, false);
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

        qrScanShowOverlay();
        try {
            const data = await sendJsonRequest("?handler=Manual", "POST", buildJsonHeaders(antiForgeryToken), payload);

            if (!data.success) {
                qrScanRenderManualStatus("Указаны некорректные данные чека", false);
                if (resultMessage) {
                    resultMessage.textContent = "Указаны некорректные данные чека";
                    resultMessage.classList.remove("manual-result--success");
                    resultMessage.classList.add("manual-result--error");
                }
                return;
            }

            if (data && data.isCreated) {
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