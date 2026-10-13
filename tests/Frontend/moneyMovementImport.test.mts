import { test } from "node:test";
import assert from "node:assert/strict";
import { initMoneyMovementImportModalController } from "../../CostVision.Web/wwwroot/dist/pages/moneyMovements/importModal.js";

class ElementStub {
    children: ElementStub[] = [];
    textContent = "";
    value = "";
    disabled = false;
    checked = false;
    indeterminate = false;
    files: File[] = [];
    attributes = new Map<string, string>();
    listeners = new Map<string, ((event: unknown) => unknown)[]>();
    style = { setProperty() {} };
    classList = { add() {}, toggle() {} };

    addEventListener(type: string, listener: (event: unknown) => unknown): void {
        const listeners = this.listeners.get(type) ?? [];
        listeners.push(listener);
        this.listeners.set(type, listeners);
    }
    async fire(type: string, event: unknown = {}): Promise<void> {
        for (const listener of this.listeners.get(type) ?? []) {
            await listener(event);
        }
    }
    append(...elements: ElementStub[]): void { this.children.push(...elements); }
    replaceChildren(): void { this.children = []; }
    hasAttribute(name: string): boolean { return this.attributes.has(name); }
    setAttribute(name: string, value: string): void { this.attributes.set(name, value); }
    getAttribute(name: string): string | null { return this.attributes.get(name) ?? null; }
    click(): void {}
    allText(): string { return this.textContent + this.children.map(child => child.allText()).join(" "); }
}

function row(id: string, sourceLineNumber: number, duplicate = false) {
    return { clientRowId: id, occurredAt: "2026-10-13T12:00:00", processedAt: "2026-10-13T12:00:00",
        amount: 100, type: 0, comment: id, importComment: id, sourceLineNumber,
        isDuplicate: duplicate, replaceDuplicate: duplicate };
}

function fixture(rows = [row("first", 10), row("second", 20), row("third", 30)]) {
    const ui = Object.fromEntries(["importDropzone", "importFileInput", "importPreviewButton", "importSaveButton",
        "importToggleDuplicateReplacementsInput", "importSkipDuplicatesInput", "importToggleDuplicatesButton",
        "importPreview", "importModal", "importFileName", "importBankSelect", "importAccountSelect",
        "importAlert", "importErrors", "importSummary"].map(name => [name, new ElementStub()]));
    ui.importAccountSelect.value = "account";
    const state = { importRows: rows, importErrors: [], hideDuplicateImportRows: false, selectedImportFile: null };
    let hides = 0;
    let reloads = 0;
    const controller = initMoneyMovementImportModalController({
        ui, state, importModal: { hide() { hides++; } }, getForgeryToken: () => "token",
        reloadMovements: async () => { reloads++; }
    });
    controller.renderState();
    return { ui, state, controller, hides: () => hides, reloads: () => reloads };
}

async function withEnvironment(action: () => Promise<void>): Promise<void> {
    const previousFetch = globalThis.fetch;
    const previousDocument = globalThis.document;
    Object.assign(globalThis, { document: { createElement: () => new ElementStub() } });
    try { await action(); }
    finally { Object.assign(globalThis, { fetch: previousFetch, document: previousDocument }); }
}

test("partial import displays counts, maps submitted errors and retries only rejected rows", async () => {
    await withEnvironment(async () => {
        const f = fixture();
        const requests: Record<string, unknown>[] = [];
        globalThis.fetch = async (_url, options) => {
            requests.push(JSON.parse(options!.body as string));
            return Response.json({ success: true, data: requests.length === 1
                ? { createdCount: 1, updatedCount: 1, errorCount: 1,
                    errors: [{ lineNumber: 2, message: "Исправьте комментарий", rawText: "second" }] }
                : { createdCount: 1, updatedCount: 0, errorCount: 0, errors: [] } });
        };
        await f.ui.importSaveButton.fire("click");
        assert.equal(f.hides(), 0);
        assert.equal(f.reloads(), 1);
        assert.match(f.ui.importSummary.textContent, /Создано: 1.*Обновлено: 1.*Ошибок: 1/);
        assert.match(f.ui.importErrors.allText(), /Строка 20: Исправьте комментарий/);
        assert.deepEqual(f.state.importRows.map(row => row.clientRowId), ["second"]);
        assert.equal(f.ui.importSaveButton.disabled, false);
        f.state.importRows[0].comment = "fixed";
        await f.ui.importSaveButton.fire("click");
        assert.deepEqual((requests[1].rows as { comment: string }[]).map(row => row.comment), ["fixed"]);
        assert.equal(f.state.importRows.length, 0);
        assert.equal(f.ui.importSaveButton.disabled, true);
        await f.ui.importSaveButton.fire("click");
        assert.equal(requests.length, 2);
    });
});

test("in-flight import blocks a second send and prevents modal close", async () => {
    await withEnvironment(async () => {
        const f = fixture();
        let resolve!: (response: Response) => void;
        let requests = 0;
        globalThis.fetch = async () => {
            requests++;
            return await new Promise<Response>(done => { resolve = done; });
        };
        const first = f.ui.importSaveButton.fire("click");
        await f.ui.importSaveButton.fire("click");
        let prevented = false;
        await f.ui.importModal.fire("hide.bs.modal", { preventDefault() { prevented = true; } });
        assert.equal(prevented, true);
        assert.equal(requests, 1);
        assert.equal(f.ui.importAccountSelect.disabled, true);
        resolve(Response.json({ success: true, data: { createdCount: 3, updatedCount: 0, errorCount: 0, errors: [] } }));
        await first;
        assert.equal(f.state.importRows.length, 0);
        assert.equal(f.ui.importSaveButton.disabled, true);
    });
});

test("Conflict retains editable rows and displays the server message", async () => {
    await withEnvironment(async () => {
        const f = fixture();
        globalThis.fetch = async () => Response.json({ success: false, message: "Конкурентный импорт отменён" }, { status: 409 });
        await f.ui.importSaveButton.fire("click");
        assert.equal(f.state.importRows.length, 3);
        assert.equal(f.ui.importAlert.textContent, "Конкурентный импорт отменён");
        assert.equal(f.ui.importSaveButton.disabled, false);
        assert.equal(f.hides(), 0);
        assert.equal(f.reloads(), 0);
    });
});

test("skipped duplicates stay unsent and server line numbers use submitted order", async () => {
    await withEnvironment(async () => {
        const f = fixture([row("skip", 5, true), row("ok", 8), row("bad", 15)]);
        f.ui.importSkipDuplicatesInput.checked = true;
        let sent: { rows: { importComment: string }[] } | null = null;
        globalThis.fetch = async (_url, options) => {
            sent = JSON.parse(options!.body as string);
            return Response.json({ success: true, data: { createdCount: 1, updatedCount: 0, errorCount: 1,
                errors: [{ lineNumber: 2, message: "bad", rawText: "bad" }] } });
        };
        await f.ui.importSaveButton.fire("click");
        assert.deepEqual(sent!.rows.map(row => row.importComment), ["ok", "bad"]);
        assert.deepEqual(f.state.importRows.map(row => row.clientRowId), ["skip", "bad"]);
        assert.match(f.ui.importErrors.allText(), /Строка 15: bad/);
    });
});

test("reload failure cannot reintroduce accepted rows", async () => {
    await withEnvironment(async () => {
        const f = fixture();
        globalThis.fetch = async () => Response.json({ success: true, data: { createdCount: 3, updatedCount: 0, errorCount: 0, errors: [] } });
        // Контроллер с отказом refresh списка использует тот же state/UI.
        const ui = { ...f.ui, importSaveButton: new ElementStub(), importModal: new ElementStub() };
        initMoneyMovementImportModalController({ ui, state: f.state, importModal: { hide() {} },
            getForgeryToken: () => null, reloadMovements: async () => { throw new Error("list failed"); } }).renderState();
        await ui.importSaveButton.fire("click");
        assert.equal(f.state.importRows.length, 0);
        assert.equal(ui.importSaveButton.disabled, true);
        assert.equal(ui.importAlert.textContent, "list failed");
        assert.match(ui.importSummary.textContent, /Создано: 3/);
    });
});

test("a duplicate discovered on save can be explicitly corrected in the remaining window", async () => {
    await withEnvironment(async () => {
        const f = fixture();
        let count = 0;
        let sent: { rows: { replaceDuplicate: boolean; duplicateMoneyMovementId: string }[] } | null = null;
        globalThis.fetch = async (_url, options) => {
            count++;
            sent = JSON.parse(options!.body as string);
            return Response.json({ success: true, data: count === 1
                ? { createdCount: 2, updatedCount: 0, errorCount: 1, errors: [{ lineNumber: 2, rawText: "second",
                    message: "Повтор", canReplaceDuplicate: true, duplicateMoneyMovementId: "existing" }] }
                : { createdCount: 0, updatedCount: 1, errorCount: 0, errors: [] } });
        };
        await f.ui.importSaveButton.fire("click");
        assert.equal(f.ui.importSaveButton.disabled, true);
        assert.equal(f.state.importRows[0].isDuplicate, true);
        assert.match(f.ui.importPreview.allText(), /обновить пользовательский комментарий/);
        f.ui.importToggleDuplicateReplacementsInput.checked = true;
        await f.ui.importToggleDuplicateReplacementsInput.fire("change");
        assert.equal(f.ui.importSaveButton.disabled, false);
        await f.ui.importSaveButton.fire("click");
        assert.equal(sent!.rows.length, 1);
        assert.equal(sent!.rows[0].replaceDuplicate, true);
        assert.equal(sent!.rows[0].duplicateMoneyMovementId, "existing");
        assert.equal(f.state.importRows.length, 0);
    });
});

test("account changed after preview requires a new preview", async () => {
    await withEnvironment(async () => {
        const f = fixture();
        f.state.selectedImportFile = new File(["local"], "statement.pdf");
        f.ui.importBankSelect.value = "test";
        let requests = 0;
        globalThis.fetch = async () => {
            requests++;
            return Response.json({ success: true, data: { accountId: "account", bankId: "test",
                rows: [row("new", 10)], errors: [] } });
        };
        await f.ui.importPreviewButton.fire("click");
        f.ui.importAccountSelect.value = "other";
        await f.ui.importSaveButton.fire("click");
        assert.equal(requests, 1);
        assert.equal(f.state.importRows.length, 1);
        assert.match(f.ui.importAlert.textContent, /Счёт изменён/);
    });
});

test("pending preview cannot overlap save and restore previously accepted rows", async () => {
    await withEnvironment(async () => {
        const f = fixture();
        f.state.selectedImportFile = new File(["local"], "statement.pdf");
        f.ui.importBankSelect.value = "test";
        let resolve!: (response: Response) => void;
        let requests = 0;
        globalThis.fetch = async () => {
            requests++;
            return await new Promise<Response>(done => { resolve = done; });
        };
        const preview = f.ui.importPreviewButton.fire("click");
        assert.equal(f.ui.importSaveButton.disabled, true);
        await f.ui.importSaveButton.fire("click");
        assert.equal(requests, 1);
        resolve(Response.json({ success: true, data: { bankId: "test", accountId: "account",
            rows: [row("new", 10)], errors: [] } }));
        await preview;
        assert.deepEqual(f.state.importRows.map(row => row.clientRowId), ["new"]);
        assert.equal(f.ui.importSaveButton.disabled, false);
    });
});
