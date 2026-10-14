import { getRequestVerificationToken } from "../../shared/verificationToken.js";
import { requireElementById, requireInputById, requireSelectById } from "../../shared/dom.js";
import { initFileDropzone } from "../../shared/dropzone.js";
import { createRequestCoordinator, getWriteErrorMessage, isUnknownWriteOutcome } from "../../shared/requestCoordinator.js";
import { getOrCreateBootstrapModal } from "../../shared/bootstrap.js";
import { getSelectedAccountId, loadAvailableAccounts, renderAvailableAccounts } from "./accounts.js";
import { loadBrowserUserId } from "./accountsApi.js";
import { createCameraController, isMobileDevice, loadAvailableCameras } from "./cameraController.js";
import { createCameraView, qrScanRenderCameraOptions } from "./cameraUi.js";
import { createFileScanner } from "./fileScanner.js";
import { decodeFiles } from "./fileScanWorkflow.js";
import { createManualForm } from "./manualForm.js";
import { createReceiptSession } from "./state.js";
import { submitScanPacket, submitManualPacket } from "./submission.js";
import { createScanOverlay, renderScanOverlay, qrScanSetCollapseState, qrScanRenderStatus,
    qrScanRenderResults, qrScanRenderManualStatus, createCameraFeedback, renderCameraReceiptFeedback } from "./rendering.js";
import { ManualReceiptOutcome } from "./types.js";
import type { ReceiptStorage, QrScanResult, ManualReceiptPayload } from "./types.js";
import type { RequestTicket } from "../../shared/requestCoordinator.js";
import { CAMERA_STORAGE_KEY } from "./constants.js";
import { HttpError } from "../../shared/http.js";

type ScanPacket = { readonly accountId: string; readonly rows: readonly QrScanResult[]; readonly token: string | null };
const EMPTY_DRAFT = { fn: "", fd: "", fp: "", sum: "", date: "", type: "1" };

export function createReceiptController(userId: string, storage: ReceiptStorage) {
    const form = requireElementById("qrForm", HTMLFormElement);
    const select = requireSelectById("accountSelect");
    const accountError = requireElementById("accountSelectError");
    const fileInput = requireInputById("multiFileInput");
    const dropzone = requireElementById("qr-dropzone");
    const fileRoot = requireElementById("file-scan-root");
    const resultsHost = requireElementById("decoded-results-host");
    const pageMessage = requireElementById("receipt-page-message");
    const reader = requireElementById("qr-reader");
    const manual = createManualForm();
    const session = createReceiptSession();
    const token = getRequestVerificationToken();
    const mobile = isMobileDevice();
    const status = document.createElement("div");
    status.className = "qr-status d-flex flex-column align-items-start gap-2 mt-3 mb-4";
    select.parentNode?.insertBefore(status, select);
    const overlay = createScanOverlay();
    document.body.appendChild(overlay);
    const retry = document.createElement("button");
    retry.id = "receipt-scan-retry";
    retry.type = "submit";
    retry.className = "btn btn-outline-primary d-none";
    retry.textContent = "Повторить сохранение пакета";
    form.appendChild(retry);
    let retainedPacket: ScanPacket | null = null;
    let currentRun: object | null = null;
    let writeInFlight = false;
    let disposed = false;
    let camera: ReturnType<typeof createCameraController> | null = null;
    let cameraView: ReturnType<typeof createCameraView> | null = null;
    let feedback: HTMLElement | null = null;
    let feedbackModal: ReturnType<typeof getOrCreateBootstrapModal> | null = null;
    let feedbackVisible = false;
    const cameraReads = createRequestCoordinator();
    const subscriptions: (() => void)[] = [];

    function bind(target: EventTarget, event: string, handler: EventListener): void {
        target.addEventListener(event, handler);
        subscriptions.push(() => target.removeEventListener(event, handler));
    }
    function showError(message: string): void {
        pageMessage.textContent = message;
        pageMessage.classList.remove("d-none");
    }
    function storageAction(action: () => void): void {
        try {
            action();
        } catch {
            showError("Не удалось обратиться к локальному черновику. Данные формы доступны до ухода со страницы.");
        }
    }
    function clearSensitiveView(): void {
        manual.restore(EMPTY_DRAFT);
        manual.showMessage("", false);
        resultsHost.replaceChildren();
        status.replaceChildren();
        feedback?.replaceChildren();
        select.replaceChildren();
        select.disabled = true;
        retainedPacket = null;
        suspend();
    }
    async function verifyUser(signal: AbortSignal): Promise<void> {
        let actualId: string;
        try {
            actualId = await loadBrowserUserId(token, signal);
        } catch (error) {
            if (!signal.aborted && error instanceof HttpError && (error.status === 401 || error.status === 403)) {
                clearSensitiveView();
                showError(error.message);
            }
            throw error;
        }
        if (signal.aborted) {
            return;
        }
        if (actualId.toLowerCase() !== userId.toLowerCase()) {
            clearSensitiveView();
            showError("Пользователь изменился. Заново откройте страницу добавления чека.");
            throw new Error("Пользователь изменился.");
        }
    }
    function showFeedback(rows: QrScanResult[], added: number, errors: number, message: string, source: "files" | "camera"): void {
        if (!feedback) {
            feedback = createCameraFeedback();
            document.body.appendChild(feedback);
            feedbackModal = getOrCreateBootstrapModal(feedback);
            bind(feedback, "hidden.bs.modal", () => {
                feedbackVisible = false;
                if (session.active && !session.busy) {
                    void camera?.resume().catch(error => showError(error instanceof Error ? error.message : "Не удалось продолжить сканирование."));
                }
            });
        }
        renderCameraReceiptFeedback(feedback, rows, added, errors, message, source);
        feedbackVisible = true;
        feedbackModal!.show();
    }
    function canBegin(): boolean {
        if (!session.active || disposed) {
            return false;
        }
        if (session.requiresReconciliation) {
            showError("Результат записи неизвестен. Откройте список чеков, сверьте факт сохранения и затем заново откройте добавление чека.");
            return false;
        }
        if (session.busy || feedbackVisible) {
            showError("Дождитесь завершения текущей обработки и закройте её результат.");
            return false;
        }
        return true;
    }
    async function run(action: (ticket: RequestTicket<{ accountId: string }>, markWrite: () => void) => Promise<void>, accountId: string): Promise<void> {
        if (!canBegin()) {
            return;
        }
        const owner = {};
        const ticket = session.runs.begin(Object.freeze({ accountId }));
        currentRun = owner;
        session.busy = true;
        retainedPacket = null;
        retry.classList.add("d-none");
        let sent = false;
        renderScanOverlay(overlay, true);
        pageMessage.textContent = "";
        pageMessage.classList.add("d-none");
        retry.disabled = true;
        manual.button.disabled = true;
        try {
            await verifyUser(ticket.signal);
            if (!ticket.isCurrent() || !session.active) {
                return;
            }
            await camera?.pause();
            if (!ticket.isCurrent() || !session.active) {
                return;
            }
            await action(ticket, () => { sent = true; writeInFlight = true; });
        } catch (error) {
            if (sent && isUnknownWriteOutcome(error)) {
                session.requiresReconciliation = true;
            }
            if (ticket.isCurrent() && session.active) {
                showError(sent ? getWriteErrorMessage(error) : error instanceof Error ? error.message : "Не удалось обработать данные.");
            }
        } finally {
            writeInFlight = false;
            if (currentRun === owner) {
                currentRun = null;
                session.busy = false;
                if (ticket.isCurrent() && session.active) {
                    renderScanOverlay(overlay, false);
                    retry.classList.toggle("d-none", retainedPacket === null || session.requiresReconciliation);
                    retry.disabled = session.requiresReconciliation;
                    manual.button.disabled = session.requiresReconciliation;
                    if (!feedbackVisible) {
                        await camera?.resume().catch(error => showError(error instanceof Error ? error.message : "Не удалось продолжить сканирование."));
                    }
                }
            }
        }
    }
    async function savePacket(packet: ScanPacket, source: "files" | "camera", ticket: RequestTicket<{ accountId: string }>, markWrite: () => void): Promise<void> {
        // Decoding may outlast a user switch in a different tab.
        await verifyUser(ticket.signal);
        if (!ticket.isCurrent() || !session.active) {
            return;
        }
        retainedPacket = packet;
        markWrite();
        const response = await submitScanPacket(form.getAttribute("action") || window.location.href, packet.accountId, packet.rows, packet.token);
        if (!ticket.isCurrent() || !session.active) {
            return;
        }
        retainedPacket = null;
        retry.classList.add("d-none");
        qrScanRenderStatus(status, response.scannedCount, response.addedToDbCount, response.errorCount, response.errorMessage || "", response.results);
        qrScanRenderResults(resultsHost, response.results, mobile);
        if (mobile) {
            renderScanOverlay(overlay, false);
            showFeedback(response.results, response.addedToDbCount, response.errorCount, response.errorMessage || "", source);
        }
    }
    async function scanFiles(input: readonly File[]): Promise<void> {
        if (!canBegin() || input.length === 0) {
            return;
        }
        const files = Object.freeze([...input]);
        let accountId: string;
        try {
            accountId = getSelectedAccountId(select);
        } catch (error) {
            showError(error instanceof Error ? error.message : "Счёт не выбран.");
            return;
        }
        await run(async (ticket, markWrite) => {
            const vendor = document.querySelector<HTMLScriptElement>('script[src*="/lib/html5-qrcode/html5-qrcode.min.js"]');
            if (!vendor) {
                throw new Error("Не найдено подключение библиотеки сканирования.");
            }
            const scanner = await createFileScanner(fileRoot, vendor.src, ticket.signal);
            try {
                const rows = await decodeFiles(files, scanner, ticket.signal);
                if (!ticket.isCurrent() || !session.active) {
                    return;
                }
                await savePacket(Object.freeze({ accountId, rows: Object.freeze(rows), token }), "files", ticket, markWrite);
            } finally {
                scanner.dispose();
            }
        }, accountId);
    }
    async function scanCamera(text: string): Promise<void> {
        if (!canBegin()) {
            return;
        }
        const now = Date.now();
        if (session.lastCameraText === text && now - session.lastCameraTime < 5000) {
            return;
        }
        session.lastCameraText = text;
        session.lastCameraTime = now;
        let accountId: string;
        try {
            accountId = getSelectedAccountId(select);
        } catch (error) {
            showError(error instanceof Error ? error.message : "Счёт не выбран.");
            return;
        }
        const packet = Object.freeze({ accountId, token, rows: Object.freeze([{ fileName: "CAMERA", decodedText: text, errorMessage: null }]) });
        await run((ticket, markWrite) => savePacket(packet, "camera", ticket, markWrite), accountId);
    }
    async function saveManual(): Promise<void> {
        if (!canBegin() || !manual.validate()) {
            return;
        }
        let payload: ManualReceiptPayload;
        try {
            payload = manual.payload(getSelectedAccountId(select));
        } catch (error) {
            showError(error instanceof Error ? error.message : "Счёт не выбран.");
            return;
        }
        const draft = Object.freeze(manual.capture());
        await run(async (ticket, markWrite) => {
            markWrite();
            const response = await submitManualPacket(payload, token);
            if (!ticket.isCurrent() || !session.active) {
                return;
            }
            const saved = response.outcome === ManualReceiptOutcome.Created || response.outcome === ManualReceiptOutcome.AddedToAccount;
            const message = response.outcome === ManualReceiptOutcome.Created ? "Чек добавлен в систему" : response.message;
            if (saved && manual.clearIfUnchanged(draft)) {
                storageAction(() => storage.clearDraft());
            }
            qrScanRenderManualStatus(status, message, saved ? "success" : "warning");
            manual.showMessage(message, saved);
        }, payload.accountId);
    }
    function suspend(): void {
        session.active = false;
        session.accounts.invalidate();
        session.runs.invalidate();
        cameraReads.invalidate();
        if (writeInFlight) {
            session.requiresReconciliation = true;
        }
        renderScanOverlay(overlay, false);
        feedbackModal?.hide();
        void camera?.stop().catch(error => console.error("Не удалось остановить камеру.", error));
    }
    async function activate(restoreDraft: boolean): Promise<void> {
        if (disposed) {
            return;
        }
        session.active = true;
        select.disabled = true;
        manual.button.disabled = true;
        const ticket = session.accounts.begin(userId);
        try {
            await verifyUser(ticket.signal);
            if (!ticket.isCurrent() || !session.active) {
                return;
            }
            if (restoreDraft) {
                storageAction(() => {
                    const draft = storage.readDraft();
                    if (draft) {
                        manual.restore(draft);
                    }
                });
                if (!manual.date.value) {
                    const now = new Date();
                    manual.date.value = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
                }
            }
            const accounts = await loadAvailableAccounts(token, ticket.signal);
            if (!ticket.isCurrent() || !session.active) {
                return;
            }
            renderAvailableAccounts(select, accounts);
            let savedId: string | null = null;
            storageAction(() => { savedId = storage.readAccount(); });
            select.value = accounts.some(account => account.id === savedId) ? savedId! : accounts[0].id;
            select.disabled = false;
            manual.button.disabled = session.busy || session.requiresReconciliation;
            accountError.classList.add("d-none");
            if (session.requiresReconciliation) {
                showError("Результат записи неизвестен. Сверьте список чеков перед новым добавлением.");
            }
        } catch (error) {
            if (ticket.isCurrent() && session.active) {
                accountError.textContent = error instanceof Error ? error.message : "Не удалось загрузить счета.";
                accountError.classList.remove("d-none");
                showError(accountError.textContent);
            }
        }
    }
    async function initializeCamera(): Promise<void> {
        if (!mobile) {
            reader.style.display = "none";
            return;
        }
        document.body.classList.add("qr-mobile-scan-mode");
        dropzone.closest(".qr-upload-block")?.classList.add("d-none");
        const start = () => {
            if (session.active && !session.busy && !feedbackVisible && !session.requiresReconciliation) {
                void camera?.start(cameraView!.select.value).catch(error => cameraView?.renderStatus(error instanceof Error ? error.message : "Не удалось запустить камеру.", true));
            }
        };
        cameraView = createCameraView(reader, {
            start, switchCamera: start,
            stop: () => { void camera?.stop().catch(error => cameraView?.renderStatus(error instanceof Error ? error.message : "Не удалось остановить камеру.", true)); },
            gallery: () => fileInput.click()
        });
        camera = createCameraController(new Html5Qrcode("qr-reader-viewport"), {
            onDecoded: text => { void scanCamera(text).catch(error => showError(error instanceof Error ? error.message : "Ошибка сканирования.")); },
            onRunning: id => {
                cameraView!.renderRunning(id !== null);
                cameraView!.renderStatus(id ? "Сканирование активно" : "Камера остановлена", false);
                if (id) {
                    try { localStorage.setItem(CAMERA_STORAGE_KEY, id); } catch { /* Non-sensitive device preference. */ }
                }
            }
        });
        const ticket = cameraReads.begin(userId);
        try {
            const cameras = await loadAvailableCameras();
            if (!ticket.isCurrent() || !session.active) {
                return;
            }
            qrScanRenderCameraOptions(cameraView.select, cameras);
            let saved: string | null = null;
            try { saved = localStorage.getItem(CAMERA_STORAGE_KEY); } catch { /* No saved device preference. */ }
            cameraView.select.value = cameras.some(item => item.id === saved) ? saved! : cameras[0]?.id || "";
            if (cameraView.select.value) {
                await camera.start(cameraView.select.value);
            } else {
                cameraView.renderStatus("Камера не найдена.", true);
            }
        } catch (error) {
            if (ticket.isCurrent() && session.active) {
                cameraView.renderStatus(error instanceof Error ? error.message : "Не удалось получить доступ к камере.", true);
            }
        }
    }

    subscriptions.push(initFileDropzone({ dropzone, fileInput, multiple: true,
        onFilesSelected: files => { void scanFiles(files).catch(error => showError(error instanceof Error ? error.message : "Ошибка сканирования.")); } }));
    bind(document, "paste", event => {
        const paste = event as ClipboardEvent;
        const target = paste.target instanceof HTMLElement ? paste.target : null;
        if (target && (["INPUT", "TEXTAREA"].includes(target.tagName) || target.isContentEditable)) {
            return;
        }
        const files = Array.from(paste.clipboardData?.items || []).filter(item => item.kind === "file")
            .map(item => item.getAsFile()).filter((file): file is File => file !== null);
        if (files.length > 0) {
            paste.preventDefault();
            void scanFiles(files).catch(error => showError(error instanceof Error ? error.message : "Ошибка сканирования."));
        }
    });
    bind(form, "submit", event => {
        event.preventDefault();
        const packet = retainedPacket;
        if (!packet || !canBegin()) {
            return;
        }
        void run((ticket, markWrite) => savePacket(packet, "files", ticket, markWrite), packet.accountId)
            .catch(error => showError(error instanceof Error ? error.message : "Ошибка сохранения."));
    });
    bind(manual.button, "click", () => { void saveManual().catch(error => showError(error instanceof Error ? error.message : "Ошибка сохранения.")); });
    bind(select, "change", () => {
        if (session.active) {
            storageAction(() => storage.writeAccount(select.value));
        }
    });
    subscriptions.push(manual.bindDraft(() => {
        if (session.active) {
            storageAction(() => storage.writeDraft(manual.capture()));
        }
    }));
    bind(document, "click", event => {
        if (!(event.target instanceof Element)) {
            return;
        }
        const button = event.target.closest<HTMLButtonElement>(".qr-section__header");
        const selector = button?.getAttribute("data-collapse-target");
        const body = selector ? document.querySelector<HTMLElement>(selector) : null;
        if (button && body) {
            qrScanSetCollapseState(button, body, !button.classList.contains("qr-section__header--collapsed"));
        }
    });
    bind(document, "visibilitychange", () => {
        if (document.visibilityState === "hidden") {
            suspend();
        } else {
            void activate(false);
        }
    });
    bind(window, "pagehide", event => {
        suspend();
        if (!(event as PageTransitionEvent).persisted) {
            void dispose().catch(error => console.error("Не удалось освободить сканер.", error));
        }
    });
    bind(window, "pageshow", event => {
        if ((event as PageTransitionEvent).persisted) {
            void activate(false);
        }
    });

    async function dispose(): Promise<void> {
        if (disposed) {
            return;
        }
        suspend();
        disposed = true;
        for (const unsubscribe of subscriptions) {
            unsubscribe();
        }
        cameraView?.dispose();
        overlay.remove();
        status.remove();
        retry.remove();
        // Bootstrap owns its hide transition; keep its shared stack's hidden event intact.
        if (feedback && feedbackModal) {
            const element = feedback;
            const modal = feedbackModal;
            if (feedbackVisible) {
                const hideAfterShown = () => modal.hide();
                const finish = () => {
                    element.removeEventListener("shown.bs.modal", hideAfterShown);
                    modal.dispose();
                    element.remove();
                };
                element.addEventListener("hidden.bs.modal", finish, { once: true });
                element.addEventListener("shown.bs.modal", hideAfterShown, { once: true });
                modal.hide();
            } else {
                modal.dispose();
                element.remove();
            }
        }
        await camera?.dispose();
    }
    return {
        async initialize(): Promise<void> {
            await activate(true);
            if (session.active && !select.disabled) {
                await initializeCamera();
            }
        },
        dispose
    };
}
