import { test } from "node:test";
import assert from "node:assert/strict";
import { createPageNavigation } from "../../CostVision.Web/wwwroot/dist/shared/paging.js";
import { loadMoneyMovements, loadReceiptCandidates } from "../../CostVision.Web/wwwroot/dist/pages/moneyMovements/api.js";
import { loadMoneyMovementCandidatesApi } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/api.js";
import { renderSummary } from "../../CostVision.Web/wwwroot/dist/pages/moneyMovements/render.js";
import { initReceiptMoneyMovementsModal } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/modals/receiptMoneyMovementsModal.js";

class ElementStub {
    children: ElementStub[] = [];
    following: ElementStub | null = null;
    textContent = "";
    disabled = false;
    hidden = false;
    type = "";
    value = "";
    checked = false;
    style = { removeProperty() {}, pointerEvents: "" };
    classList = { add() {}, toggle() {} };
    handlers: Record<string, (() => void)[]> = {};
    setAttribute(): void {}
    getAttribute(): string | null { return null; }
    hasAttribute(): boolean { return false; }
    closest(): ElementStub | null { return null; }
    append(...children: ElementStub[]): void { this.children.push(...children); }
    replaceChildren(...children: ElementStub[]): void { this.children = children; }
    after(element: ElementStub): void { this.following = element; }
    addEventListener(event: string, handler: () => void): void {
        (this.handlers[event] ??= []).push(handler);
    }
    click(): void {
        if (!this.disabled) {
            for (const handler of this.handlers.click ?? []) { handler(); }
        }
    }
}

function page(items: unknown[] = [], current = 1) {
    return { items, page: current, pageSize: 1, totalCount: 3, totalPages: 3,
        hasPreviousPage: current > 1, hasNextPage: current < 3 };
}

test("navigation uses server pages and count, blocks overlapping clicks and shows all document copies", async () => {
    // Arrange
    const previousDocument = globalThis.document;
    Object.assign(globalThis, { document: { createElement: () => new ElementStub() } });
    try {
        const list = new ElementStub();
        const requested: number[] = [];
        let finish: (() => void) | undefined;
        const navigation = createPageNavigation(list, async current => {
            requested.push(current);
            await new Promise<void>(resolve => { finish = resolve; });
            navigation.render(page([{ receiptId: "copy1" }, { receiptId: "copy2" }], current));
        }, error => { throw error; }, "Документов");
        navigation.render(page([], 1));
        const [previous, label, next] = list.following!.children;

        // Act
        next.click();
        next.click();

        // Assert
        assert.deepEqual(requested, [2]);
        assert.equal(previous.disabled, true);
        assert.equal(next.disabled, true);
        finish!();
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(label.textContent, "Страница 2 из 3 · Документов: 3");
        assert.equal(previous.disabled, false);
        assert.equal(next.disabled, false);
        navigation.render(page([], 3));
        assert.equal(next.disabled, true);
        navigation.clear();
        assert.equal(list.following!.hidden, true);
        assert.equal(previous.disabled, true);
    } finally {
        Object.assign(globalThis, { document: previousDocument });
    }
});

test("actual candidate modal resets pages for changed filters and ignores a previous receipt reply", async () => {
    // Arrange
    const globals = { document: globalThis.document, window: globalThis.window, HTMLElement: globalThis.HTMLElement,
        HTMLInputElement: globalThis.HTMLInputElement, MutationObserver: globalThis.MutationObserver, fetch: globalThis.fetch };
    const elements = new Map<string, ElementStub>();
    function element(id: string): ElementStub {
        if (!elements.has(id)) { elements.set(id, new ElementStub()); }
        return elements.get(id)!;
    }
    const requests: URLSearchParams[] = [];
    let pendingReceipt: string | null = null;
    let finishOld: ((response: Response) => void) | undefined;
    Object.assign(globalThis, {
        document: { getElementById: element, createElement: () => new ElementStub(), addEventListener() {} },
        HTMLElement: ElementStub, HTMLInputElement: ElementStub,
        MutationObserver: class { observe() {} disconnect() {} },
        window: { addEventListener() {}, bootstrap: { Modal: class {
            static getOrCreateInstance() { return new this(); }
            show() {} hide() {}
        } } }
    });
    globalThis.fetch = async url => {
        const query = new URL(String(url), "https://local.invalid").searchParams;
        if (query.get("handler") === "LinkedMoneyMovements") {
            return new Response(JSON.stringify({ success: true, data: [] }));
        }
        requests.push(query);
        if (query.get("receiptId") === pendingReceipt) {
            return await new Promise<Response>(resolve => { finishOld = resolve; });
        }
        return new Response(JSON.stringify({ success: true, data: page([], Number(query.get("page"))) }));
    };
    try {
        const controller = initReceiptMoneyMovementsModal({
            getReceipts: () => [
                { id: "first", dateTime: "2026-01-01", totalSum: 100, retailPlace: "Shop" },
                { id: "second", dateTime: "2026-01-02", totalSum: 200, retailPlace: "Other" }
            ],
            getForgeryToken: () => null, reloadReceiptList: async () => {}, formatCurrency: value => String(value)
        });

        // Act
        await controller.open("first");
        const navigation = element("receiptMoneyMovementCandidates").following!;
        navigation.children[2].click();
        await new Promise(resolve => setImmediate(resolve));
        element("receiptMoneyMovementsAmountTolerance").value = "5";
        element("receiptMoneyMovementsReloadCandidatesButton").click();
        await new Promise(resolve => setImmediate(resolve));

        // Assert
        assert.deepEqual(requests.map(query => query.get("page")), ["1", "2", "1"]);
        assert.equal(requests[2].get("amountTolerance"), "5");
        pendingReceipt = "first";
        const oldOpen = controller.open("first");
        await new Promise(resolve => setImmediate(resolve));
        await controller.open("second");
        assert.equal(navigation.children[1].textContent, "Страница 1 из 3 · Всего: 3");
        finishOld!(new Response(JSON.stringify({ success: true, data: { ...page([], 3), totalCount: 99 } })));
        await oldOpen;
        assert.equal(navigation.children[1].textContent, "Страница 1 из 3 · Всего: 3");
        assert.equal(requests.at(-1)!.get("receiptId"), "second");
        assert.equal(requests.at(-1)!.get("page"), "1");
    } finally {
        Object.assign(globalThis, globals);
    }
});

test("money movement API sends server filters and paging and retains full totals", async () => {
    // Arrange
    const originalFetch = globalThis.fetch;
    const urls: string[] = [];
    const response = { ...page([{ amount: 1 }], 2), incomeSum: 500.25, expenseSum: 1000.75 };
    globalThis.fetch = async url => {
        urls.push(String(url));
        return new Response(JSON.stringify({ success: true, data: response }), { status: 200 });
    };
    try {
        // Act
        const result = await loadMoneyMovements("token", "2026-01-01", "2026-01-31", "account", 2, "coffee", "withReceipts");
        const query = new URL(urls[0], "https://local.invalid").searchParams;

        // Assert
        assert.equal(query.get("page"), "2");
        assert.equal(query.get("pageSize"), "20");
        assert.equal(query.get("search"), "coffee");
        assert.equal(query.get("receiptFilter"), "withReceipts");
        assert.equal(query.get("accountId"), "account");
        assert.deepEqual(result, response);
        assert.equal(result.expenseSum, 1000.75);
    } finally { globalThis.fetch = originalFetch; }
});

test("both candidate APIs return page envelopes without discarding total or copying only one receipt", async () => {
    // Arrange
    const originalFetch = globalThis.fetch;
    const urls: string[] = [];
    const response = page([{ receiptId: "copy1" }, { receiptId: "copy2" }], 2);
    globalThis.fetch = async url => {
        urls.push(String(url));
        return new Response(JSON.stringify({ success: true, data: response }), { status: 200 });
    };
    try {
        // Act
        const receipts = await loadReceiptCandidates(null, {
            moneyMovementId: "movement", page: 2, pageSize: 1, useTimeWindow: false, timeWindowHours: 1,
            dateFrom: "2026-01-01", dateTo: "2026-01-31", useAmountFilter: true, amountTolerance: 1, excludeLinkedReceipts: true
        });
        const movements = await loadMoneyMovementCandidatesApi({
            receiptId: "receipt", page: 2, pageSize: 1, useTimeWindow: false, timeWindowHours: 1,
            dateFrom: null, dateTo: null, useAmountFilter: true, amountTolerance: 1, excludeLinkedMoneyMovements: true
        }, null);

        // Assert
        assert.deepEqual(receipts, response);
        assert.deepEqual(movements, response);
        for (const url of urls) {
            const query = new URL(url, "https://local.invalid").searchParams;
            assert.equal(query.get("page"), "2");
            assert.equal(query.get("pageSize"), "1");
            assert.equal(query.get("amountTolerance"), "1");
        }
        assert.equal(receipts.items.length, 2);
        assert.equal(receipts.totalCount, 3);
    } finally { globalThis.fetch = originalFetch; }
});

test("movement summary displays server totals and an unknown state without page arithmetic", () => {
    // Arrange
    const count = new ElementStub();
    const income = new ElementStub();
    const expense = new ElementStub();
    const response = { ...page([{ amount: 1 }]), incomeSum: 500.25, expenseSum: 1000.75 };

    // Act
    renderSummary(count, income, expense, response);

    // Assert
    assert.equal(count.textContent, "Операций: 3");
    assert.match(income.textContent, /500,25/);
    assert.match(expense.textContent, /1\s000,75/);
    renderSummary(count, income, expense, null);
    assert.equal(count.textContent, "Операций: —");
    assert.equal(income.textContent, "Приход: —");
    assert.equal(expense.textContent, "Расход: —");
});
