import { test } from "node:test";
import assert from "node:assert/strict";
import { createPageNavigation } from "../../CostVision.Web/wwwroot/dist/shared/paging.js";
import { createRequestCoordinator } from "../../CostVision.Web/wwwroot/dist/shared/requestCoordinator.js";
import { initReceiptMoneyMovementsModal } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/modals/receiptMoneyMovementsModal.js";
import { loadIncomeExpenseReportAsync } from "../../CostVision.Web/wwwroot/dist/pages/dashboard/api.js";
import { getProducts, getProductStorePurchases } from "../../CostVision.Web/wwwroot/dist/pages/products/api.js";
import { getStores, getStoreReceipts, openStoreReceipt } from "../../CostVision.Web/wwwroot/dist/pages/stores/api.js";
import { loadReceiptsApi, loadLinkedMoneyMovementsApi, loadMoneyMovementCandidatesApi } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/api.js";
import { loadMoneyMovements, loadLinkedReceipts, loadReceiptCandidates, openReceipt, previewBankStatementImport } from "../../CostVision.Web/wwwroot/dist/pages/moneyMovements/api.js";

class ElementStub {
    tagName: string;
    children: ElementStub[] = [];
    parentNode: ElementStub | null = null;
    following: ElementStub | null = null;
    textContent = "";
    value = "";
    checked = false;
    disabled = false;
    hidden = false;
    type = "";
    id = "";
    name = "";
    title = "";
    className = "";
    dataset: Record<string, string> = {};
    attributes = new Map<string, string>();
    listeners = new Map<string, ((event: any) => unknown)[]>();
    classes = new Set<string>();
    classList = {
        add: (...names: string[]) => { names.forEach(name => this.classes.add(name)); },
        remove: (...names: string[]) => { names.forEach(name => this.classes.delete(name)); },
        contains: (name: string) => this.classes.has(name) || this.className.split(" ").includes(name),
        toggle: (name: string, enabled?: boolean) => {
            const add = enabled ?? !this.classes.has(name);
            if (add) this.classes.add(name); else this.classes.delete(name);
            return add;
        }
    };
    style: Record<string, any> = { setProperty() {}, removeProperty() {} };
    constructor(tagName = "div") { this.tagName = tagName; }
    get options(): ElementStub[] { return this.children.filter(child => child.tagName === "option"); }
    get selectedIndex(): number { return this.options.findIndex(option => option.value === this.value); }
    get firstChild(): ElementStub | null { return this.children[0] ?? null; }
    addEventListener(type: string, listener: (event: any) => unknown, options?: { once?: boolean }): void {
        const handlers = this.listeners.get(type) ?? [];
        const handler = options?.once ? (event: any) => {
            this.listeners.set(type, (this.listeners.get(type) ?? []).filter(item => item !== handler));
            return listener(event);
        } : listener;
        handlers.push(handler); this.listeners.set(type, handlers);
    }
    async fire(type: string, event: any = {}): Promise<void> {
        event.target ??= this;
        event.preventDefault ??= () => {};
        event.stopPropagation ??= () => {};
        for (const listener of [...(this.listeners.get(type) ?? [])]) { await listener(event); }
    }
    append(...children: ElementStub[]): void { children.forEach(child => { child.parentNode = this; this.children.push(child); }); }
    appendChild(child: ElementStub): ElementStub { this.append(child); return child; }
    replaceChildren(...children: ElementStub[]): void {
        this.children.forEach(child => { child.parentNode = null; }); this.children = []; this.append(...children);
    }
    removeChild(child: ElementStub): void { this.children = this.children.filter(item => item !== child); child.parentNode = null; }
    remove(): void { this.parentNode?.removeChild(this); }
    after(element: ElementStub): void { this.following = element; }
    setAttribute(name: string, value: string): void {
        this.attributes.set(name, value);
        if (name.startsWith("data-")) this.dataset[this.dataKey(name)] = value;
    }
    getAttribute(name: string): string | null {
        return name.startsWith("data-") ? this.dataset[this.dataKey(name)] ?? null : this.attributes.get(name) ?? null;
    }
    hasAttribute(name: string): boolean { return this.getAttribute(name) !== null; }
    removeAttribute(name: string): void { this.attributes.delete(name); }
    removeEventListener(type: string, listener: (event: any) => unknown): void {
        this.listeners.set(type, (this.listeners.get(type) ?? []).filter(item => item !== listener));
    }
    dataKey(name: string): string { return name.slice(5).replace(/-([a-z])/g, (_, letter) => letter.toUpperCase()); }
    matches(selector: string): boolean {
        if (selector.startsWith("[")) {
            const match = /^\[([^=\]]+)(?:="([^"]*)")?\]/.exec(selector)!;
            return match[2] === undefined ? this.hasAttribute(match[1]) : this.getAttribute(match[1]) === match[2];
        }
        if (selector.startsWith(".")) return selector.slice(1).split(".").every(name => this.classList.contains(name));
        return this.tagName === selector;
    }
    closest(selector: string): ElementStub | null { return this.matches(selector) ? this : this.parentNode?.closest(selector) ?? null; }
    querySelector(selector: string): ElementStub | null { return this.querySelectorAll(selector)[0] ?? null; }
    querySelectorAll(selector: string): ElementStub[] {
        return this.children.flatMap(child => [...(child.matches(selector) ? [child] : []), ...child.querySelectorAll(selector)]);
    }
    contains(element: ElementStub): boolean { return element === this || this.children.some(child => child.contains(element)); }
    focus(): void {}
    blur(): void {}
    getContext(): object { return {}; }
    text(): string { return this.textContent + this.children.map(child => child.text()).join(" "); }
}

function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (error: unknown) => void;
    const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail; });
    return { promise, resolve, reject };
}
const settle = async () => { for (let i = 0; i < 3; i++) await new Promise(resolve => setImmediate(resolve)); };
const response = (data: unknown) => Response.json({ success: true, data });
const page = (items: unknown[] = [], count = items.length, extra = {}) => ({
    items, page: 1, pageSize: 20, totalCount: count, totalPages: Math.max(1, Math.ceil(count / 20)),
    hasPreviousPage: false, hasNextPage: count > 20, ...extra
});
const receipt = (id: string) => ({ id, dateTime: "2026-10-01T12:00:00", totalSum: 100, operationType: 1,
    retailPlace: id, retailPlaceAddress: "", items: [], accounts: [], fiscalDriveNumber: "fn", fiscalDocumentNumber: id, fiscalSign: "fp",
    createdByUserName: "user", createdAtUtc: "2026-10-01", accountId: null, accountName: "",
    moneyMovementCount: 0, availableMoneyMovementCount: 0, moneyMovementsTotalSum: 0 });
const product = (id: string) => ({ id, name: id, adaptiveName: null, receiptCount: 1, averagePrice: 10, averagePriceIsWeighted: false });
const store = (id: string) => ({ id, name: id, displayName: id, address: "address", children: [], receiptCount: 1, totalSpent: 10, incomeSum: 0 });
const movement = (id: string) => ({ id, occurredAt: "2026-10-01T12:00:00", amount: 100, type: 0,
    comment: id, importComment: null, accountId: "account", accountName: "account", accountColorHex: "#123456",
    canEdit: true, canEditComment: true, canEditAccount: true, linkedReceiptCount: 0, availableReceiptCount: 1 });

let sequence = 0;
async function environment(action: (f: ReturnType<typeof makeEnvironment>) => Promise<void>) {
    const names = ["document", "window", "Element", "Node", "HTMLElement", "HTMLInputElement", "HTMLSelectElement",
        "HTMLTextAreaElement", "HTMLButtonElement", "HTMLTableRowElement", "HTMLTableSectionElement", "HTMLCanvasElement", "HTMLFormElement", "MutationObserver", "Chart", "fetch", "alert"];
    const globals = Object.fromEntries(names.map(name => [name, (globalThis as any)[name]]));
    const f = makeEnvironment();
    Object.assign(globalThis, f.globals);
    try { await action(f); }
    finally { await f.windowEvents.fire("pagehide", { persisted: false }); await settle(); f.dispose(); Object.assign(globalThis, globals); }
}
function makeEnvironment() {
    const elements = new Map<string, ElementStub>();
    const docEvents = new ElementStub();
    const windowEvents = new ElementStub();
    const alerts: string[] = [];
    const charts: any[] = [];
    const modalShows: string[] = [];
    const timers = new Set<ReturnType<typeof setTimeout>>();
    const element = (id: string): ElementStub => {
        if (!elements.has(id)) { const item = new ElementStub(); item.id = id; elements.set(id, item); }
        return elements.get(id)!;
    };
    element("productStorePurchasesPageSize").value = "20";
    element("storeReceiptsPageSize").value = "20";
    element("receiptsPageSize").value = "20";
    const table = new ElementStub("table");
    table.setAttribute("data-table-loading-content", "true");
    element("productsTableContainer").append(table);
    const storesTable = new ElementStub("table");
    storesTable.setAttribute("data-table-loading-content", "true");
    element("storesTableContainer").append(storesTable);
    for (const value of ["Expense", "Income"]) {
        const option = new ElementStub("option"); option.value = value; element("moneyMovementType").append(option);
    }
    const document = {
        getElementById: element, createElement: (tag: string) => new ElementStub(tag),
        createTextNode: (text: string) => { const item = new ElementStub("text"); item.textContent = text; return item; },
        querySelector: () => null, querySelectorAll: () => [], cookie: "", activeElement: null,
        addEventListener: docEvents.addEventListener.bind(docEvents),
        removeEventListener: docEvents.removeEventListener.bind(docEvents), body: new ElementStub()
    };
    const window = {
        location: { href: "https://local.invalid/page", search: "" }, history: { replaceState() {} },
        addEventListener: windowEvents.addEventListener.bind(windowEvents),
        removeEventListener: windowEvents.removeEventListener.bind(windowEvents),
        setTimeout(callback: () => void, delay: number) {
            const timer = setTimeout(() => { timers.delete(timer); callback(); }, delay);
            timers.add(timer); return timer;
        },
        clearTimeout,
        bootstrap: { Modal: class {
            modal: ElementStub;
            constructor(modal: ElementStub) { this.modal = modal; }
            static getOrCreateInstance(modal: ElementStub) { return new this(modal); }
            show() { modalShows.push(this.modal.id); }
            hide() { void this.modal.fire("hide.bs.modal"); }
            dispose() {}
        } }
    };
    const globals = { document, window, Element: ElementStub, Node: ElementStub, HTMLElement: ElementStub,
        MutationObserver: class { observe() {} disconnect() {} },
        HTMLInputElement: ElementStub, HTMLSelectElement: ElementStub, HTMLTextAreaElement: ElementStub,
        HTMLButtonElement: ElementStub, HTMLTableRowElement: ElementStub, HTMLTableSectionElement: ElementStub,
        HTMLCanvasElement: ElementStub, HTMLFormElement: ElementStub,
        Chart: class { constructor(_context: object, options: any) { charts.push(options); } destroy() {} },
        alert: (message: string) => { alerts.push(message); },
        fetch: async () => { throw new Error("Unexpected fetch in isolated test"); } };
    async function start(area: string) {
        await import(new URL(`../../CostVision.Web/wwwroot/dist/pages/${area}/index.js?test=${++sequence}`, import.meta.url).href);
        await docEvents.fire("DOMContentLoaded");
        await settle();
        assert.deepEqual(alerts, [], "initialization must complete through the actual entry point");
    }
    return { globals, element, elements, alerts, charts, modalShows, start, windowEvents,
        dispose: () => { timers.forEach(timer => clearTimeout(timer)); } };
}

test("coordinators own independent generations and abort complements a captured snapshot", () => {
    // Arrange
    const firstArea = createRequestCoordinator(), secondArea = createRequestCoordinator();
    const a = firstArea.begin({ id: "a" }), independent = secondArea.begin("independent");

    // Act
    const b = firstArea.begin({ id: "b" });
    firstArea.invalidate();

    // Assert
    assert.equal(a.signal.aborted, true);
    assert.equal(a.isCurrent(), false);
    assert.deepEqual(b.parameters, { id: "b" });
    assert.equal(b.isCurrent(), false);
    assert.equal(independent.isCurrent(), true);
});

test("every scoped read API forwards the same signal while preserving request and response contracts", async () => {
    await environment(async () => {
        // Arrange
        const controller = new AbortController();
        const calls: (() => Promise<unknown>)[] = [
            () => loadIncomeExpenseReportAsync("from", "to", "day", "receipts", ["account"], false, [], null, controller.signal),
            () => getProducts("", 1, 20, "name", "asc", "", controller.signal),
            () => getProductStorePurchases("p", controller.signal),
            () => getStores("", false, 1, 20, "name", "asc", "", controller.signal),
            () => getStoreReceipts("s", null, 1, 20, controller.signal),
            () => openStoreReceipt("r", controller.signal),
            () => loadReceiptsApi("page=1", null, controller.signal),
            () => loadLinkedMoneyMovementsApi("r", null, controller.signal),
            () => loadMoneyMovementCandidatesApi({ receiptId: "r", useTimeWindow: false, timeWindowHours: 1, dateFrom: null, dateTo: null,
                useAmountFilter: true, amountTolerance: 1, excludeLinkedMoneyMovements: true }, null, controller.signal),
            () => loadMoneyMovements(null, "from", "to", "", 1, "", "", controller.signal),
            () => loadLinkedReceipts(null, "m", controller.signal),
            () => loadReceiptCandidates(null, { moneyMovementId: "m", useTimeWindow: false, timeWindowHours: 1, dateFrom: null, dateTo: null,
                useAmountFilter: true, amountTolerance: 1, excludeLinkedReceipts: true }, controller.signal),
            () => openReceipt(null, "r", controller.signal),
            () => previewBankStatementImport(null, "bank", "account", new File(["local"], "statement.pdf"), controller.signal)
        ];
        let forwarded = 0;
        globalThis.fetch = async (_url, options) => {
            assert.equal(options!.signal, controller.signal); forwarded++;
            return new Response("<html>invalid</html>", { status: 500 });
        };

        // Act / Assert
        for (const call of calls) { await assert.rejects(call); }
        assert.equal(forwarded, calls.length);
    });
});

test("dashboard ignores old success and error after filters change, including abort-ignoring fetch", async () => {
    await environment(async f => {
        // Arrange
        let delay = false;
        const pending: { task: ReturnType<typeof deferred<Response>>; signal: AbortSignal; query: URLSearchParams }[] = [];
        globalThis.fetch = async (url, options) => {
            const query = new URL(String(url), "https://local.invalid").searchParams;
            if (query.get("handler") === "Accounts") return response([]);
            if (!delay) return response({ stores: [], points: [] });
            const task = deferred<Response>(); pending.push({ task, signal: options!.signal!, query }); return task.promise;
        };
        await f.start("dashboard");
        delay = true;

        // Act
        f.element("dashboardIncomeExpenseDateFrom").value = "2026-01-01";
        void f.element("dashboardIncomeExpenseDateFrom").fire("change"); await settle();
        f.element("dashboardIncomeExpenseDateFrom").value = "2026-02-01";
        void f.element("dashboardIncomeExpenseDateFrom").fire("change"); await settle();
        pending[1].task.resolve(response({ stores: [], points: [{ periodStart: "2026-10-01", label: "new", incomeSum: 20, expenseSum: 30 }] }));
        await settle();
        pending[0].task.resolve(response({ stores: [], points: [{ periodStart: "2026-10-01", label: "old", incomeSum: 999, expenseSum: 999 }] }));
        await settle();

        // Assert
        assert.equal(pending[0].signal.aborted, true);
        assert.equal(pending[1].query.get("dateFrom"), "2026-02-01");
        assert.deepEqual(f.charts.at(-1).data.labels, ["new"]);
        assert.doesNotMatch(f.element("dashboardIncomeExpenseSummary").text(), /999/);

        // Act
        void f.element("dashboardIncomeExpenseDateFrom").fire("change"); await settle();
        void f.element("dashboardIncomeExpenseDateTo").fire("change"); await settle();
        pending[2].task.reject(new Error("stale network error")); await settle();

        // Assert
        assert.equal(f.element("dashboardIncomeExpenseError").textContent, "");
        pending[3].task.resolve(new Response("invalid", { status: 403 })); await settle();
        assert.notEqual(f.element("dashboardIncomeExpenseError").textContent, "");
    });
});

test("actual dashboard entry shows a contract error for a missing point amount and keeps the last valid chart", async () => {
    await environment(async f => {
        // Arrange
        let valid = true;
        globalThis.fetch = async url => {
            const query = new URL(String(url), "https://local.invalid").searchParams;
            if (query.get("handler") === "Accounts") return response([]);
            const point = valid
                ? { periodStart: "2026-10-14", label: "valid", incomeSum: 12.34, expenseSum: 56.78 }
                : { periodStart: "2026-10-14", label: "invalid", incomeSum: 12.34 };
            return response({ stores: [], points: [point] });
        };
        await f.start("dashboard");
        const chartCount = f.charts.length;
        const summary = f.element("dashboardIncomeExpenseSummary").text();

        // Act
        valid = false;
        await f.element("dashboardIncomeExpenseDateFrom").fire("change");
        await settle();

        // Assert
        assert.equal(f.charts.length, chartCount);
        assert.deepEqual(f.charts.at(-1).data.labels, ["valid"]);
        assert.equal(f.element("dashboardIncomeExpenseSummary").text(), summary);
        assert.notEqual(f.element("dashboardIncomeExpenseError").textContent, "");
    });
});

for (const area of ["products", "stores", "reports/receipts", "moneyMovements"]) {
    test(`actual ${area} list rejects stale success/error without clearing newer loading or totals`, async () => {
        await environment(async f => {
            // Arrange
            let delay = false;
            const pending: { task: ReturnType<typeof deferred<Response>>; signal: AbortSignal }[] = [];
            const data = () => page([], 7, { totalSum: 123.45, incomeSum: 67.89, expenseSum: 123.45 });
            globalThis.fetch = async (url, options) => {
                const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
                if (handler === "Accounts" || handler === "ImportBanks") return response([]);
                if (!delay) return response(data());
                const task = deferred<Response>(); pending.push({ task, signal: options!.signal! }); return task.promise;
            };
            await f.start(area);
            delay = true;
            const buttonId = area === "products" ? "productsApplyFilter" : area === "stores" ? "storesApplyFilter"
                : area === "reports/receipts" ? "applyFilter" : "moneyMovementApplyFilter";
            const countId = area === "products" ? "productsCount" : area === "stores" ? "storesCount"
                : area === "reports/receipts" ? "receiptsCount" : "moneyMovementCount";

            // Act
            void f.element(buttonId).fire("click"); await settle();
            void f.element(buttonId).fire("click"); await settle();
            pending[1].task.resolve(response(data())); await settle();
            pending[0].task.resolve(response(page([], 99, { totalSum: 999, incomeSum: 999, expenseSum: 999 }))); await settle();

            // Assert
            assert.equal(pending[0].signal.aborted, true);
            assert.match(f.element(countId).textContent, /7/);
            assert.doesNotMatch(f.element(countId).textContent, /99/);

            // Act
            void f.element(buttonId).fire("click"); await settle();
            void f.element(buttonId).fire("click"); await settle();
            pending[2].task.reject(new TypeError("stale error")); await settle();

            // Assert
            assert.deepEqual(f.alerts, []);
            const alertId = area === "products" ? "productsAlert" : area === "stores" ? "storesAlert" : "moneyMovementAlert";
            if (area !== "reports/receipts") assert.equal(f.element(alertId).textContent, "");
            if (area === "products" || area === "stores") {
                const container = f.element(area + "TableContainer");
                assert.equal(container.children.at(-1)!.classList.contains("d-none"), false, "old error must leave new loading active");
            }
            pending[3].task.resolve(response(data())); await settle();
            assert.match(f.element(countId).textContent, /7/);
        });
    });
}

test("actual product purchases close/reopen ignores an old reply and stale error", async () => {
    await environment(async f => {
        // Arrange
        const pending: ReturnType<typeof deferred<Response>>[] = [];
        globalThis.fetch = async url => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (handler === "Accounts") return response([]);
            if (handler === "List") return response(page([product("a"), product("b")], 2, { totalSum: 20 }));
            const task = deferred<Response>(); pending.push(task); return task.promise;
        };
        await f.start("products");
        const buttons = f.element("productsTableBody").querySelectorAll('[data-product-store-purchases-button="true"]');

        // Act
        void f.element("productsTableBody").fire("click", { target: buttons[0] }); await settle();
        await f.element("productStorePurchasesModal").fire("hide.bs.modal");
        void f.element("productsTableBody").fire("click", { target: buttons[1] }); await settle();
        pending[1].resolve(response([{ storeName: "new store", storeId: "b", quantity: 1, pricePerUnit: 1 }])); await settle();
        pending[0].reject(new TypeError("old failure")); await settle();

        // Assert
        assert.match(f.element("productStorePurchasesModalTitle").textContent, /b/);
        assert.match(f.element("productStorePurchasesBody").text(), /new store/);
        assert.equal(f.element("productsAlert").textContent, "");
    });
});

test("actual store receipt pages and details reject a previous store after close/reopen", async () => {
    await environment(async f => {
        // Arrange
        const pending: { task: ReturnType<typeof deferred<Response>>; query: URLSearchParams; signal: AbortSignal }[] = [];
        globalThis.fetch = async (url, options) => {
            const query = new URL(String(url), "https://local.invalid").searchParams;
            if (query.get("handler") === "Accounts") return response([]);
            if (query.get("handler") === "List") return response(page([store("a"), store("b")], 2, { totalSum: 20, incomeSum: 0 }));
            const task = deferred<Response>(); pending.push({ task, query, signal: options!.signal! }); return task.promise;
        };
        await f.start("stores");
        const buttons = f.element("storesTableBody").querySelectorAll('[data-store-receipts-button="true"]');

        // Act
        void f.element("storesTableBody").fire("click", { target: buttons[0] }); await settle();
        await f.element("storeReceiptsModal").fire("hide.bs.modal");
        void f.element("storesTableBody").fire("click", { target: buttons[1] }); await settle();
        pending[1].task.resolve(response(page([receipt("new")], 1, { totalSum: 25, incomeSum: 10 }))); await settle();
        pending[0].task.resolve(response(page([receipt("old")], 999, { totalSum: 999, incomeSum: 999 }))); await settle();

        // Assert
        assert.equal(pending[0].signal.aborted, true);
        assert.equal(pending[1].query.get("storeId"), "b");
        assert.match(f.element("storeReceiptsList").text(), /new/);
        assert.doesNotMatch(f.element("storeReceiptsSummary").textContent, /999/);
        assert.equal(f.element("storeReceiptsAlert").textContent, "");

        // Act
        const open = f.element("storeReceiptsList").querySelector('[data-action="open"]')!;
        void f.element("storeReceiptsList").fire("click", { target: open }); await settle();
        await f.element("storeReceiptsModal").fire("hide.bs.modal");
        pending[2].task.resolve(response(receipt("stale details"))); await settle();

        // Assert
        assert.equal(pending[2].signal.aborted, true);
        assert.equal(f.element("receipt-details-header").text(), "");
        assert.equal(f.modalShows.includes("receiptDetailsModal"), false);
    });
});

test("receipt details use their own generation and do not reopen after close", async () => {
    await environment(async f => {
        // Arrange
        const pending: { task: ReturnType<typeof deferred<Response>>; signal: AbortSignal }[] = [];
        globalThis.fetch = async (url, options) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (handler === "Accounts") return response([]);
            if (handler === "ReceiptList") return response(page([receipt("a"), receipt("b")], 2, { totalSum: 200, incomeSum: 0 }));
            const task = deferred<Response>(); pending.push({ task, signal: options!.signal! }); return task.promise;
        };
        await f.start("reports/receipts");
        const buttons = f.element("receipt-list").querySelectorAll('[data-action="open"]');

        // Act
        void f.element("receipt-list").fire("click", { target: buttons[0] }); await settle();
        void f.element("receipt-list").fire("click", { target: buttons[1] }); await settle();
        pending[1].task.resolve(response(receipt("b"))); await settle();
        pending[0].task.reject(new TypeError("old detail error")); await settle();

        // Assert
        assert.equal(pending[0].signal.aborted, true);
        assert.match(f.element("receipt-details-header").text(), /b/);
        assert.deepEqual(f.alerts, []);

        // Act
        void f.element("receipt-list").fire("click", { target: buttons[0] }); await settle();
        await f.element("receiptDetailsModal").fire("hide.bs.modal");
        const shows = f.modalShows.length;
        pending[2].task.resolve(response(receipt("a"))); await settle();

        // Assert
        assert.equal(pending[2].signal.aborted, true);
        assert.equal(f.modalShows.length, shows);
        assert.match(f.element("receipt-details-header").text(), /b/);
    });
});

test("receipt link modal separates linked/candidate loads, suppresses stale failures and captures command IDs", async () => {
    await environment(async f => {
        // Arrange
        let hold = false;
        const pending: { task: ReturnType<typeof deferred<Response>>; handler: string; signal: AbortSignal }[] = [];
        const posts: any[] = [];
        let reloads = 0;
        globalThis.fetch = async (url, options) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler")!;
            if (handler === "LinkMoneyMovement") {
                posts.push(JSON.parse(options!.body as string));
                const task = deferred<Response>(); pending.push({ task, handler, signal: options!.signal! }); return task.promise;
            }
            if (hold) { const task = deferred<Response>(); pending.push({ task, handler, signal: options!.signal! }); return task.promise; }
            if (handler === "LinkedMoneyMovements") return response([]);
            return response(page([{ moneyMovementId: "m", occurredAt: "2026-10-01T12:00:00", amount: 100, comment: "m" }]));
        };
        const controller = initReceiptMoneyMovementsModal({
            getReceipts: () => [receipt("a"), receipt("b")], getForgeryToken: () => null,
            reloadReceiptList: async () => { reloads++; }, formatCurrency: value => String(value)
        });
        await controller.open("a");
        const button = f.element("receiptMoneyMovementCandidates").querySelector('[data-action="link-money-movement"]')!;

        // Act
        const command = f.element("receiptMoneyMovementCandidates").fire("click", { target: button });
        await f.element("receiptMoneyMovementCandidates").fire("click", { target: button });
        await f.element("receiptMoneyMovementsModal").fire("hide.bs.modal");
        await controller.open("b");
        pending[0].task.resolve(Response.json({ success: true })); await command;

        // Assert
        assert.deepEqual(posts, [{ receiptId: "a", moneyMovementId: "m" }]);
        assert.equal(reloads, 0);
        assert.match(f.element("receiptMoneyMovementsInfo").textContent, /b/);
        assert.equal(f.element("receiptMoneyMovementsAlert").textContent, "");

        // Act
        hold = true;
        const old = controller.open("a");
        await settle();
        const next = controller.open("a"); await settle();
        pending[1].task.reject(new TypeError("old linked error"));
        pending[2].task.reject(new TypeError("old candidate error"));
        pending[3].task.resolve(response([]));
        pending[4].task.resolve(response(page([])));
        await Promise.all([old, next]);

        // Assert
        assert.equal(pending[1].signal.aborted, true);
        assert.equal(pending[2].signal.aborted, true);
        assert.equal(f.element("receiptMoneyMovementsAlert").textContent, "");
        assert.deepEqual(f.alerts, []);
    });
});

test("receipt candidate filter change invalidates pending data and old errors before the next reload", async () => {
    await environment(async f => {
        // Arrange
        const task = deferred<Response>();
        let signal: AbortSignal | undefined;
        globalThis.fetch = async (url, options) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (handler === "LinkedMoneyMovements") return response([]);
            signal = options!.signal!;
            return task.promise;
        };
        const controller = initReceiptMoneyMovementsModal({ getReceipts: () => [receipt("a")],
            getForgeryToken: () => null, reloadReceiptList: async () => {}, formatCurrency: String });
        const opening = controller.open("a"); await settle();

        // Act
        f.element("receiptMoneyMovementsAmountTolerance").value = "5";
        await f.element("receiptMoneyMovementsAmountTolerance").fire("input");
        task.reject(new TypeError("obsolete filters"));
        await opening;

        // Assert
        assert.equal(signal!.aborted, true);
        assert.equal(f.element("receiptMoneyMovementsAlert").textContent, "");
        assert.equal(f.element("receiptMoneyMovementCandidates").children.length, 0);
    });
});

test("actual money movement modal discards old linked/candidate/detail results and commands keep the original operation", async () => {
    await environment(async f => {
        // Arrange
        const pending: { task: ReturnType<typeof deferred<Response>>; handler: string; query: URLSearchParams; signal?: AbortSignal }[] = [];
        const posts: any[] = [];
        let hold = false;
        globalThis.fetch = async (url, options) => {
            const query = new URL(String(url), "https://local.invalid").searchParams;
            const handler = query.get("handler")!;
            if (handler === "Accounts") return response([]);
            if (handler === "ImportBanks") return response([]);
            if (handler === "List") return response(page([movement("a"), movement("b")], 2, { incomeSum: 0, expenseSum: 200 }));
            if (handler === "LinkReceipt") {
                posts.push(JSON.parse(options!.body as string));
                const task = deferred<Response>(); pending.push({ task, handler, query }); return task.promise;
            }
            if (hold) {
                const task = deferred<Response>(); pending.push({ task, handler, query, signal: options!.signal! }); return task.promise;
            }
            if (handler === "LinkedReceipts") return response([]);
            return response(page([{ ...receipt("r"), receiptId: "r" }]));
        };
        await f.start("moneyMovements");
        const buttons = f.element("moneyMovementList").querySelectorAll('[data-action="open-receipts"]');
        await f.element("moneyMovementList").fire("click", { target: buttons[0] }); await settle();
        const link = f.element("moneyMovementReceiptCandidates").querySelector('[data-action="link-receipt"]')!;

        // Act
        const command = f.element("moneyMovementReceiptCandidates").fire("click", { target: link });
        await f.element("moneyMovementReceiptCandidates").fire("click", { target: link });
        await f.element("moneyMovementReceiptsModal").fire("hide.bs.modal");
        await f.element("moneyMovementList").fire("click", { target: buttons[1] }); await settle();
        pending[0].task.resolve(Response.json({ success: true })); await command;

        // Assert
        assert.deepEqual(posts, [{ moneyMovementId: "a", receiptId: "r" }]);
        assert.match(f.element("moneyMovementReceiptsInfo").textContent, /b/);
        assert.equal(f.element("moneyMovementReceiptsAlert").textContent, "");

        // Act
        hold = true;
        void f.element("moneyMovementList").fire("click", { target: buttons[0] }); await settle();
        await f.element("moneyMovementReceiptsModal").fire("hide.bs.modal");
        void f.element("moneyMovementList").fire("click", { target: buttons[1] }); await settle();
        pending[1].task.reject(new TypeError("old linked"));
        pending[2].task.resolve(response(page([{ receiptId: "old", totalSum: 999 }])));
        pending[3].task.resolve(response([]));
        pending[4].task.resolve(response(page([{ ...receipt("next"), receiptId: "next" }])));
        await settle();

        // Assert
        assert.equal(pending[1].signal!.aborted, true);
        assert.equal(pending[2].signal!.aborted, true);
        assert.match(f.element("moneyMovementReceiptCandidates").text(), /next/);
        assert.doesNotMatch(f.element("moneyMovementReceiptCandidates").text(), /old/);
        assert.equal(f.element("moneyMovementReceiptsAlert").textContent, "");

        // Act
        const open = f.element("moneyMovementReceiptCandidates").querySelector('[data-action="open-receipt"]')!;
        void f.element("moneyMovementReceiptCandidates").fire("click", { target: open }); await settle();
        await f.element("moneyMovementReceiptsModal").fire("hide.bs.modal");
        pending[5].task.resolve(response(receipt("obsolete"))); await settle();

        // Assert
        assert.equal(pending[5].signal!.aborted, true);
        assert.equal(f.modalShows.includes("moneyMovementReceiptDetailsModal"), false);
    });
});

test("actual receipt deletion captures ID, blocks repeated click and cannot close a reopened confirmation", async () => {
    await environment(async f => {
        // Arrange
        const write = deferred<Response>();
        const posts: any[] = [];
        const nextReceipt = { ...receipt("b"), totalSum: 200 };
        globalThis.fetch = async (url, options) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (handler === "Accounts") return response([]);
            if (handler === "ReceiptList") return response(page([receipt("a"), nextReceipt], 2, { totalSum: 300, incomeSum: 0 }));
            posts.push(JSON.parse(options!.body as string)); return write.promise;
        };
        await f.start("reports/receipts");
        const buttons = f.element("receipt-list").querySelectorAll('[data-action="delete"]');

        // Act
        await f.element("receipt-list").fire("click", { target: buttons[0] });
        const first = f.element("confirmDeleteReceiptBtn").fire("click");
        await f.element("confirmDeleteReceiptBtn").fire("click");
        await f.element("deleteReceiptModal").fire("hide.bs.modal");
        await f.element("receipt-list").fire("click", { target: buttons[1] });
        write.resolve(Response.json({ success: true })); await first;

        // Assert
        assert.deepEqual(posts, [{ receiptId: "a" }]);
        assert.match(f.element("deleteReceiptModalReceiptInfo").text(), /200/);
        assert.equal(f.element("confirmDeleteReceiptBtn").disabled, false);
        assert.equal(f.element("receipt-list").children.length, 2, "old command must not remove another context");
    });
});

test("actual comment command preserves an edited draft, releases its button and cannot affect a reopened same-ID editor", async () => {
    await environment(async f => {
        // Arrange
        const pending: ReturnType<typeof deferred<Response>>[] = [];
        const posts: any[] = [];
        globalThis.fetch = async (url, options) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (handler === "Accounts") return response([account("account")]);
            if (handler === "ImportBanks") return response([]);
            if (handler === "List") return response(page([movement("a")], 1, { incomeSum: 0, expenseSum: 100 }));
            posts.push(JSON.parse(options!.body as string));
            const task = deferred<Response>(); pending.push(task); return task.promise;
        };
        await f.start("moneyMovements");
        const list = f.element("moneyMovementList");
        await list.fire("click", { target: list.querySelector('[data-action="edit-comment"]')! });
        const input = list.querySelector('[data-action="edit-comment-input"]')!;
        const button = list.querySelector('[data-action="save-comment"]')!;
        input.value = "captured";

        // Act
        void list.fire("click", { target: button }); await settle();
        input.value = "changed draft";
        pending[0].reject(new TypeError("lost write")); await settle();

        // Assert
        assert.deepEqual(posts, [{ moneyMovementId: "a", accountId: "account", comment: "captured" }]);
        assert.equal(input.value, "changed draft");
        assert.equal(button.disabled, false);
        assert.match(f.element("moneyMovementAlert").textContent, /Результат записи неизвестен/);

        // Act
        void list.fire("click", { target: button }); await settle();
        await list.fire("click", { target: list.querySelector('[data-action="cancel-comment"]')! });
        await list.fire("click", { target: list.querySelector('[data-action="edit-comment"]')! });
        const reopened = list.querySelector('[data-action="edit-comment-input"]')!;
        reopened.value = "reopened draft";
        const reopenedSave = list.querySelector('[data-action="save-comment"]')!;
        await list.fire("click", { target: reopenedSave }); await settle();
        pending[1].resolve(Response.json({ success: true })); await settle();

        // Assert
        assert.equal(posts.length, 2, "same-ID command stays locked until its original POST completes");
        assert.deepEqual(posts[1], { moneyMovementId: "a", accountId: "account", comment: "changed draft" });
        assert.equal(list.querySelector('[data-action="edit-comment-input"]'), reopened);
        assert.equal(reopened.value, "reopened draft");
        assert.equal(reopenedSave.disabled, false);
    });
});

test("actual create command preserves a changed draft and reports unknown server outcome without retry", async () => {
    await environment(async f => {
        // Arrange
        let hold = false;
        const write = deferred<Response>();
        let posts = 0;
        globalThis.fetch = async (url) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (handler === "Accounts") return response([{ id: "account", name: "account", accessRole: 1 }]);
            if (handler === "ImportBanks") return response([]);
            if (handler === "List") return response(page([], 0, { incomeSum: 0, expenseSum: 0 }));
            posts++;
            return hold ? write.promise : response(movement("created"));
        };
        await f.start("moneyMovements");
        f.element("moneyMovementAmount").value = "100";
        await f.element("moneyMovementAmount").fire("input");
        hold = true;

        // Act
        const command = f.element("moneyMovementCreateForm").fire("submit");
        await f.element("moneyMovementCreateForm").fire("submit");
        f.element("moneyMovementComment").value = "new draft";
        write.reject(new TypeError("lost after commit")); await command;

        // Assert
        assert.equal(posts, 1);
        assert.equal(f.element("moneyMovementComment").value, "new draft");
        assert.match(f.element("moneyMovementAlert").textContent, /Результат записи неизвестен/);
    });
});
for (const area of ["dashboard", "products", "stores", "reports/receipts", "moneyMovements"]) {
    test(`actual ${area} initial account load stops after pagehide even when fetch ignores abort`, async () => {
        await environment(async f => {
            // Arrange
            const task = deferred<Response>();
            const requests: { handler: string; signal: AbortSignal }[] = [];
            globalThis.fetch = async (url, options) => {
                requests.push({ handler: new URL(String(url), "https://local.invalid").searchParams.get("handler")!, signal: options!.signal! });
                return task.promise;
            };
            await f.start(area);

            // Act
            await f.windowEvents.fire("pagehide");
            task.resolve(response([{ id: "obsolete", name: "obsolete", accessRole: 1, colorHex: "#123456" }]));
            await settle();

            // Assert
            assert.equal(requests.length, 1, "obsolete initialization must not start a report, list or bank request");
            assert.equal(requests[0].signal.aborted, true);
            assert.deepEqual(f.alerts, []);
        });
    });
}

for (const area of ["products", "stores"]) {
    test(`actual ${area} naming captures ID/name and a reopened same-ID form stays usable`, async () => {
        await environment(async f => {
            // Arrange
            const first = deferred<Response>();
            const posts: any[] = [];
            globalThis.fetch = async (url, options) => {
                const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
                if (handler === "Accounts") return response([]);
                if (handler === "List") return response(page(area === "products" ? [product("a"), product("b")] : [store("a"), store("b")],
                    2, { totalSum: 20, incomeSum: 0 }));
                posts.push(JSON.parse(options!.body as string));
                return posts.length === 1 ? first.promise : Response.json({ success: true });
            };
            await f.start(area);
            const singular = area === "products" ? "product" : "store";
            const table = f.element(area + "TableBody");
            const buttons = table.querySelectorAll(`[data-${singular}-adaptive-name-button="true"]`);
            await table.fire("click", { target: buttons[0] });
            f.element(singular + "AdaptiveName").value = "captured";

            // Act
            void f.element(singular + "AdaptiveNameForm").fire("submit"); await settle();
            await f.element(singular + "AdaptiveNameModal").fire("hide.bs.modal");
            await table.fire("click", { target: buttons[0] });
            f.element(singular + "AdaptiveName").value = "new draft";
            await f.element(singular + "AdaptiveNameForm").fire("submit"); await settle();

            // Assert
            assert.equal(posts.length, 1);
            assert.equal(f.element(singular + "SaveAdaptiveName").disabled, false);

            // Act
            first.resolve(Response.json({ success: true })); await settle();
            assert.equal(f.element(singular + "AdaptiveName").value, "new draft");
            await f.element(singular + "AdaptiveNameForm").fire("submit"); await settle();

            // Assert
            assert.deepEqual(posts, [
                { [singular + "Id"]: "a", adaptiveName: "captured" },
                { [singular + "Id"]: "a", adaptiveName: "new draft" }
            ]);
            await table.fire("click", { target: buttons[0] });
            assert.equal(f.element(singular + "SaveAdaptiveName").disabled, false);
        });
    });
}

const account = (id: string) => ({ id, name: id, accessRole: 1, canManage: true, colorHex: "#123456" });
const accountReceipt = (id: string) => ({ ...receipt(id), accounts: [
    { ...account("account"), receiptId: id, canEditReceipt: true }
] });

for (const area of ["reports/receipts", "stores", "moneyMovements"]) {
    test(`actual ${area} move captures original ID/target and old completion leaves a reopened form intact`, async () => {
        await environment(async f => {
            // Arrange
            const write = deferred<Response>();
            const posts: any[] = [];
            globalThis.fetch = async (url, options) => {
                const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
                if (handler === "Accounts") return response([account("account"), account("target"), account("other")]);
                if (handler === "ImportBanks") return response([]);
                if (handler === "List") {
                    if (area === "stores") return response(page([store("store")], 1, { totalSum: 200, incomeSum: 0 }));
                    return response(page([movement("a"), movement("b")], 2, { incomeSum: 0, expenseSum: 200 }));
                }
                if (handler === "ReceiptList" || handler === "Receipts") return response(page([accountReceipt("a"), accountReceipt("b")], 2,
                    { totalSum: 200, incomeSum: 0 }));
                posts.push(JSON.parse(options!.body as string)); return write.promise;
            };
            await f.start(area);
            if (area === "stores") {
                const open = f.element("storesTableBody").querySelector('[data-store-receipts-button="true"]')!;
                await f.element("storesTableBody").fire("click", { target: open }); await settle();
            }
            const list = f.element(area === "stores" ? "storeReceiptsList" : area === "moneyMovements" ? "moneyMovementList" : "receipt-list");
            const buttons = list.querySelectorAll('[data-action="edit-account-link"]');
            const target = f.element(area === "moneyMovements" ? "moveMoneyMovementTargetAccount" : "moveReceiptTargetAccount");
            const confirm = f.element(area === "moneyMovements" ? "confirmMoveMoneyMovementAccountButton" : "confirmMoveReceiptAccountBtn");
            const modal = f.element(area === "moneyMovements" ? "moveMoneyMovementAccountModal" : "moveReceiptAccountModal");
            await list.fire("click", { target: buttons[0] }); await settle();
            target.value = "target";

            // Act
            void confirm.fire("click"); await settle();
            await confirm.fire("click"); await settle();
            target.value = "other";
            await modal.fire("hide.bs.modal");
            await list.fire("click", { target: buttons[1] }); await settle();
            write.resolve(Response.json({ success: true })); await settle();

            // Assert
            assert.deepEqual(posts, [{ [area === "moneyMovements" ? "moneyMovementId" : "receiptId"]: "a",
                sourceAccountId: "account", targetAccountId: "target" }]);
            assert.equal(confirm.disabled, false);
            assert.equal(confirm.textContent, "Перенести");
            assert.equal(list.children.length, 2);
        });
    });
}

test("delayed receipt confirmation hidden event cannot clear a reopened confirmation", async () => {
    await environment(async f => {
        // Arrange
        const posts: any[] = [];
        globalThis.fetch = async (url, options) => {
            const handler = new URL(String(url), "https://local.invalid").searchParams.get("handler");
            if (handler === "Accounts") return response([]);
            if (handler === "ReceiptList") return response(page([receipt("a"), receipt("b")], 2, { totalSum: 200, incomeSum: 0 }));
            posts.push(JSON.parse(options!.body as string)); return Response.json({ success: true });
        };
        await f.start("reports/receipts");
        const list = f.element("receipt-list"), buttons = list.querySelectorAll('[data-action="delete"]');
        await list.fire("click", { target: buttons[0] });

        // Act
        await f.element("deleteReceiptModal").fire("hide.bs.modal");
        await list.fire("click", { target: buttons[1] });
        await f.element("deleteReceiptModal").fire("hidden.bs.modal");
        await f.element("confirmDeleteReceiptBtn").fire("click"); await settle();

        // Assert
        assert.deepEqual(posts, [{ receiptId: "b" }]);
        assert.deepEqual(f.alerts, []);
    });
});

test("paging clear isolates old navigation error/finally from a new busy navigation", async () => {
    await environment(async () => {
        // Arrange
        const anchor = new ElementStub(), old = deferred<void>(), next = deferred<void>();
        const errors: unknown[] = [];
        let calls = 0;
        const nav = createPageNavigation(anchor as any, () => (++calls === 1 ? old.promise : next.promise), error => errors.push(error));
        nav.render(page([], 30));
        const button = anchor.following!.children[2];
        await button.fire("click");

        // Act
        nav.clear();
        nav.render(page([], 30));
        await button.fire("click");
        old.reject(new Error("old navigation")); await settle();

        // Assert
        assert.deepEqual(errors, []);
        assert.equal(button.disabled, true);
        await button.fire("click");
        assert.equal(calls, 2);

        // Act / Assert
        next.resolve(); await settle();
        assert.equal(button.disabled, false);
    });
});
for (const area of ["products", "stores"]) {
    test(`actual ${area} edited search invalidates the pending response before apply`, async () => {
        await environment(async f => {
            // Arrange
            let hold = false;
            const task = deferred<Response>();
            let signal: AbortSignal | null = null;
            globalThis.fetch = async (url, options) => {
                if (new URL(String(url), "https://local.invalid").searchParams.get("handler") === "Accounts") return response([]);
                if (!hold) return response(page([], 7, { totalSum: 70, incomeSum: 0 }));
                signal = options!.signal!;
                return task.promise;
            };
            await f.start(area);
            hold = true;
            void f.element(area + "ApplyFilter").fire("click"); await settle();

            // Act
            f.element(area + "Search").value = "changed";
            await f.element(area + "Search").fire("input");
            task.resolve(response(page([], 999, { totalSum: 999, incomeSum: 999 }))); await settle();

            // Assert
            assert.equal(signal!.aborted, true);
            assert.doesNotMatch(f.element(area + "Count").textContent, /999/);
            assert.equal(f.element(area + "Alert").textContent, "");
        });
    });
}

