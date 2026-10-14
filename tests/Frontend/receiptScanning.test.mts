import { test } from "node:test";
import assert from "node:assert/strict";
import { createCameraController } from "../../CostVision.Web/wwwroot/dist/pages/receipt/cameraController.js";
import { createReceiptStorage } from "../../CostVision.Web/wwwroot/dist/pages/receipt/draftPersistence.js";
import { getReceiptStorageKeys } from "../../CostVision.Web/wwwroot/dist/pages/receipt/constants.js";
import { submitQrScanAsync } from "../../CostVision.Web/wwwroot/dist/pages/receipt/qrScanner.js";
import { createFileScanner } from "../../CostVision.Web/wwwroot/dist/pages/receipt/fileScanner.js";
import { qrScanGetPhotoDateFromFile, qrScanLoadImageFromFile } from "../../CostVision.Web/wwwroot/dist/pages/receipt/fileDecoding.js";
import { HttpError } from "../../CostVision.Web/wwwroot/dist/shared/http.js";

const USER_A = "11111111-1111-1111-1111-111111111111";

test("actual entry preserves jsQR fallback and EXIF-to-lastModified fallback without cutting off neighbours", async () => {
    await environment(async f => {
        // Arrange
        await f.start(); f.controls.decodeError = true;
        const globals = globalThis as any;
        globals.jsQR = () => ({ data: "QR:fallback" });
        f.window.EXIF = globals.EXIF = { readFromBinaryFile() { return false; } };
        f.element("multiFileInput").files = [new File(["x"], "fallback.jpg", { lastModified: new Date("2026-10-01T01:02:03Z").getTime() })];
        // Act
        await f.element("multiFileInput").fire("change"); await settle();
        // Assert
        assert.equal(f.posts.length, 1);
        assert.equal(f.posts[0].body.results[0].decodedText, "QR:fallback");
        assert.equal(f.posts[0].body.results[0].photoDateTime, "2026-10-01T01:02:03.000Z");
        assert.equal(f.urls.size, 0);
    });
});

test("owned EXIF FileReader aborts during lifecycle cancellation and removes its handlers", async () => {
    await environment(async f => {
        // Arrange
        const globals = globalThis as any;
        let aborted = 0, parsedCount = 0;
        const readers: any[] = [];
        globals.FileReader = class {
            onload: Function | null = null; onerror: Function | null = null; onabort: Function | null = null;
            result = new ArrayBuffer(4);
            constructor() { readers.push(this); }
            readAsArrayBuffer() {}
            abort() { aborted++; this.onabort?.(); }
        };
        f.window.EXIF = globals.EXIF = { readFromBinaryFile() { parsedCount++; return {}; } };
        const controller = new AbortController();
        // Act
        const pending = qrScanGetPhotoDateFromFile(new File(["data"], "pending.jpg"), controller.signal);
        controller.abort();
        await assert.rejects(pending, (error: any) => error.name === "AbortError");
        // Assert
        assert.equal(aborted, 1);
        assert.equal(parsedCount, 0);
        assert.equal(readers[0].onload, null);
        assert.equal(readers[0].onerror, null);
        assert.equal(readers[0].onabort, null);
    });
});

test("actual mobile entry shows every server row and disposes its feedback modal and camera", async () => {
    await environment(async f => {
        // Arrange
        f.controls.mobile = true; await f.start();
        f.controls.postTask = deferred();
        f.element("multiFileInput").files = [new File(["x"], "created.png"), new File(["x"], "duplicate.png")];
        // Act
        await f.element("multiFileInput").fire("change"); await settle();
        const rows = f.posts[0].body.results;
        rows[1].errorMessage = "Такой чек уже есть на выбранном счёте.";
        f.controls.postTask.resolve(response(outcome(rows, 1))); await settle();
        // Assert
        const modal = f.body.querySelector(".qr-camera-modal")!;
        assert.match(modal.text(), /created.png/);
        assert.match(modal.text(), /duplicate.png/);
        assert.match(modal.text(), /Такой чек уже есть/);
        // Act
        await f.windowEvents.fire("pagehide", { persisted: false }); await settle();
        // Assert
        assert.equal(f.modalDisposals.length, 1);
        assert.equal(modal.parentNode, null);
        assert.equal(f.scanners[0].state, 1);
        assert.equal(f.scanners[0].clears, 1);
    });
});

test("actual entry invalidates a previous user's visible draft after logout without deleting retained personal storage", async () => {
    await environment(async f => {
        // Arrange
        f.storage.items.set(getReceiptStorageKeys(USER_A).draft, JSON.stringify(draft));
        await f.start();
        f.controls.identityTask = deferred();
        // Act
        f.document.visibilityState = "hidden"; await f.docEvents.fire("visibilitychange");
        f.document.visibilityState = "visible"; await f.docEvents.fire("visibilitychange");
        f.controls.identityTask.resolve(Response.json({ success: false, message: "Войдите снова." }, { status: 401 }));
        await settle();
        // Assert
        assert.equal(f.element("manual-fn").value, "");
        assert.equal(f.element("accountSelect").disabled, true);
        assert.equal(f.posts.length, 0);
        assert.ok(f.storage.items.has(getReceiptStorageKeys(USER_A).draft));
        assert.deepEqual(f.storage.removals, []);
        assert.match(f.element("receipt-page-message").textContent, /Войдите снова/);
    });
});

test("actual camera entry switches to the latest requested device and never accepts a previous start callback", async () => {
    await environment(async f => {
        // Arrange
        f.controls.mobile = true; await f.start();
        f.controls.startTask = deferred();
        const select = f.body.querySelector("#qr-reader-camera-select")!;
        // Act
        select.value = "other"; await select.fire("change"); await settle();
        select.value = "back"; await select.fire("change");
        f.startCalls[1].callback("QR:old-camera");
        f.controls.startTask.resolve(); await settle();
        // Assert
        assert.deepEqual(f.startCalls.map(call => call.id), ["back", "other", "back"]);
        assert.equal(f.posts.length, 0);
        assert.equal(f.scanners[0].state, 2);
        assert.equal(f.storage.items.get("qrScan.selectedCameraId"), "back");
    });
});

test("actual entry fails closed on a missing user identity and malformed account list", async () => {
    await environment(async f => {
        // Arrange
        f.element("receipt-page").dataset.currentUserId = "";
        // Act
        await f.start();
        // Assert
        assert.match(f.element("receipt-page-message").textContent, /пользователь/);
        assert.equal(f.posts.length, 0);
        assert.equal(f.storage.reads.length, 0);
    });
    await environment(async f => {
        // Arrange
        f.controls.accountsTask = deferred();
        await f.start();
        // Act
        f.controls.accountsTask.resolve(response([{ id: "A", name: null }])); await settle();
        // Assert
        assert.equal(f.element("accountSelect").disabled, true);
        assert.notEqual(f.element("accountSelectError").textContent, "");
        assert.equal(f.posts.length, 0);
    });
});
const USER_B = "22222222-2222-2222-2222-222222222222";
const parsed = { dateTime: "2026-10-14T12:34:00", sum: 12.34, fiscalDriveNumber: "1234567890123456",
    fiscalDocumentNumber: "1234", fiscalSign: "12345678", operationType: 1 };
const draft = { fn: "1234567890123456", fd: "1234", fp: "12345678", sum: "12.34", date: "2026-10-14T12:34", type: "1" };
function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (error: unknown) => void;
    const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail; });
    return { promise, resolve, reject };
}
const settle = async () => { for (let i = 0; i < 5; i++) await new Promise(resolve => setImmediate(resolve)); };
const response = (data: unknown) => Response.json({ success: true, data });
function outcome(rows: any[], added = rows.length) {
    return { scannedCount: rows.filter(row => row.decodedText?.trim()).length, addedToDbCount: added,
        errorCount: rows.filter(row => row.errorMessage?.trim()).length,
        results: rows.map(row => ({ ...row, parsed: row.decodedText ? parsed : null })) };
}
class StorageStub {
    items = new Map<string, string>();
    reads: string[] = [];
    writes: string[] = [];
    removals: string[] = [];
    getItem(key: string) { this.reads.push(key); return this.items.get(key) ?? null; }
    setItem(key: string, value: string) { this.writes.push(key); this.items.set(key, value); }
    removeItem(key: string) { this.removals.push(key); this.items.delete(key); }
}
class Classes {
    values = new Set<string>();
    add(...values: string[]) { values.forEach(value => this.values.add(value)); }
    remove(...values: string[]) { values.forEach(value => this.values.delete(value)); }
    contains(value: string) { return this.values.has(value); }
    toggle(value: string, force?: boolean) {
        const enabled = force ?? !this.contains(value);
        if (enabled) this.add(value); else this.remove(value);
        return enabled;
    }
}
class ElementStub {
    tagName: string;
    id = ""; name = ""; value = ""; type = ""; disabled = false; textContent = "";
    children: ElementStub[] = [];
    parentNode: ElementStub | null = null;
    dataset: Record<string, string> = {};
    classList = new Classes();
    attributes = new Map<string, string>();
    listeners = new Map<string, { fn: Function; once: boolean }[]>();
    style: any = { setProperty() {}, removeProperty() {} };
    files: File[] = [];
    isContentEditable = false;
    contentWindow: any = null;
    contentDocument: any = null;
    onload: Function | null = null;
    onerror: Function | null = null;
    src = "";
    constructor(tag = "div") { this.tagName = tag.toUpperCase(); }
    set className(value: string) { this.classList.values = new Set(value.split(/\s+/).filter(Boolean)); }
    get className() { return [...this.classList.values].join(" "); }
    append(...children: ElementStub[]) { children.forEach(child => this.appendChild(child)); }
    appendChild(child: ElementStub) { child.parentNode = this; this.children.push(child); return child; }
    insertBefore(child: ElementStub, before: ElementStub) {
        child.parentNode = this;
        const index = this.children.indexOf(before);
        this.children.splice(index < 0 ? this.children.length : index, 0, child);
    }
    replaceChildren(...children: ElementStub[]) { this.children.forEach(child => child.parentNode = null); this.children = []; this.append(...children); }
    remove() { if (this.parentNode) { this.parentNode.children = this.parentNode.children.filter(child => child !== this); this.parentNode = null; } }
    get previousElementSibling() { return this.parentNode?.children[this.parentNode.children.indexOf(this) - 1] ?? null; }
    setAttribute(key: string, value: string) { this.attributes.set(key, value); }
    getAttribute(key: string) { return this.attributes.get(key) ?? null; }
    hasAttribute(key: string) { return this.attributes.has(key); }
    addEventListener(name: string, fn: Function, options?: any) {
        const items = this.listeners.get(name) ?? [];
        items.push({ fn, once: options?.once === true });
        this.listeners.set(name, items);
    }
    removeEventListener(name: string, fn: Function) { this.listeners.set(name, (this.listeners.get(name) ?? []).filter(item => item.fn !== fn)); }
    async fire(name: string, values: Record<string, unknown> = {}) {
        const event = { type: name, target: this, preventDefault() {}, stopPropagation() {}, ...values };
        for (const item of [...(this.listeners.get(name) ?? [])]) {
            if (item.once) this.removeEventListener(name, item.fn);
            await item.fn(event);
        }
    }
    matches(selector: string): boolean {
        if (selector.startsWith("#")) return this.id === selector.slice(1);
        if (selector.startsWith(".")) return this.classList.contains(selector.slice(1));
        return this.tagName.toLowerCase() === selector;
    }
    querySelector(selector: string): ElementStub | null {
        for (const child of this.children) {
            if (child.matches(selector)) return child;
            const descendant = child.querySelector(selector);
            if (descendant) return descendant;
        }
        return null;
    }
    closest(selector: string): ElementStub | null { return selector.split(",").some(value => this.matches(value.trim())) ? this : this.parentNode?.closest(selector) ?? null; }
    contains(item: ElementStub): boolean { return item === this || this.children.some(child => child.contains(item)); }
    click() { void this.fire("click"); }
    focus() {}
    getContext() { return { fillRect() {}, drawImage() {}, getImageData: () => ({ data: new Uint8ClampedArray(16) }), putImageData() {} }; }
    text(): string { return this.textContent + this.children.map(child => child.text()).join(" "); }
}

let sequence = 0;
async function environment(action: (f: ReturnType<typeof makeEnvironment>) => Promise<void>) {
    const f = makeEnvironment();
    const names = Object.keys(f.globals);
    const descriptors = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
    for (const [name, value] of Object.entries(f.globals)) {
        Object.defineProperty(globalThis, name, { configurable: true, writable: true, value });
    }
    try { await action(f); }
    finally {
        await f.windowEvents.fire("pagehide", { persisted: false });
        await settle();
        f.timers.forEach(timer => clearTimeout(timer));
        for (const [name, descriptor] of descriptors) {
            if (descriptor) Object.defineProperty(globalThis, name, descriptor);
            else Reflect.deleteProperty(globalThis, name);
        }
    }
}
function makeEnvironment() {
    const body = new ElementStub();
    const docEvents = new ElementStub();
    const windowEvents = new ElementStub();
    const nodes = new Map<string, ElementStub>();
    const element = (id: string, tag = "div") => {
        if (!nodes.has(id)) { const node = new ElementStub(tag); node.id = id; nodes.set(id, node); body.append(node); }
        return nodes.get(id)!;
    };
    element("receipt-page").dataset.currentUserId = USER_A;
    const account = element("accountSelect", "select");
    account.disabled = true;
    element("accountSelectError");
    element("receipt-page-message").classList.add("d-none");
    element("qrForm", "form");
    element("qr-dropzone");
    element("multiFileInput", "input");
    element("file-scan-root");
    element("decoded-results-host");
    element("qr-reader");
    element("manual-request-root");
    element("manual-check-btn", "button");
    element("manual-result-message");
    for (const name of ["fn", "fd", "fp", "sum", "date", "type"]) element("manual-" + name, name === "type" ? "select" : "input");
    element("manual-type").value = "1";
    const storage = new StorageStub();
    const urls = new Set<string>();
    const revoked: string[] = [];
    const frames: ElementStub[] = [];
    const scanners: any[] = [];
    const decodeCalls: string[] = [];
    const startCalls: { id: string; callback: Function; scanner: any }[] = [];
    const posts: { url: string; body: any; options: any }[] = [];
    const modalDisposals: ElementStub[] = [];
    const timers = new Set<ReturnType<typeof setTimeout>>();
    let nextUrl = 0;
    const urlApi = () => ({
        createObjectURL: () => { const url = "blob:local/" + ++nextUrl; urls.add(url); return url; },
        revokeObjectURL: (url: string) => { urls.delete(url); revoked.push(url); }
    });
    const outerUrls = urlApi();
    class LocalURL extends URL {
        static createObjectURL() { return outerUrls.createObjectURL(); }
        static revokeObjectURL(url: string) { outerUrls.revokeObjectURL(url); }
    }
    const controls = {
        serverUser: USER_A, accountsTask: null as ReturnType<typeof deferred<Response>> | null,
        identityTask: null as ReturnType<typeof deferred<Response>> | null,
        postTask: null as ReturnType<typeof deferred<Response>> | null,
        decodeTask: null as ReturnType<typeof deferred<{ decodedText: string }>> | null,
        startTask: null as ReturnType<typeof deferred<void>> | null,
        imageTask: false,
        mobile: false,
        scriptFailure: false,
        decodeError: false
    };
    class Scanner {
        state = 1;
        clears = 0; stops = 0; resumes = 0;
        realm: any;
        constructor(_root: string, realm?: any) { this.realm = realm; scanners.push(this); }
        getState() { return this.state; }
        async start(id: string, _config: unknown, callback: Function) {
            startCalls.push({ id, callback, scanner: this });
            if (controls.startTask) await controls.startTask.promise;
            this.state = 2;
        }
        async stop() { this.stops++; this.state = 1; }
        pause() { this.state = 3; }
        resume() { this.resumes++; this.state = 2; }
        clear() { this.clears++; }
        async scanFileV2(file: File) {
            decodeCalls.push(file.name);
            this.realm.URL.createObjectURL(file);
            this.realm.URL.createObjectURL(file);
            if (controls.decodeTask) return controls.decodeTask.promise;
            if (controls.decodeError) throw new Error("No QR in primary decoder");
            return { decodedText: "QR:" + file.name };
        }
        static async getCameras() { return [{ id: "back", label: "back main camera" }, { id: "other", label: "back wide camera" }]; }
    }
    const createElement = (tag: string): ElementStub => {
        const node = new ElementStub(tag);
        if (tag === "iframe") {
            const frameBody = new ElementStub();
            const frameHead = new ElementStub();
            const realm: any = { File, URL: urlApi() };
            realm.Html5Qrcode = class extends Scanner { constructor(root: string) { super(root, realm); } };
            const baseAppend = frameHead.appendChild.bind(frameHead);
            frameHead.appendChild = script => {
                baseAppend(script);
                queueMicrotask(() => controls.scriptFailure ? script.onerror?.() : script.onload?.());
                return script;
            };
            node.contentWindow = realm;
            node.contentDocument = { body: frameBody, head: frameHead, createElement: (tag: string) => new ElementStub(tag) };
            frames.push(node);
        }
        return node;
    };
    const vendor = new ElementStub("script");
    vendor.src = "https://local.invalid/lib/html5-qrcode/html5-qrcode.min.js";
    const document: any = {
        body, head: new ElementStub(), visibilityState: "visible", activeElement: null,
        getElementById: (id: string) => nodes.get(id) ?? body.querySelector("#" + id),
        createElement, createTextNode: (text: string) => { const node = new ElementStub("text"); node.textContent = text; return node; },
        querySelector: (selector: string) => selector.startsWith('script[src') ? vendor : selector.startsWith('meta[') ? { getAttribute: () => "token" } : body.querySelector(selector),
        querySelectorAll: () => [],
        addEventListener: docEvents.addEventListener.bind(docEvents),
        removeEventListener: docEvents.removeEventListener.bind(docEvents)
    };
    const window: any = {
        location: { href: "https://local.invalid/add-receipt" },
        addEventListener: windowEvents.addEventListener.bind(windowEvents),
        removeEventListener: windowEvents.removeEventListener.bind(windowEvents),
        matchMedia: () => ({ matches: controls.mobile }),
        setTimeout(callback: Function, delay: number) {
            const timer = setTimeout(() => { timers.delete(timer); callback(); }, delay);
            timers.add(timer); return timer;
        },
        clearTimeout, requestAnimationFrame(callback: Function) { callback(); },
        bootstrap: { Modal: class {
            node: ElementStub;
            constructor(node: ElementStub) { this.node = node; }
            static getOrCreateInstance(node: ElementStub) { return new this(node); }
            show() { this.node.classList.add("show"); }
            hide() { this.node.classList.remove("show"); void this.node.fire("hidden.bs.modal"); }
            dispose() { modalDisposals.push(this.node); }
        } }
    };
    class ImageStub {
        naturalWidth = 2; naturalHeight = 2; width = 2; height = 2;
        onload: Function | null = null; onerror: Function | null = null;
        value = "";
        set src(value: string) { this.value = value; if (value && !controls.imageTask) queueMicrotask(() => this.onload?.()); }
        get src() { return this.value; }
    }
    class ReaderStub {
        result: ArrayBuffer | null = null;
        error = null;
        onload: Function | null = null; onerror: Function | null = null; onabort: Function | null = null;
        aborted = false;
        async readAsArrayBuffer(file: File) { this.result = await file.arrayBuffer(); if (!this.aborted) this.onload?.(); }
        abort() { this.aborted = true; this.onabort?.(); }
    }
    const globals = {
        document, window, localStorage: storage,
        navigator: { maxTouchPoints: 0, userAgent: "" },
        Element: ElementStub, Node: ElementStub, HTMLElement: ElementStub, HTMLFormElement: ElementStub,
        HTMLInputElement: ElementStub, HTMLSelectElement: ElementStub, HTMLButtonElement: ElementStub,
        Html5Qrcode: Scanner, Html5QrcodeScannerState: { NOT_STARTED: 1, SCANNING: 2, PAUSED: 3 },
        Image: ImageStub, FileReader: ReaderStub, URL: LocalURL, EXIF: undefined, jsQR: undefined,
        fetch: async (url: any, options: any) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (options?.method === "POST") {
                const item = { url: String(url), body: JSON.parse(options.body), options };
                posts.push(item);
                if (controls.postTask) return controls.postTask.promise;
                return handler === "Manual" ? response({ outcome: 0, receipt: {} }) : response(outcome(item.body.results));
            }
            if (handler === "BrowserUser") return controls.identityTask ? controls.identityTask.promise : response(controls.serverUser);
            if (handler === "Accounts") return controls.accountsTask ? controls.accountsTask.promise : response([{ id: "account-A", name: "Счёт A" }, { id: "account-B", name: "Счёт B" }]);
            throw new Error("Unexpected read: " + url);
        }
    };
    return { globals, controls, element, document, window, body, storage, urls, revoked, frames, scanners, decodeCalls, startCalls, posts, modalDisposals, docEvents, windowEvents, timers,
        async start() {
            await import(new URL("../../CostVision.Web/wwwroot/dist/pages/receipt/index.js?isolated=" + ++sequence, import.meta.url).href);
            await docEvents.fire("DOMContentLoaded");
            await settle();
        },
        fillManual() { for (const [key, value] of Object.entries(draft)) element("manual-" + key).value = value; }
    };
}

test("actual receipt entry owns a captured file packet and account, blocks paste/drop/manual overlap and releases resources", async () => {
    await environment(async f => {
        // Arrange
        await f.start();
        f.controls.decodeTask = deferred();
        const files = [new File(["a"], "first.png"), new File(["b"], "second.png")];
        f.element("multiFileInput").files = files;
        // Act
        await f.element("multiFileInput").fire("change");
        await settle();
        files.push(new File(["late"], "not-captured.png"));
        f.element("accountSelect").value = "account-B";
        await f.docEvents.fire("paste", { clipboardData: { items: [{ kind: "file", getAsFile: () => new File(["p"], "paste.png") }] } });
        await f.element("qr-dropzone").fire("drop", { dataTransfer: { files: [new File(["drop"], "drop.png")] } });
        f.fillManual();
        await f.element("manual-check-btn").fire("click");
        f.controls.decodeTask.resolve({ decodedText: "QR:first" });
        await settle();
        // Assert
        assert.equal(f.posts.length, 1);
        assert.equal(f.posts[0].body.accountId, "account-A");
        assert.deepEqual(f.posts[0].body.results.map((row: any) => row.fileName), ["first.png", "second.png"]);
        assert.deepEqual(f.decodeCalls, ["first.png", "second.png"]);
        assert.equal(f.posts[0].options.signal, undefined);
        assert.equal(f.posts[0].options.headers.RequestVerificationToken, "token");
        assert.equal(f.urls.size, 0);
        assert.ok(f.frames.every(frame => frame.parentNode === null));
        assert.equal(f.body.querySelector(".qr-overlay")!.classList.contains("d-none"), true);
    });
});

test("actual entry shows a save rejection and explicitly retries the retained packet with its original account", async () => {
    await environment(async f => {
        // Arrange
        await f.start();
        f.controls.postTask = deferred();
        f.element("multiFileInput").files = [new File(["qr"], "rejected.png")];
        // Act
        await f.element("multiFileInput").fire("change");
        await settle();
        f.controls.postTask.resolve(Response.json({ success: false, message: "Нет права записи." }, { status: 403 }));
        await settle();
        // Assert
        assert.match(f.element("receipt-page-message").textContent, /Нет права записи/);
        assert.equal(f.posts.length, 1);
        const retry = f.body.querySelector("#receipt-scan-retry")!;
        assert.equal(retry.classList.contains("d-none"), false);
        // Act
        f.controls.postTask = deferred();
        f.element("accountSelect").value = "account-B";
        await f.element("qrForm").fire("submit");
        await settle();
        f.controls.postTask.resolve(Response.json({ success: false, message: "Повторный конфликт." }, { status: 409 }));
        await settle();
        // Assert
        assert.equal(f.posts.length, 2);
        assert.equal(f.posts[1].body.accountId, "account-A");
        assert.equal(f.posts[1].body.results[0].fileName, "rejected.png");
        assert.equal(retry.classList.contains("d-none"), false);
        // Act
        f.controls.postTask = null;
        await f.element("qrForm").fire("submit"); await settle();
        // Assert
        assert.equal(f.posts.length, 3);
        assert.equal(f.posts[2].body.accountId, "account-A");
        assert.equal(retry.classList.contains("d-none"), true);
    });
});

for (const failure of ["network", "malformed", "counters", "missing-rows", "wrong-error-row"]) {
    test("actual entry reports unknown write outcome for " + failure + " and never retries POST", async () => {
        await environment(async f => {
            // Arrange
            await f.start();
            f.controls.postTask = deferred();
            f.element("multiFileInput").files = [new File(["qr"], "unknown.png")];
            // Act
            await f.element("multiFileInput").fire("change");
            await settle();
            if (failure === "network") f.controls.postTask.reject(new TypeError("lost response"));
            else if (failure === "malformed") f.controls.postTask.resolve(new Response("{broken", { status: 200 }));
            else {
                const data: any = outcome(f.posts[0].body.results);
                if (failure === "counters") data.addedToDbCount = -1;
                if (failure === "missing-rows") delete data.results;
                if (failure === "wrong-error-row") data.results[0].errorMessage = {};
                f.controls.postTask.resolve(response(data));
            }
            await settle();
            await f.element("qrForm").fire("submit");
            await f.element("multiFileInput").fire("change");
            f.fillManual();
            await f.element("manual-check-btn").fire("click");
            await settle();
            // Assert
            assert.match(f.element("receipt-page-message").textContent, /неизвестен/);
            assert.equal(f.posts.length, 1);
            assert.equal(f.body.querySelector(".qr-overlay")!.classList.contains("d-none"), true);
            assert.equal(f.body.querySelector("#decoded-results-section"), null);
        });
    });
}

test("actual entry keeps the new manual draft and captured payload during an outstanding save, blocking a double click", async () => {
    await environment(async f => {
        // Arrange
        await f.start();
        f.fillManual();
        f.controls.postTask = deferred();
        // Act
        await f.element("manual-check-btn").fire("click");
        await settle();
        f.element("manual-sum").value = "99.99";
        f.element("accountSelect").value = "account-B";
        await f.element("manual-sum").fire("input");
        await f.element("manual-check-btn").fire("click");
        f.controls.postTask.resolve(response({ outcome: 0, receipt: {} }));
        await settle();
        // Assert
        assert.equal(f.posts.length, 1);
        assert.equal(f.posts[0].body.accountId, "account-A");
        assert.equal(f.posts[0].body.receipt.sum, 12.34);
        assert.equal(f.element("manual-sum").value, "99.99");
        assert.equal(JSON.parse(f.storage.items.get(getReceiptStorageKeys(USER_A).draft)!).sum, "99.99");
        assert.equal(f.element("manual-check-btn").disabled, false);
    });
});

test("actual entry clears only the unchanged saved manual draft", async () => {
    await environment(async f => {
        // Arrange
        await f.start(); f.fillManual();
        await f.element("manual-sum").fire("input");
        // Act
        await f.element("manual-check-btn").fire("click");
        await settle();
        // Assert
        assert.equal(f.element("manual-sum").value, "");
        assert.equal(f.storage.items.has(getReceiptStorageKeys(USER_A).draft), false);
        assert.deepEqual(f.storage.removals, [getReceiptStorageKeys(USER_A).draft]);
    });
});

test("actual entry never restores before the authenticated identity read or after a late account response to pagehide", async () => {
    await environment(async f => {
        // Arrange
        f.storage.items.set(getReceiptStorageKeys(USER_A).draft, JSON.stringify(draft));
        f.controls.identityTask = deferred();
        await f.start();
        // Assert
        assert.equal(f.element("manual-fn").value, "");
        assert.equal(f.storage.reads.length, 0);
        // Act
        await f.windowEvents.fire("pagehide", { persisted: false });
        f.controls.identityTask.resolve(response(USER_A));
        await settle();
        // Assert
        assert.equal(f.element("manual-fn").value, "");
        assert.equal(f.element("accountSelect").disabled, true);
        assert.equal(f.storage.reads.length, 0);
    });
});

test("actual entry leaves legacy data untouched and restores only the current user's keys", async () => {
    await environment(async f => {
        // Arrange
        f.storage.items.set("qrScan.manualReceiptDraft", JSON.stringify({ ...draft, fn: "LEGACY-SECRET" }));
        f.storage.items.set("qrScan.selectedAccountId", "legacy-account");
        f.storage.items.set(getReceiptStorageKeys(USER_B).draft, JSON.stringify({ ...draft, fn: "OTHER-SECRET" }));
        f.storage.items.set(getReceiptStorageKeys(USER_B).account, "account-B");
        f.storage.items.set(getReceiptStorageKeys(USER_A).draft, JSON.stringify(draft));
        f.storage.items.set(getReceiptStorageKeys(USER_A).account, "account-A");
        // Act
        await f.start();
        // Assert
        assert.equal(f.element("manual-fn").value, draft.fn);
        assert.equal(f.element("accountSelect").value, "account-A");
        assert.ok(f.storage.reads.every(key => key.startsWith("qrScan.user." + USER_A)));
        assert.deepEqual(f.storage.removals, []);
        assert.match(f.storage.items.get("qrScan.manualReceiptDraft")!, /LEGACY-SECRET/);
    });
});

test("actual entry rechecks identity before a POST and clears displayed values when the browser user changes", async () => {
    await environment(async f => {
        // Arrange
        await f.start(); f.fillManual();
        f.controls.serverUser = USER_B;
        // Act
        await f.element("manual-check-btn").fire("click");
        await settle();
        // Assert
        assert.equal(f.posts.length, 0);
        assert.equal(f.element("manual-fn").value, "");
        assert.equal(f.element("accountSelect").disabled, true);
        assert.match(f.element("receipt-page-message").textContent, /Пользователь изменился/);
    });
});

test("actual entry rechecks identity after decoding, retaining no foreign user's visible packet", async () => {
    await environment(async f => {
        // Arrange
        await f.start();
        f.controls.decodeTask = deferred();
        f.element("multiFileInput").files = [new File(["qr"], "switch.png")];
        // Act
        await f.element("multiFileInput").fire("change"); await settle();
        f.controls.serverUser = USER_B;
        f.controls.decodeTask.resolve({ decodedText: "QR:secret" });
        await settle();
        // Assert
        assert.equal(f.posts.length, 0);
        assert.equal(f.element("accountSelect").disabled, true);
        assert.equal(f.urls.size, 0);
        assert.equal(f.element("decoded-results-host").text(), "");
    });
});

test("actual entry handles an expired cookie without restoring a draft or claiming save success", async () => {
    await environment(async f => {
        // Arrange
        f.storage.items.set(getReceiptStorageKeys(USER_A).draft, JSON.stringify(draft));
        f.controls.identityTask = deferred();
        await f.start();
        // Act
        f.controls.identityTask.resolve(Response.json({ success: false, message: "Сессия истекла." }, { status: 401 }));
        await settle();
        // Assert
        assert.equal(f.element("manual-fn").value, "");
        assert.equal(f.storage.reads.length, 0);
        assert.equal(f.element("manual-check-btn").disabled, true);
        assert.match(f.element("receipt-page-message").textContent, /Сессия истекла/);
    });
});

test("actual entry cancels decoding on pagehide, removes listeners and ignores a delayed decoder callback", async () => {
    await environment(async f => {
        // Arrange
        await f.start(); f.controls.decodeTask = deferred();
        f.element("multiFileInput").files = [new File(["a"], "a.png"), new File(["b"], "b.png")];
        await f.element("multiFileInput").fire("change"); await settle();
        // Act
        await f.windowEvents.fire("pagehide", { persisted: false }); await settle();
        f.controls.decodeTask.resolve({ decodedText: "OLD" });
        await f.element("multiFileInput").fire("change"); await settle();
        // Assert
        assert.deepEqual(f.decodeCalls, ["a.png"]);
        assert.equal(f.posts.length, 0);
        assert.equal(f.urls.size, 0);
        assert.ok(f.frames.every(frame => frame.parentNode === null));
        assert.equal(f.body.querySelector(".qr-overlay"), null);
        assert.equal(f.docEvents.listeners.get("paste")!.length, 0);
        assert.equal(f.element("multiFileInput").listeners.get("change")!.length, 0);
    });
});

test("actual entry preserves a newer overlay against an old finally after visibility cancellation", async () => {
    await environment(async f => {
        // Arrange
        await f.start();
        const old = deferred<{ decodedText: string }>();
        f.controls.decodeTask = old;
        f.element("multiFileInput").files = [new File(["a"], "old.png")];
        await f.element("multiFileInput").fire("change"); await settle();
        // Act
        f.document.visibilityState = "hidden";
        await f.docEvents.fire("visibilitychange"); await settle();
        f.document.visibilityState = "visible";
        await f.docEvents.fire("visibilitychange"); await settle();
        const next = deferred<{ decodedText: string }>();
        f.controls.decodeTask = next;
        f.element("multiFileInput").files = [new File(["b"], "new.png")];
        await f.element("multiFileInput").fire("change"); await settle();
        old.resolve({ decodedText: "OLD" }); await settle();
        // Assert
        assert.equal(f.body.querySelector(".qr-overlay")!.classList.contains("d-none"), false);
        assert.equal(f.posts.length, 0);
        // Act
        next.resolve({ decodedText: "NEW" }); await settle();
        // Assert
        assert.equal(f.posts.length, 1);
        assert.equal(f.posts[0].body.results[0].fileName, "new.png");
        assert.equal(f.body.querySelector(".qr-overlay")!.classList.contains("d-none"), true);
    });
});

test("actual bfcache resume does not abort or repeat a pending POST, and requires server reconciliation", async () => {
    await environment(async f => {
        // Arrange
        await f.start(); f.fillManual(); f.controls.postTask = deferred();
        await f.element("manual-check-btn").fire("click"); await settle();
        const savedText = f.element("manual-result-message").textContent;
        // Act
        await f.windowEvents.fire("pagehide", { persisted: true });
        await f.windowEvents.fire("pageshow", { persisted: true }); await settle();
        f.controls.postTask.resolve(response({ outcome: 0, receipt: {} })); await settle();
        await f.element("manual-check-btn").fire("click"); await settle();
        // Assert
        assert.equal(f.posts.length, 1);
        assert.equal(f.posts[0].options.signal, undefined);
        assert.equal(f.element("manual-result-message").textContent, savedText);
        assert.match(f.element("receipt-page-message").textContent, /неизвестен/);
        assert.equal(f.element("manual-fn").value, draft.fn);
    });
});

test("actual camera entry rejects late callbacks after stop and serializes stop during start", async () => {
    await environment(async f => {
        // Arrange
        f.controls.mobile = true; f.controls.startTask = deferred();
        await f.start();
        assert.equal(f.startCalls.length, 1);
        // Act
        const stop = f.body.querySelector("#qr-reader-camera-stop")!;
        await stop.fire("click");
        f.startCalls[0].callback("QR:late");
        f.controls.startTask.resolve(); await settle();
        f.startCalls[0].callback("QR:late-again"); await settle();
        // Assert
        assert.equal(f.scanners[0].state, 1);
        assert.equal(f.scanners[0].stops, 1);
        assert.equal(f.posts.length, 0);
    });
});

test("actual camera entry awaits one submission, pauses feedback and stops on visibility hide", async () => {
    await environment(async f => {
        // Arrange
        f.controls.mobile = true; await f.start();
        f.controls.postTask = deferred();
        // Act
        f.startCalls[0].callback("QR:camera"); await settle();
        f.startCalls[0].callback("QR:another"); await settle();
        // Assert
        assert.equal(f.posts.length, 1);
        assert.equal(f.posts[0].body.results[0].decodedText, "QR:camera");
        assert.equal(f.scanners[0].state, 3);
        // Act
        f.controls.postTask.resolve(response(outcome(f.posts[0].body.results))); await settle();
        const modal = f.body.querySelector(".qr-camera-modal")!;
        assert.equal(modal.classList.contains("show"), true);
        f.document.visibilityState = "hidden";
        await f.docEvents.fire("visibilitychange"); await settle();
        // Assert
        assert.equal(f.scanners[0].state, 1);
        assert.equal(f.scanners[0].resumes, 0);
        assert.equal(f.posts.length, 1);
    });
});

test("camera boundary coalesces rapid switches, suppresses stale callbacks and clears after disposal", async () => {
    await environment(async () => {
        // Arrange
        const task = deferred<void>();
        const calls: string[] = [], received: string[] = [], states: (string | null)[] = [];
        let state = 1; let callback: Function | null = null;
        const scanner: any = { getState: () => state, pause: () => { state = 3; }, resume: () => { state = 2; },
            clear: () => calls.push("clear"), stop: async () => { calls.push("stop"); state = 1; },
            start: async (id: string, _config: unknown, decoded: Function) => {
                calls.push("start:" + id); callback = decoded;
                if (id === "A") await task.promise;
                state = 2;
            } };
        const camera = createCameraController(scanner, { onDecoded: text => received.push(text), onRunning: id => states.push(id) });
        // Act
        const first = camera.start("A"); await settle();
        const second = camera.start("B");
        const third = camera.start("C");
        callback!("old");
        task.resolve(); await Promise.all([first, second, third]);
        callback!("current");
        await camera.dispose();
        callback!("disposed");
        // Assert
        assert.deepEqual(calls, ["start:A", "stop", "start:C", "stop", "clear"]);
        assert.deepEqual(states, ["C"]);
        assert.deepEqual(received, ["current"]);
        await assert.rejects(camera.start("D"), /освобождён/);
    });
});

test("camera boundary suppresses an obsolete start rejection but reports the current device failure", async () => {
    await environment(async () => {
        // Arrange
        const pending = deferred<void>();
        const running: (string | null)[] = [];
        let state = 1;
        const scanner: any = { getState: () => state, clear() {},
            stop: async () => { state = 1; },
            start: async (id: string) => {
                if (id === "A") await pending.promise;
                if (id === "C") throw new Error("current device failure");
                state = 2;
            } };
        const camera = createCameraController(scanner, { onDecoded() {}, onRunning: id => running.push(id) });

        // Act
        const old = camera.start("A"); await settle();
        const current = camera.start("B");
        pending.reject(new Error("obsolete device failure"));
        await Promise.all([old, current]);

        // Assert
        assert.deepEqual(running, ["B"]);
        await assert.rejects(camera.start("C"), /current device failure/);
        await camera.dispose();
    });
});

test("file-scanner adapter owns both vendor URLs and releases them on decode failure or cancellation", async () => {
    await environment(async f => {
        // Arrange
        const root = f.element("file-scan-root");
        const controller = new AbortController();
        const scanner = await createFileScanner(root as any, "/lib/html5-qrcode/html5-qrcode.min.js", controller.signal);
        f.controls.decodeError = true;
        // Act
        await assert.rejects(scanner.scanFileV2(new File(["bad"], "bad.png")));
        // Assert
        assert.equal(f.urls.size, 0);
        assert.equal(f.scanners[0].clears, 1);
        // Act
        f.controls.decodeError = false; f.controls.decodeTask = deferred();
        const pending = scanner.scanFileV2(new File(["slow"], "slow.png"));
        await settle();
        controller.abort();
        await assert.rejects(pending, (error: any) => error.name === "AbortError");
        f.controls.decodeTask.resolve({ decodedText: "late" }); await settle();
        // Assert
        assert.equal(f.urls.size, 0);
        assert.equal(f.frames[0].parentNode, null);
        assert.equal(f.revoked.length, 4);
    });
});

test("file-scanner initialization handles a vendor load error without leaked frame or resources", async () => {
    await environment(async f => {
        // Arrange
        f.controls.scriptFailure = true;
        // Act
        await assert.rejects(createFileScanner(f.element("file-scan-root") as any, "/vendor", new AbortController().signal), /загрузить/);
        // Assert
        assert.equal(f.urls.size, 0);
        assert.equal(f.frames[0].parentNode, null);
    });
});

test("owned image decoding releases its object URL and handlers on cancellation", async () => {
    await environment(async f => {
        // Arrange
        f.controls.imageTask = true;
        const controller = new AbortController();
        const image = qrScanLoadImageFromFile(new File(["image"], "image.png"), controller.signal);
        // Act
        controller.abort();
        // Assert
        await assert.rejects(image, (error: any) => error.name === "AbortError");
        assert.equal(f.urls.size, 0);
    });
});

test("EXIF boundary uses the actual binary API with local file data and preserves tag priority", async () => {
    await environment(async f => {
        // Arrange
        const globals = globalThis as any;
        let reads = 0;
        f.window.EXIF = globals.EXIF = { readFromBinaryFile(buffer: ArrayBuffer) {
            reads++; assert.equal(buffer.byteLength, 4);
            return { DateTimeOriginal: "2026:10:14 12:34:56", DateTimeDigitized: "2020:01:01 01:01:01" };
        } };
        // Act
        const date = await qrScanGetPhotoDateFromFile(new File(["data"], "exif.jpg"), new AbortController().signal);
        // Assert
        assert.equal(date, new Date(2026, 9, 14, 12, 34, 56).toISOString());
        assert.equal(reads, 1);
        delete globals.EXIF;
    });
});

test("personal storage keeps users and legacy keys separate, retains data across a new session and rejects malformed drafts", () => {
    // Arrange
    const storage = new StorageStub();
    storage.items.set("qrScan.manualReceiptDraft", JSON.stringify({ ...draft, fn: "legacy" }));
    const a = createReceiptStorage(USER_A, storage as any);
    const b = createReceiptStorage(USER_B, storage as any);
    // Act
    a.writeDraft(draft); a.writeAccount("account-A");
    b.writeDraft({ ...draft, fn: "user-B" }); b.writeAccount("account-B");
    const nextA = createReceiptStorage(USER_A, storage as any);
    // Assert
    assert.equal(nextA.readDraft()!.fn, draft.fn);
    assert.equal(nextA.readAccount(), "account-A");
    assert.equal(b.readDraft()!.fn, "user-B");
    assert.equal(b.readAccount(), "account-B");
    assert.ok(!storage.reads.includes("qrScan.manualReceiptDraft"));
    assert.deepEqual(storage.removals, []);
    // Act / Assert
    storage.items.set(getReceiptStorageKeys(USER_A).draft, '{"sum":null}');
    assert.throws(() => a.readDraft(), /черновик/);
    assert.throws(() => createReceiptStorage("Display Name", storage as any), /пользователь/);
    assert.throws(() => createReceiptStorage("00000000-0000-0000-0000-000000000000", storage as any), /пользователь/);
});

test("QR API preserves pending/dedup error neighbours and rejects malformed counters/rows without fictitious success", async () => {
    await environment(async f => {
        // Arrange
        const rows = [{ fileName: "one", decodedText: "QR", errorMessage: null },
            { fileName: "duplicate", decodedText: "QR", errorMessage: "Такой чек уже есть на выбранном счёте." },
            { fileName: "bad", decodedText: "", errorMessage: "Не удалось распознать QR-код" }];
        const data = outcome(rows, 1);
        // Act
        const globals = globalThis as any;
        globals.fetch = async () => response(data);
        const result = await submitQrScanAsync("/add-receipt", { accountId: "account-A", results: rows }, null);
        // Assert
        assert.equal(result.addedToDbCount, 1);
        assert.equal(result.errorCount, 2);
        assert.equal(result.results[1].errorMessage, rows[1].errorMessage);
        // Act / Assert
        for (const count of [null, "1", -1, 1.5, 9]) {
            globals.fetch = async () => response({ ...data, scannedCount: count });
            await assert.rejects(submitQrScanAsync("/add-receipt", { accountId: "account-A", results: rows }, null),
                (error: unknown) => error instanceof HttpError && error.kind === "contract");
        }
        globals.fetch = async () => response({ ...data, results: [null] });
        await assert.rejects(submitQrScanAsync("/add-receipt", { accountId: "account-A", results: rows }, null), HttpError);
        for (const fields of [{ sum: null }, { operationType: null }, { operationType: 5 }]) {
            globals.fetch = async () => response({ ...data, results: data.results.map((row, index) =>
                index === 0 ? { ...row, parsed: { ...parsed, ...fields } } : row) });
            await assert.rejects(submitQrScanAsync("/add-receipt", { accountId: "account-A", results: rows }, null), HttpError);
        }
        globals.fetch = async () => response({ ...data, results: data.results.map((row, index) =>
            index === 1 ? { ...row, parsed: { ...parsed, operationType: 5, sum: null } } : row) });
        const rejectedNeighbour = await submitQrScanAsync("/add-receipt", { accountId: "account-A", results: rows }, null);
        assert.equal(rejectedNeighbour.errorCount, 2);
    });
});
