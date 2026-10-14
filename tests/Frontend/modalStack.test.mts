import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { getOrCreateBootstrapModal, createBootstrapModal } from "../../CostVision.Web/wwwroot/dist/shared/bootstrap.js";
import { renderHelpTooltip, dismissHelpTooltip } from "../../CostVision.Web/wwwroot/dist/shared/helpTooltip.js";

// Fake public Bootstrap 5.1 event/transition contract; production helpers are imported above.
// No browser, native FocusTrap, rendering, network or application is executed.
class Events {
    listeners = new Map<string, { listener: Function; capture: boolean; once: boolean }[]>();
    addEventListener(name: string, listener: Function, options?: any) {
        const rows = this.listeners.get(name) ?? [];
        if (!rows.some(row => row.listener === listener && row.capture === (options === true))) {
            rows.push({ listener, capture: options === true, once: options?.once === true });
        }
        this.listeners.set(name, rows);
    }
    removeEventListener(name: string, listener: Function) {
        this.listeners.set(name, (this.listeners.get(name) ?? []).filter(row => row.listener !== listener));
    }
    fire(name: string, values: any = {}) {
        const event = { target: this, defaultPrevented: false, stopped: false, ...values,
            preventDefault() { this.defaultPrevented = true; },
            stopPropagation() { this.stopped = true; }, stopImmediatePropagation() { this.stopped = true; } };
        for (const row of [...(this.listeners.get(name) ?? [])].sort((a, b) => Number(b.capture) - Number(a.capture))) {
            if (row.once) this.removeEventListener(name, row.listener);
            row.listener(event);
            if (event.stopped) break;
        }
        return event;
    }
    count() { return [...this.listeners.values()].reduce((count, rows) => count + rows.length, 0); }
}
class Style {
    values = new Map<string, [string, string]>();
    pointerEvents = ""; display = ""; visibility = "visible";
    setProperty(key: string, value: string, priority = "") { this.values.set(key, [value, priority]); }
    getPropertyValue(key: string) { return this.values.get(key)?.[0] ?? ""; }
    getPropertyPriority(key: string) { return this.values.get(key)?.[1] ?? ""; }
    removeProperty(key: string) { this.values.delete(key); }
}
class Element extends Events {
    tagName: string;
    children: Element[] = []; parentNode: Element | null = null;
    attrs = new Map<string, string>(); classes = new Set<string>();
    style = new Style(); id = ""; textContent = ""; type = "";
    disabled = false; tabIndex: number; hidden = false; offsetHeight = 10;
    classList = {
        add: (...names: string[]) => names.forEach(name => this.classes.add(name)),
        remove: (...names: string[]) => names.forEach(name => this.classes.delete(name)),
        contains: (name: string) => this.classes.has(name)
    };
    constructor(tag = "div") { super(); this.tagName = tag; this.tabIndex = ["button", "input", "select", "a"].includes(tag) ? 0 : -1; }
    get isConnected(): boolean { return this === current!.body || !!this.parentNode?.isConnected; }
    append(...nodes: Element[]) { for (const node of nodes) { node.parentNode = this; this.children.push(node); } }
    replaceChildren(...nodes: Element[]) { this.children.forEach(node => node.parentNode = null); this.children = []; this.append(...nodes); }
    remove() { if (this.parentNode) this.parentNode.children = this.parentNode.children.filter(node => node !== this); this.parentNode = null; }
    contains(node: Element): boolean { return this === node || this.children.some(child => child.contains(node)); }
    setAttribute(key: string, value: string) { this.attrs.set(key, value); }
    getAttribute(key: string) { return this.attrs.get(key) ?? null; }
    hasAttribute(key: string) { return this.attrs.has(key); }
    removeAttribute(key: string) { this.attrs.delete(key); }
    matches(selector: string): boolean {
        return selector.split(",").some(part => {
            part = part.trim();
            if (part === ":disabled") return this.disabled || !!this.parentNode?.closest("fieldset:disabled");
            if (part === "fieldset:disabled") return this.tagName === "fieldset" && this.disabled;
            if (part.startsWith(".")) return this.classes.has(part.slice(1));
            if (part === "[hidden]") return this.hidden || this.hasAttribute("hidden");
            const attr = /^\[([^=\]]+)(?:=['"]?([^'"\]]+)['"]?)?\]$/.exec(part);
            if (attr) return attr[2] === undefined ? this.hasAttribute(attr[1]) : this.getAttribute(attr[1]) === attr[2];
            if (part === "a[href]") return this.tagName === "a" && this.hasAttribute("href");
            return part === this.tagName;
        });
    }
    closest(selector: string): Element | null { return this.matches(selector) ? this : this.parentNode?.closest(selector) ?? null; }
    querySelectorAll(selector: string): Element[] { return this.children.flatMap(child => [...(child.matches(selector) ? [child] : []), ...child.querySelectorAll(selector)]); }
    querySelector(selector: string) { return this.querySelectorAll(selector)[0] ?? null; }
    getClientRects() { return this.isConnected && !this.closest("[hidden]") && this.style.display !== "none" ? [{}] : []; }
    getBoundingClientRect() { return { top: 20, left: 20, bottom: 40, width: 100, height: 60 }; }
    focus() {
        const previous = current!.document.activeElement;
        if (previous === this) return;
        previous?.fire("blur"); current!.document.activeElement = this;
        this.fire("focus"); current!.document.fire("focusin", { target: this });
    }
}
let current: ReturnType<typeof fixture> | null = null;
const flush = async () => { for (let i = 0; i < 5; i++) await Promise.resolve(); };
function fixture() {
    const body = new Element("body"), document = new Events() as Events & { body: Element; activeElement: Element | null; querySelectorAll: Function; createElement: Function };
    document.body = body; document.activeElement = null;
    document.querySelectorAll = (selector: string) => body.querySelectorAll(selector);
    document.createElement = (tag: string) => new Element(tag);
    const window = new Events() as any;
    let now = 0, sequence = 0;
    const jobs = new Map<number, { at: number; callback: Function }>();
    window.setTimeout = (callback: Function, delay: number) => { const id = ++sequence; jobs.set(id, { at: now + delay, callback }); return id; };
    window.clearTimeout = (id: number) => jobs.delete(id);
    window.requestAnimationFrame = (callback: Function) => window.setTimeout(callback, 16);
    window.cancelAnimationFrame = window.clearTimeout;
    window.getComputedStyle = (node: Element) => ({ visibility: node.style.visibility, zIndex: node.style.getPropertyValue("z-index") });
    window.innerWidth = 1024; window.innerHeight = 768;
    const instances = new Map<Element, Modal>();
    class Modal {
        node: Element; options: any; phase = "hidden"; disposed = false; disposeCount = 0;
        backdrop: Element | null = null;
        constructor(node: Element, options: any) { this.node = node; this.options = options; instances.set(node, this); }
        static getOrCreateInstance(node: Element, options: any) { return instances.get(node) ?? new this(node, options); }
        show() {
            assert.equal(this.disposed, false);
            if (this.phase !== "hidden") return;
            if (this.node.fire("show.bs.modal").defaultPrevented) return;
            this.phase = "showing";
            body.classList.add("modal-open"); body.style.setProperty("overflow", "hidden");
            body.style.setProperty("padding-right", String(Number(body.style.getPropertyValue("padding-right") || "0") + 15));
            this.backdrop = new Element(); this.backdrop.classList.add("modal-backdrop"); body.append(this.backdrop);
            this.node.classList.add("show"); this.node.style.display = "block";
            this.node.removeAttribute("aria-hidden"); this.node.setAttribute("aria-modal", "true");
        }
        shown() { assert.equal(this.phase, "showing"); assert.equal(this.disposed, false); this.phase = "shown"; this.node.fire("shown.bs.modal"); }
        hide() {
            if (this.phase !== "shown") return; // actual 5.1 ignores hide during show
            if (this.node.fire("hide.bs.modal").defaultPrevented) return;
            this.phase = "hiding"; this.node.classList.remove("show");
        }
        hidden() {
            assert.equal(this.phase, "hiding"); assert.equal(this.disposed, false);
            this.phase = "hidden"; this.node.style.display = "none";
            this.node.setAttribute("aria-hidden", "true"); this.node.removeAttribute("aria-modal");
            this.backdrop?.remove(); body.classList.remove("modal-open");
            body.style.removeProperty("overflow"); body.style.removeProperty("padding-right");
            this.node.fire("hidden.bs.modal");
        }
        dispose() { assert.equal(this.phase, "hidden", "native dispose must wait for transition callbacks"); this.disposed = true; this.disposeCount++; instances.delete(this.node); }
    }
    window.bootstrap = { Modal };
    const observers: any[] = [];
    class Observer {
        callback: Function; connected = false;
        constructor(callback: Function) { this.callback = callback; observers.push(this); }
        observe() { this.connected = true; }
        disconnect() { this.connected = false; }
    }
    const globals = { document, window, Element, HTMLElement: Element, Node: Element, MutationObserver: Observer };
    const descriptors = Object.fromEntries(Object.keys(globals).map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
    async function tick(ms: number) {
        const end = now + ms;
        for (;;) {
            const next = [...jobs].filter(([, job]) => job.at <= end).sort((a, b) => a[1].at - b[1].at)[0];
            if (!next) break;
            jobs.delete(next[0]); now = next[1].at; next[1].callback(); await flush();
        }
        now = end; await flush();
    }
    function modal() {
        const node = new Element(); node.classList.add("modal", "fade"); node.style.display = "none"; node.setAttribute("aria-hidden", "true");
        body.append(node);
        const api = createBootstrapModal(node as any);
        return { node, api, raw: instances.get(node)! };
    }
    return { body, document, window, jobs, instances, observers, modal, tick,
        custom: () => body.querySelectorAll("[data-modal-stack-backdrop]"),
        mutate: () => observers.filter(item => item.connected).forEach(item => item.callback()),
        install() { Object.entries(globals).forEach(([key, value]) => Object.defineProperty(globalThis, key, { configurable: true, writable: true, value })); },
        restore() { for (const [key, descriptor] of Object.entries(descriptors)) { if (descriptor) Object.defineProperty(globalThis, key, descriptor); else Reflect.deleteProperty(globalThis, key); } }
    };
}
async function environment(action: (f: ReturnType<typeof fixture>) => Promise<void>) {
    const f = fixture(); current = f; f.install();
    try { await action(f); }
    finally {
        f.window.fire("pagehide", { persisted: false }); await flush();
        for (const raw of [...f.instances.values()]) {
            if (raw.phase === "showing") raw.shown();
            await flush();
            if (raw.phase === "shown") raw.hide();
            if (raw.phase === "hiding") raw.hidden();
        }
        await flush(); await f.tick(500);
        assert.equal(f.instances.size, 0); assert.equal(f.document.count(), 0);
        assert.equal(f.window.count(), 0); assert.equal(f.jobs.size, 0);
        f.restore(); current = null;
    }
}

test("old backdrop removal cannot be cancelled by another show's frame or retirement", async () => environment(async f => {
    // Arrange
    const first = f.modal(); first.api.show(); await flush(); first.raw.shown(); await f.tick(16);
    const old = f.custom()[0]; assert.equal(old.classList.contains("show"), true);
    // Act
    first.api.hide(); first.raw.hidden(); await flush();
    first.api.show(); await flush(); first.raw.shown();
    const next = f.custom().find(node => node !== old)!;
    await f.tick(16); first.api.hide(); first.raw.hidden(); await flush();
    const third = f.modal(); third.api.show(); await flush(); third.raw.shown();
    await f.tick(150);
    // Assert
    assert.equal(old.isConnected, false); assert.equal(next.isConnected, false);
    assert.equal(f.custom().length, 1); assert.equal(f.custom()[0].classList.contains("show"), true);
}));

test("prevented show/hide is observed after later listeners and no queued update resurrects disposal", async () => environment(async f => {
    // Arrange
    const modal = f.modal(); const prevent = (event: any) => event.preventDefault();
    modal.node.addEventListener("show.bs.modal", prevent);
    // Act / Assert
    modal.api.show(); await flush(); await f.tick(100);
    assert.equal(f.custom().length, 0); assert.equal(f.jobs.size, 0);
    modal.node.removeEventListener("show.bs.modal", prevent);
    modal.api.show(); await flush(); modal.raw.shown();
    modal.node.addEventListener("hide.bs.modal", prevent);
    modal.api.hide(); await flush();
    assert.equal(modal.raw.phase, "shown"); assert.equal(modal.node.style.pointerEvents, "auto");
    modal.api.dispose(); await flush(); assert.equal(modal.raw.disposed, false);
    modal.node.removeEventListener("hide.bs.modal", prevent);
    modal.api.hide(); modal.raw.hidden(); await flush(); await f.tick(200);
    assert.equal(modal.raw.disposeCount, 1); assert.equal(f.custom().length, 0);
    assert.equal(modal.node.listeners.get("shown.bs.modal")!.length, 0);
}));

test("hide during show waits for shown; rapid hide/show reopens only after hidden", async () => environment(async f => {
    // Arrange
    const modal = f.modal(); modal.api.show(); modal.api.hide(); await flush();
    // Act / Assert
    assert.equal(modal.raw.phase, "showing"); modal.raw.shown(); await flush();
    assert.equal(modal.raw.phase, "hiding");
    modal.api.show(); modal.raw.hidden(); await flush();
    assert.equal(modal.raw.phase, "showing"); modal.raw.shown(); await flush();
    assert.equal(getOrCreateBootstrapModal(modal.node as any), modal.api);
    assert.equal(f.instances.size, 1);
}));

test("dispose during show leaves native callbacks alive until hidden and can bind a fresh owner", async () => environment(async f => {
    // Arrange
    const modal = f.modal(); modal.api.show(); modal.api.dispose(); await flush();
    // Act
    assert.equal(modal.raw.disposed, false); modal.raw.shown(); await flush();
    assert.equal(modal.raw.phase, "hiding"); modal.raw.hidden(); await flush(); await f.tick(200);
    // Assert
    assert.equal(modal.raw.disposeCount, 1); assert.equal(f.custom().length, 0);
    const fresh = createBootstrapModal(modal.node as any); assert.notEqual(fresh, modal.api); fresh.dispose();
}));

test("nested stack keeps top focus/Tab/click/Escape and restores lower opener and root navigation", async () => environment(async f => {
    // Arrange
    const root = new Element("a"); root.setAttribute("href", "/"); f.body.append(root); root.focus();
    const lower = f.modal(), upper = f.modal();
    const open = new Element("button"), next = new Element("input"); lower.node.append(open, next);
    lower.api.show(); await flush(); lower.raw.shown(); open.focus();
    const disabled = new Element("button"); disabled.disabled = true;
    const hidden = new Element("button"); hidden.hidden = true;
    const first = new Element("input"), last = new Element("button"); upper.node.append(disabled, hidden, first, last);
    // Act
    upper.api.show(); await flush(); upper.raw.shown(); await f.tick(16);
    // Assert
    assert.deepEqual(upper.raw.options, { focus: false, keyboard: false });
    assert.equal(f.document.activeElement, first); assert.equal(lower.node.hasAttribute("inert"), true);
    const backdrop = f.custom().find(node => node.classList.contains("show"))!;
    assert.equal(backdrop.style.getPropertyValue("z-index"), "2025");
    assert.equal(lower.node.style.getPropertyValue("z-index"), "2010");
    assert.equal(upper.node.style.getPropertyValue("z-index"), "2040");
    f.document.fire("keydown", { key: "Tab", target: first }); assert.equal(f.document.activeElement, last);
    f.document.fire("keydown", { key: "Tab", target: last }); assert.equal(f.document.activeElement, first);
    f.document.fire("keydown", { key: "Tab", shiftKey: true, target: first }); assert.equal(f.document.activeElement, last);
    assert.equal(f.document.fire("click", { target: open }).defaultPrevented, true);
    root.focus(); assert.equal(f.document.activeElement, last);
    f.document.fire("keydown", { key: "Escape", target: last }); await flush();
    assert.equal(upper.raw.phase, "hiding"); assert.equal(lower.raw.phase, "shown");
    upper.raw.hidden(); await flush(); assert.equal(f.document.activeElement, open);
    assert.equal(lower.node.hasAttribute("inert"), false); assert.equal(f.body.classList.contains("modal-open"), true);
    assert.equal(f.body.style.getPropertyValue("padding-right"), "15");
    lower.api.hide(); lower.raw.hidden(); await flush(); assert.equal(f.document.activeElement, root);
    assert.equal(f.body.style.getPropertyValue("overflow"), "");
    assert.equal(f.document.fire("keydown", { key: "Tab", target: root }).defaultPrevented, false);
}));

test("out of order shown never promotes a lower modal", async () => environment(async f => {
    // Arrange
    const lower = f.modal(), upper = f.modal(); const button = new Element("button"); lower.node.append(button);
    lower.api.show(); await flush(); upper.api.show(); await flush();
    // Act
    upper.raw.shown(); lower.raw.shown(); await flush();
    // Assert
    assert.equal(lower.node.style.pointerEvents, "none"); assert.equal(upper.node.style.pointerEvents, "auto");
    upper.api.hide(); upper.raw.hidden(); await flush(); assert.equal(f.document.activeElement, button);
}));

test("empty modal traps Tab and a disabled/removed opener falls back to live navigation", async () => environment(async f => {
    // Arrange
    const root = new Element("a"); root.setAttribute("href", "/"); f.body.append(root);
    const opener = new Element("button"); f.body.append(opener); opener.focus();
    const modal = f.modal(), fieldset = new Element("fieldset"), input = new Element("input");
    fieldset.disabled = true; fieldset.append(input); modal.node.append(fieldset);
    // Act
    modal.api.show(); await flush(); modal.raw.shown();
    f.document.fire("keydown", { key: "Tab", target: modal.node });
    // Assert
    assert.equal(f.document.activeElement, modal.node);
    opener.disabled = true; opener.remove();
    modal.api.hide(); modal.raw.hidden(); await flush();
    assert.equal(f.document.activeElement, root);
}));

test("pagehide preserves bfcache owners but final teardown removes subscriptions after transitions", async () => environment(async f => {
    // Arrange
    const modal = f.modal(); modal.api.show(); await flush(); modal.raw.shown();
    // Act / Assert
    f.window.fire("pagehide", { persisted: true }); assert.equal(modal.raw.phase, "shown");
    f.window.fire("pagehide", { persisted: false }); await flush(); assert.equal(modal.raw.phase, "hiding");
    modal.raw.hidden(); await flush(); await f.tick(150);
    assert.equal(modal.raw.disposeCount, 1); assert.equal(f.document.count(), 0); assert.equal(f.window.count(), 0);
}));

test("a cancelled show with synchronous disposal invalidates its pending stack job", async () => environment(async f => {
    // Arrange
    const modal = f.modal();
    const cancel = (event: any) => { event.preventDefault(); modal.api.dispose(); };
    modal.node.addEventListener("show.bs.modal", cancel);
    // Act
    modal.api.show(); await flush(); await f.tick(100);
    // Assert
    assert.equal(modal.raw.disposeCount, 1); assert.equal(f.custom().length, 0); assert.equal(f.jobs.size, 0);
    modal.node.removeEventListener("show.bs.modal", cancel);
}));

test("tooltip focus/Enter click/Escape/blur preserve descriptions and cleanup rerender/removal/pagehide", async () => environment(async f => {
    // Arrange
    const host = new Element(); f.body.append(host);
    const trigger = renderHelpTooltip(host as any, { title: "Помощь", text: ["<script>literal</script>", "Вторая строка"] }) as any as Element;
    trigger.setAttribute("aria-describedby", "existing-description");
    // Act
    trigger.focus(); const popup = f.body.querySelectorAll("[role='tooltip']")[0];
    // Assert
    assert.ok(popup); assert.match(trigger.getAttribute("aria-describedby")!, /^existing-description help-tooltip-/);
    assert.equal(trigger.getAttribute("aria-controls"), popup.id); assert.equal(trigger.getAttribute("aria-expanded"), "true");
    assert.equal(popup.children[1].children[0].textContent, "<script>literal</script>");
    trigger.fire("click"); f.document.fire("keydown", { key: "Escape", target: trigger });
    assert.equal(popup.isConnected, false); assert.equal(f.document.activeElement, trigger);
    assert.equal(trigger.getAttribute("aria-describedby"), "existing-description");
    trigger.fire("mouseenter"); await f.tick(499); assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 0);
    trigger.fire("mouseleave"); assert.equal(f.jobs.size, 0);
    trigger.fire("mouseenter");
    renderHelpTooltip(host as any, { title: "Новая помощь", text: "Новый текст" }); await f.tick(500);
    assert.equal(trigger.count(), 0); assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 0);
    const fresh = host.children[0]; fresh.focus();
    assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 1);
    const outside = new Element("button"); f.body.append(outside); outside.focus();
    assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 0);
    fresh.focus(); assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 1);
    host.remove(); f.mutate();
    assert.equal(fresh.count(), 0); assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 0);
    assert.equal(dismissHelpTooltip(), false);
}));

test("actual Russian layout and page modal/form markup has resolvable accessible identities", () => {
    // Arrange
    const read = (path: string) => readFileSync(new URL(`../../CostVision.Web/Pages/${path}`, import.meta.url), "utf8");
    // Act / Assert
    assert.match(read("Shared/_Layout.cshtml"), /<html lang="ru">/);
    for (const path of ["Receipts.cshtml", "Stores.cshtml", "MoneyMovements.cshtml", "AddReceipt.cshtml"]) {
        const source = read(path);
        const ids = [...source.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
        assert.equal(new Set(ids).size, ids.length, `${path}: duplicate id`);
        for (const modal of source.matchAll(/<div class="modal fade"[^>]+>/g)) {
            const labelledBy = /aria-labelledby="([^"]+)"/.exec(modal[0]);
            assert.ok(labelledBy, `${path}: modal must reference its title`);
            assert.ok(ids.includes(labelledBy[1]), `${path}: missing modal title ${labelledBy[1]}`);
        }
        for (const field of source.matchAll(/<(?:input|select|textarea)\b[^>]*>/g)) {
            assert.match(field[0], /\bid="[^"]+"/, `${path}: missing field id`);
            assert.match(field[0], /\bname="[^"]+"/, `${path}: missing field name`);
        }
        for (const label of source.matchAll(/<label\b[^>]*for="([^"]+)"/g)) {
            assert.ok(ids.includes(label[1]), `${path}: orphan label ${label[1]}`);
        }
    }
});

test("Escape closes a tooltip before its top modal and hidden cancels hover timers", async () => environment(async f => {
    // Arrange
    const modal = f.modal(), host = new Element(); modal.node.append(host);
    const trigger = renderHelpTooltip(host as any, { title: "Помощь", text: "Описание" }) as any as Element;
    modal.api.show(); await flush(); modal.raw.shown(); trigger.focus();
    // Act
    f.document.fire("keydown", { key: "Escape", target: trigger });
    // Assert
    assert.equal(modal.raw.phase, "shown"); assert.equal(trigger.getAttribute("aria-expanded"), "false");
    trigger.fire("mouseenter"); modal.api.hide(); modal.raw.hidden(); await flush(); await f.tick(600);
    assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 0);
}));

test("hover tooltip stays open while the pointer enters its popup and cleans a detached pending trigger", async () => environment(async f => {
    // Arrange
    const host = new Element(); f.body.append(host);
    const trigger = renderHelpTooltip(host as any, { title: "Помощь", text: "Описание" }) as any as Element;
    // Act
    trigger.fire("mouseenter"); await f.tick(500);
    const popup = f.body.querySelectorAll("[role='tooltip']")[0];
    trigger.fire("mouseleave"); popup.fire("mouseenter"); await f.tick(150);
    // Assert
    assert.equal(popup.isConnected, true);
    popup.fire("mouseleave"); await f.tick(100); assert.equal(popup.isConnected, false);
    trigger.fire("mouseenter"); trigger.remove(); f.mutate(); await f.tick(500);
    assert.equal(trigger.count(), 0); assert.equal(f.body.querySelectorAll("[role='tooltip']").length, 0);
    assert.equal(f.observers.some(item => item.connected), false);
}));
