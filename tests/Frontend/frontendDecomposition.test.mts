import { test } from "node:test";
import assert from "node:assert/strict";
import { getDateRangeByPeriodPreset, formatDateForQuery } from "../../CostVision.Web/wwwroot/dist/shared/dateRange.js";
import { getDateRangeByPeriodPreset as moneyDateRange } from "../../CostVision.Web/wwwroot/dist/pages/moneyMovements/dateRange.js";
import { getDateRangeByPeriodPreset as reportDateRange } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/dateRange.js";
import { getCookie, setCookie, getPageFromQuery, updatePageQuery } from "../../CostVision.Web/wwwroot/dist/shared/preferences.js";
import { getPageRange, createPageButton } from "../../CostVision.Web/wwwroot/dist/shared/paging.js";
import { formatMoneyRub, formatRuNumber } from "../../CostVision.Web/wwwroot/dist/shared/formatters.js";
import { requireElementById, requireInputById } from "../../CostVision.Web/wwwroot/dist/shared/dom.js";
import { loadIncomeExpenseReportAsync } from "../../CostVision.Web/wwwroot/dist/pages/dashboard/api.js";
import { normalizeAvailableAccount, getReceiptAccounts } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/state/accountModel.js";
import { createDashboardState } from "../../CostVision.Web/wwwroot/dist/pages/dashboard/state.js";
import { createStoresSession } from "../../CostVision.Web/wwwroot/dist/pages/stores/state.js";
import { createReceiptsSession } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/state.js";
import { createMoneyMovementsSession } from "../../CostVision.Web/wwwroot/dist/pages/moneyMovements/state.js";
import { createReceiptWorkflow, runReceiptAccountCommand } from "../../CostVision.Web/wwwroot/dist/shared/receipts/workflow.js";
import { createRequestCoordinator } from "../../CostVision.Web/wwwroot/dist/shared/requestCoordinator.js";
import { HttpError } from "../../CostVision.Web/wwwroot/dist/shared/http.js";

function installGlobals(values: Record<string, unknown>): () => void {
    const descriptors = Object.fromEntries(Object.keys(values).map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
    for (const [key, value] of Object.entries(values)) {
        Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
    }
    return () => {
        for (const [key, descriptor] of Object.entries(descriptors)) {
            if (descriptor) { Object.defineProperty(globalThis, key, descriptor); }
            else { Reflect.deleteProperty(globalThis, key); }
        }
    };
}

function deferred<T>() {
    let resolve!: (value: T) => void;
    let reject!: (error: unknown) => void;
    const promise = new Promise<T>((success, failure) => { resolve = success; reject = failure; });
    return { promise, resolve, reject };
}

test("calendar aliases share inclusive Monday/Sunday, leap month and year boundaries without changing now", () => {
    // Arrange
    const now = new Date(2024, 0, 7, 15, 30);
    const originalTime = now.getTime();
    const cases: [string, Date, string, string][] = [
        ["currentDay", now, "2024-01-07", "2024-01-07"],
        ["previousDay", new Date(2024, 0, 1), "2023-12-31", "2023-12-31"],
        ["currentWeek", now, "2024-01-01", "2024-01-07"],
        ["currentWeek", new Date(2024, 0, 8), "2024-01-08", "2024-01-14"],
        ["previousWeek", new Date(2024, 0, 1), "2023-12-25", "2023-12-31"],
        ["previousMonth", new Date(2024, 0, 12), "2023-12-01", "2023-12-31"],
        ["previousMonth", new Date(2024, 2, 1), "2024-02-01", "2024-02-29"],
        ["currentYear", now, "2024-01-01", "2024-12-31"],
        ["currentMonth", new Date(2024, 1, 12), "2024-02-01", "2024-02-29"],
        ["unknown", new Date(2024, 1, 12), "2024-02-01", "2024-02-29"]
    ];
    // Act / Assert
    assert.equal(moneyDateRange, getDateRangeByPeriodPreset);
    assert.equal(reportDateRange, getDateRangeByPeriodPreset);
    for (const [preset, date, from, to] of cases) {
        const range = getDateRangeByPeriodPreset(preset, date);
        assert.equal(formatDateForQuery(range.dateFrom), from);
        assert.equal(formatDateForQuery(range.dateTo), to);
        assert.equal(range.dateFrom.getHours(), 0);
    }
    assert.equal(now.getTime(), originalTime);
});

test("preferences preserve cookie encoding, attributes and malformed value failure", () => {
    // Arrange
    const doc = { cookie: "other=value; stores=Чек%20%26%3B%3D" };
    const restore = installGlobals({ document: doc });
    try {
        // Act / Assert
        assert.equal(getCookie("stores"), "Чек &;=");
        assert.equal(getCookie("missing"), null);
        setCookie("stores", "Кофе &=;");
        assert.equal(doc.cookie, "stores=" + encodeURIComponent("Кофе &=;") + "; path=/; max-age=31536000; samesite=lax");
        doc.cookie = "stores=%ZZ";
        assert.throws(() => getCookie("stores"), URIError);
    } finally { restore(); }
});

test("query page keeps positive integers and preserves other parameters and hash", () => {
    // Arrange
    let replaced: URL | undefined;
    const location = { search: "", href: "https://costvision.test/stores?accountId=A&page=3#details" };
    const restore = installGlobals({ window: { location, history: { replaceState: (_state: unknown, _title: string, url: URL) => { replaced = url; } } } });
    try {
        // Act / Assert
        for (const query of ["", "?page=0", "?page=-1", "?page=1.5", "?page=bad"]) {
            location.search = query;
            assert.equal(getPageFromQuery(), 1);
        }
        location.search = "?page=7&other=value";
        assert.equal(getPageFromQuery(), 7);
        updatePageQuery(4);
        assert.equal(replaced!.searchParams.get("accountId"), "A");
        assert.equal(replaced!.searchParams.get("page"), "4");
        assert.equal(replaced!.hash, "#details");
    } finally { restore(); }
});

test("pagination keeps different catalog and centered windows including empty and last page", () => {
    // Act / Assert
    assert.deepEqual(getPageRange(1, 10), [1, 2, 3, 4, 5]);
    assert.deepEqual(getPageRange(4, 10), [1, 2, 3, 4, 5]);
    assert.deepEqual(getPageRange(5, 10), [3, 4, 5, 6, 7]);
    assert.deepEqual(getPageRange(10, 10), [6, 7, 8, 9, 10]);
    assert.deepEqual(getPageRange(1, 10, "centered"), [1, 2, 3]);
    assert.deepEqual(getPageRange(4, 10, "centered"), [2, 3, 4, 5, 6]);
    assert.deepEqual(getPageRange(10, 10, "centered"), [8, 9, 10]);
    assert.deepEqual(getPageRange(1, 0), []);
    assert.deepEqual(getPageRange(1, 0, "centered"), []);
});

test("shared page button uses DOM text and retains callback and active state", () => {
    // Arrange
    let click: (() => void) | undefined;
    let selectedPage = 0;
    const button = { type: "", className: "", textContent: "", addEventListener: (event: string, callback: () => void) => { assert.equal(event, "click"); click = callback; } };
    const restore = installGlobals({ document: { createElement: (tag: string) => { assert.equal(tag, "button"); return button; } } });
    try {
        // Act
        createPageButton("<5>", () => { selectedPage = 5; }, true);
        click!();
        // Assert
        assert.equal(button.type, "button");
        assert.equal(button.className, "btn btn-primary");
        assert.equal(button.textContent, "<5>");
        assert.equal(selectedPage, 5);
    } finally { restore(); }
});

test("required financial values reject missing and nonfinite numbers while zero and kopecks remain valid", () => {
    // Act / Assert
    for (const value of [undefined, null, "0", "12.50", NaN, Infinity, -Infinity, {}]) {
        assert.throws(() => formatMoneyRub(value), /обязательное/);
        assert.throws(() => formatRuNumber(value), /обязательное/);
    }
    assert.equal(formatMoneyRub(0), (0).toLocaleString("ru-RU", { style: "currency", currency: "RUB" }));
    assert.equal(formatMoneyRub(12.34), (12.34).toLocaleString("ru-RU", { style: "currency", currency: "RUB" }));
});

test("required DOM boundary distinguishes element subclasses and missing nodes", () => {
    // Arrange
    class Element {}
    class Input extends Element {}
    class Button extends Element {}
    const input = new Input();
    const nodes: Record<string, unknown> = { input, button: new Button(), foreign: {} };
    const restore = installGlobals({ HTMLElement: Element, HTMLInputElement: Input, document: { getElementById: (id: string) => nodes[id] ?? null } });
    try {
        // Act / Assert
        assert.equal(requireInputById("input"), input);
        assert.equal(requireElementById("button", Button as unknown as typeof HTMLButtonElement), nodes.button);
        assert.throws(() => requireElementById("input", Button as unknown as typeof HTMLButtonElement), /DOM-тип/);
        assert.throws(() => requireInputById("button"), /input/);
        assert.throws(() => requireElementById("foreign"), /DOM-тип/);
        assert.throws(() => requireElementById("missing"), /обязательный/);
    } finally { restore(); }
});

test("dashboard API rejects absent point totals and preserves genuine zero", async () => {
    // Arrange
    let point: Record<string, unknown> = { label: "День", periodStart: "2026-10-14", incomeSum: 0, expenseSum: 0 };
    const restore = installGlobals({ fetch: async () => new Response(JSON.stringify({ success: true, data: { stores: [], points: [point] } })) });
    const load = () => loadIncomeExpenseReportAsync("2026-10-14", "2026-10-14", "day", "receipts", [], true, [], null);
    try {
        // Act / Assert
        assert.equal((await load()).points[0].expenseSum, 0);
        for (const invalid of [undefined, null, "0", NaN, Infinity]) {
            point = { label: "День", periodStart: "2026-10-14", incomeSum: 0, expenseSum: invalid };
            await assert.rejects(load(), (error: unknown) => error instanceof HttpError && error.kind === "contract");
        }
    } finally { restore(); }
});

test("available account role is required while receipt account nullable role remains optional", () => {
    // Arrange
    const account = { id: "A", name: "Счёт", colorHex: "#123456", canManage: false };
    // Act / Assert
    for (const role of [undefined, null, "0", -1, 3]) {
        assert.throws(() => normalizeAvailableAccount({ ...account, accessRole: role } as never), /роли/);
    }
    for (const role of [0, 1, 2]) {
        assert.equal(normalizeAvailableAccount({ ...account, accessRole: role }).accessRole, role);
    }
    assert.equal(getReceiptAccounts({ accounts: [{ ...account, receiptId: "copy-A", accessRole: null }] } as never)[0].accessRole, null);
});

test("production page sessions keep independent arrays, locks and read generations", () => {
    // Arrange
    const stores = [createStoresSession(), createStoresSession()];
    const reports = [createReceiptsSession(), createReceiptsSession()];
    const money = [createMoneyMovementsSession(), createMoneyMovementsSession()];
    const dashboard = [createDashboardState(), createDashboardState()];
    // Act
    stores[0].receiptWrites.add("copy-A");
    stores[0].data.search = "A";
    reports[0].pageState.receipts.push({ id: "copy-A" } as never);
    money[0].commands.add("A");
    dashboard[0].selectedAccountIds.add("A");
    const storeRead = stores[0].listRequests.begin("A");
    const moneyRead = money[0].linkedRequests.begin("A");
    stores[1].listRequests.invalidate();
    money[0].candidateRequests.invalidate();
    // Assert
    assert.equal(stores[1].receiptWrites.size, 0);
    assert.equal(stores[1].data.search, "");
    assert.equal(reports[1].pageState.receipts.length, 0);
    assert.equal(money[1].commands.size, 0);
    assert.equal(dashboard[1].selectedAccountIds.size, 0);
    assert.equal(storeRead.isCurrent(), true);
    assert.equal(moneyRead.isCurrent(), true);
});

test("shared receipt open keeps captured copy IDs and discards stale success and error", async () => {
    // Arrange
    const pending = [deferred<any>(), deferred<any>(), deferred<any>(), deferred<any>()];
    const ids: string[] = [], shown: string[] = [], errors: string[] = [];
    let index = 0;
    const workflow = createReceiptWorkflow({
        detailRequests: createRequestCoordinator(), writes: new Set(),
        captureReadContext: () => () => true, captureWriteContext: () => () => true,
        open: (id: string) => { ids.push(id); return pending[index++].promise; },
        refresh: async () => { throw new Error("unused"); },
        showDetails: receipt => shown.push(receipt.id), applyRefresh: () => {}, showError: message => errors.push(message)
    });
    // Act
    const a = workflow.open("copy-A");
    const b = workflow.open("copy-B");
    pending[1].resolve({ id: "copy-B" });
    await b;
    pending[0].resolve({ id: "copy-A" });
    await a;
    const stale = workflow.open("copy-A");
    const current = workflow.open("copy-B");
    pending[3].reject(new Error("current error"));
    await current;
    pending[2].reject(new Error("stale error"));
    await stale;
    // Assert
    assert.deepEqual(ids, ["copy-A", "copy-B", "copy-A", "copy-B"]);
    assert.deepEqual(shown, ["copy-B"]);
    assert.equal(errors.length, 1); // current transport failure remains visible
    assert.doesNotMatch(errors[0], /stale error/);
});

test("shared refresh blocks double click and leaves a reopened context untouched", async () => {
    // Arrange
    const pending = deferred<any>();
    const writes = new Set<string>();
    let current = true, sends = 0, applies = 0;
    const button = { disabled: false, textContent: "Обновить" } as HTMLButtonElement;
    const workflow = createReceiptWorkflow({
        detailRequests: createRequestCoordinator(), writes,
        captureReadContext: () => () => current, captureWriteContext: () => () => current,
        open: async () => { throw new Error("unused"); },
        refresh: id => { assert.equal(id, "copy-A"); sends++; return pending.promise; },
        showDetails: () => {}, applyRefresh: () => { applies++; }, showError: () => { throw new Error("unexpected stale error"); }
    });
    // Act
    const first = workflow.refresh("copy-A", {} as HTMLElement, button);
    await workflow.refresh("copy-A", {} as HTMLElement, button);
    current = false;
    pending.reject(new HttpError("failed", "network", null, "/refresh"));
    await first;
    // Assert
    assert.equal(sends, 1);
    assert.equal(applies, 0);
    assert.equal(writes.size, 0);
    assert.equal(button.disabled, true);
});

test("receipt account command captures target and copy, blocks repeats and reports unknown outcome without retry", async () => {
    // Arrange
    const pending = deferred<void>();
    const command = { kind: "move" as const, receiptId: "copy-A", sourceAccountId: "source", targetAccountId: "target-A" };
    const writes = new Set<string>();
    let sends = 0, finalizations = 0;
    const errors: string[] = [];
    let sent: unknown;
    const options = { command, writes, isCurrent: () => true,
        send: async (captured: unknown) => { sends++; sent = captured; await pending.promise; },
        onStart: () => {}, onSuccess: async () => {}, onError: (message: string) => errors.push(message), onFinally: () => { finalizations++; } };
    // Act
    const first = runReceiptAccountCommand(options);
    command.targetAccountId = "target-B";
    await runReceiptAccountCommand(options);
    pending.reject(new HttpError("failed", "network", null, "/move"));
    await first;
    // Assert
    assert.deepEqual(sent, { kind: "move", receiptId: "copy-A", sourceAccountId: "source", targetAccountId: "target-A" });
    assert.equal(sends, 1);
    assert.equal(finalizations, 1);
    assert.equal(writes.size, 0);
    assert.match(errors[0], /неизвестен|неизвестный/i);
});

test("receipt command releases its lock when UI start fails and suppresses stale command errors", async () => {
    // Arrange
    const writes = new Set<string>();
    let sends = 0, errors = 0, finalized = 0;
    // Act
    await runReceiptAccountCommand({ command: { kind: "delete", receiptId: "copy-A" }, writes, isCurrent: () => false,
        send: async () => { sends++; }, onStart: () => { throw new Error("UI failure"); },
        onSuccess: async () => {}, onError: () => { errors++; }, onFinally: () => { finalized++; } });
    // Assert
    assert.equal(sends, 0);
    assert.equal(errors, 0);
    assert.equal(writes.size, 0);
    assert.equal(finalized, 1);
});
