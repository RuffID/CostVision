import { dismissHelpTooltip } from "./helpTooltip.js";

export interface BootstrapModal {
    show(): void;
    hide(): void;
    // Disposal waits for hidden; a prevented hide remains authoritative.
    dispose(): void;
}

interface BootstrapModalOptions {
    focus: boolean;
    keyboard: boolean;
}

interface BootstrapModalConstructor {
    new (element: Element, options?: BootstrapModalOptions): BootstrapModal;
    getOrCreateInstance(element: Element, options?: BootstrapModalOptions): BootstrapModal;
}

interface BootstrapApi {
    Modal: BootstrapModalConstructor;
}

interface ModalOwner {
    element: HTMLElement;
    instance: BootstrapModal;
    api: BootstrapModal;
    phase: "hidden" | "showing" | "shown" | "hiding";
    revision: number;
    hideAfterShown: boolean;
    reopen: boolean;
    disposing: boolean;
    disposed: boolean;
    opener: HTMLElement | null;
    lastFocus: HTMLElement | null;
    inert: boolean;
    unsubscribe: () => void;
}

interface BackdropOwner {
    element: HTMLElement;
    frame: number | null;
    top: ModalOwner;
}

interface ScrollStyle {
    element: HTMLElement;
    property: string;
    value: string;
    priority: string;
}

const MODAL_Z_INDEX = 2010;
const MODAL_STACK_STEP = 30;
const MODAL_BACKDROP_TRANSITION_MS = 150;
const owners = new Map<Element, ModalOwner>();
let modalStack: ModalOwner[] = [];
let modalBackdrop: BackdropOwner | null = null;
let handlersBound = false;
let initialScrollStyles: ScrollStyle[] | null = null;
let lockedScrollStyles: ScrollStyle[] | null = null;

declare global {
    interface Window {
        bootstrap?: BootstrapApi;
    }
}

export function createBootstrapModal(element: Element): BootstrapModal {
    return getOrCreateBootstrapModal(element);
}

export function getOrCreateBootstrapModal(element: Element): BootstrapModal {
    if (!window.bootstrap?.Modal) {
        throw new Error("Bootstrap Modal API недоступен.");
    }
    if (!(element instanceof HTMLElement)) {
        throw new Error("Модальное окно должно быть HTMLElement.");
    }
    const existing = owners.get(element);
    if (existing) {
        return existing.api;
    }
    // Bootstrap 5.1.0 public config; no lower native FocusTrap can steal focus.
    const instance = window.bootstrap.Modal.getOrCreateInstance(element, { focus: false, keyboard: false });
    const owner: ModalOwner = {
        element, instance, api: {
            show: () => {
                if (owner.disposing || owner.disposed) {
                    return;
                }
                owner.hideAfterShown = false;
                if (owner.phase === "hiding") {
                    owner.reopen = true;
                    return;
                }
                instance.show();
            },
            hide: () => {
                owner.reopen = false;
                if (owner.disposed) {
                    return;
                }
                if (owner.phase === "showing") {
                    owner.hideAfterShown = true;
                    return;
                }
                instance.hide();
            },
            dispose: () => {
                if (owner.disposed) {
                    return;
                }
                owner.disposing = true;
                owner.reopen = false;
                if (owner.phase === "hidden") {
                    releaseModal(owner);
                } else {
                    owner.api.hide();
                }
            }
        },
        phase: "hidden", revision: 0, hideAfterShown: false, reopen: false,
        disposing: false, disposed: false, opener: null, lastFocus: null,
        inert: element.hasAttribute("inert"), unsubscribe: () => {}
    };
    const onShow = (event: Event) => {
        const revision = ++owner.revision;
        if (!initialScrollStyles) {
            initialScrollStyles = captureScrollStyles();
        }
        owner.opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        owner.phase = "showing";
        // All event listeners finish before Bootstrap checks defaultPrevented.
        queueMicrotask(() => {
            if (owner.disposed || revision !== owner.revision) {
                return;
            }
            if (event.defaultPrevented) {
                owner.phase = "hidden";
                owner.hideAfterShown = false;
                if (!modalStack.length && !Array.from(owners.values()).some(item => item.phase === "showing")) {
                    restoreScrollStyles();
                }
                if (owner.disposing) {
                    releaseModal(owner);
                }
                return;
            }
            addToStack(owner);
        });
    };
    const onShown = () => {
        ++owner.revision;
        owner.phase = "shown";
        addToStack(owner);
        focusTopModal();
        if (owner.hideAfterShown || owner.disposing) {
            owner.hideAfterShown = false;
            owner.api.hide();
        }
    };
    const onHide = (event: Event) => {
        const revision = ++owner.revision;
        owner.phase = "hiding";
        queueMicrotask(() => {
            if (!owner.disposed && revision === owner.revision) {
                if (event.defaultPrevented) {
                    owner.phase = "shown";
                    owner.reopen = false;
                } else {
                    dismissHelpTooltip();
                }
            }
        });
    };
    const onHidden = () => {
        ++owner.revision;
        owner.phase = "hidden";
        const wasTop = getTopModal() === owner;
        modalStack = modalStack.filter(item => item !== owner);
        resetModalStyle(owner);
        updateStackedModals();
        // Caller hidden handlers may reopen a confirmation before focus is restored.
        const revision = owner.revision;
        queueMicrotask(() => {
            const currentOwner = owners.get(owner.element);
            if (currentOwner && currentOwner !== owner) {
                return;
            }
            if (revision !== owner.revision && owner.phase !== "hidden") {
                return;
            }
            if (owner.disposing && !owner.disposed) {
                releaseModal(owner);
            } else if (owner.reopen) {
                owner.reopen = false;
                owner.api.show();
            }
            const opening = Array.from(owners.values()).some(item => item.phase === "showing");
            if (wasTop && owner.phase === "hidden" && !opening) {
                restoreFocus(owner);
            }
        });
    };
    const events: [string, EventListener][] = [
        ["show.bs.modal", onShow], ["shown.bs.modal", onShown],
        ["hide.bs.modal", onHide], ["hidden.bs.modal", onHidden]
    ];
    events.forEach(([name, listener]) => element.addEventListener(name, listener));
    owner.unsubscribe = () => events.forEach(([name, listener]) => element.removeEventListener(name, listener));
    owners.set(element, owner);
    bindHandlers();
    return owner.api;
}

function addToStack(owner: ModalOwner): void {
    if (!modalStack.includes(owner)) {
        dismissHelpTooltip();
        modalStack.push(owner);
    }
    updateStackedModals();
}

function releaseModal(owner: ModalOwner): void {
    dismissHelpTooltip();
    ++owner.revision;
    owner.disposed = true;
    owner.unsubscribe();
    owners.delete(owner.element);
    owner.instance.dispose();
    resetModalStyle(owner);
    if (owners.size === 0) {
        document.removeEventListener("keydown", handleKeydown, true);
        document.removeEventListener("focusin", handleFocusin, true);
        document.removeEventListener("pointerdown", handlePointer, true);
        document.removeEventListener("click", handlePointer, true);
        window.removeEventListener("pagehide", handlePagehide);
        handlersBound = false;
    }
}

function bindHandlers(): void {
    if (handlersBound) {
        return;
    }
    handlersBound = true;
    document.addEventListener("keydown", handleKeydown, true);
    document.addEventListener("focusin", handleFocusin, true);
    document.addEventListener("pointerdown", handlePointer, true);
    document.addEventListener("click", handlePointer, true);
    window.addEventListener("pagehide", handlePagehide);
}

function handlePagehide(event: PageTransitionEvent): void {
    if (!event.persisted) {
        for (const owner of owners.values()) {
            owner.api.dispose();
        }
    }
}

function updateStackedModals(): void {
    modalStack.forEach((owner, index) => {
        const top = index === modalStack.length - 1;
        owner.element.style.setProperty("z-index", String(MODAL_Z_INDEX + index * MODAL_STACK_STEP), "important");
        owner.element.style.pointerEvents = top ? "auto" : "none";
        if (top) {
            if (!owner.inert) {
                owner.element.removeAttribute("inert");
            }
            owner.element.removeAttribute("aria-hidden");
            owner.element.setAttribute("aria-modal", "true");
        } else {
            owner.element.setAttribute("inert", "");
            owner.element.setAttribute("aria-hidden", "true");
            owner.element.removeAttribute("aria-modal");
        }
    });
    document.querySelectorAll<HTMLElement>(".modal-backdrop").forEach(backdrop => {
        if (!backdrop.hasAttribute("data-modal-stack-backdrop")) {
            backdrop.style.setProperty("display", "none", "important");
        }
    });
    if (modalStack.length === 0) {
        retireBackdrop();
        document.body.classList.remove("modal-open");
        restoreScrollStyles();
        return;
    }
    document.body.classList.add("modal-open");
    if (!lockedScrollStyles) {
        lockedScrollStyles = captureScrollStyles();
    } else {
        applyScrollStyles(lockedScrollStyles);
    }
    const top = getTopModal()!;
    if (modalBackdrop && modalBackdrop.top !== top) {
        retireBackdrop();
    }
    if (!modalBackdrop) {
        const element = document.createElement("div");
        element.classList.add("modal-backdrop", "fade");
        element.setAttribute("data-modal-stack-backdrop", "");
        element.style.pointerEvents = "none";
        document.body.append(element);
        const owner: BackdropOwner = { element, frame: null, top };
        modalBackdrop = owner;
        void element.offsetHeight;
        owner.frame = window.requestAnimationFrame(() => {
            owner.frame = null;
            if (modalBackdrop === owner) {
                element.classList.add("show");
            }
        });
    }
    modalBackdrop.element.style.setProperty("z-index", String(MODAL_Z_INDEX
        + (modalStack.length - 1) * MODAL_STACK_STEP - 15), "important");
}

function captureScrollStyles(): ScrollStyle[] {
    const elements = [document.body, ...document.querySelectorAll<HTMLElement>(
        ".fixed-top, .fixed-bottom, .is-fixed, .sticky-top"
    )];
    return elements.flatMap(element => ["overflow", "padding-right", "margin-right"].map(property => ({
        element, property, value: element.style.getPropertyValue(property),
        priority: element.style.getPropertyPriority(property)
    })));
}

function applyScrollStyles(styles: ScrollStyle[]): void {
    for (const item of styles) {
        if (item.value) {
            item.element.style.setProperty(item.property, item.value, item.priority);
        } else {
            item.element.style.removeProperty(item.property);
        }
    }
}

function restoreScrollStyles(): void {
    if (initialScrollStyles) {
        applyScrollStyles(initialScrollStyles);
    }
    initialScrollStyles = null;
    lockedScrollStyles = null;
}

function retireBackdrop(): void {
    if (!modalBackdrop) {
        return;
    }
    const owner = modalBackdrop;
    modalBackdrop = null;
    if (owner.frame !== null) {
        window.cancelAnimationFrame(owner.frame);
    }
    owner.element.classList.remove("show");
    // This job owns this element only; a new show cannot cancel its removal.
    window.setTimeout(() => owner.element.remove(), MODAL_BACKDROP_TRANSITION_MS);
}

function resetModalStyle(owner: ModalOwner): void {
    owner.element.style.removeProperty("z-index");
    owner.element.style.pointerEvents = "";
    if (!owner.inert) {
        owner.element.removeAttribute("inert");
    }
}

function getTopModal(): ModalOwner | null {
    return modalStack[modalStack.length - 1] ?? null;
}

function isFocusable(element: HTMLElement): boolean {
    return element.isConnected && !element.matches(":disabled")
        && !element.closest("[hidden], [inert], [aria-hidden='true']")
        && window.getComputedStyle(element).visibility !== "hidden"
        && element.getClientRects().length > 0;
}

function focusableChildren(element: HTMLElement): HTMLElement[] {
    return Array.from(element.querySelectorAll<HTMLElement>(
        "a[href], button, input, select, textarea, [tabindex], summary, [contenteditable='true']"
    )).filter(child => child.tabIndex >= 0 && isFocusable(child));
}

function focusTopModal(): void {
    const top = getTopModal();
    if (!top || !top.element.classList.contains("show")) {
        return;
    }
    if (document.activeElement instanceof HTMLElement && top.element.contains(document.activeElement)
        && isFocusable(document.activeElement)) {
        return;
    }
    const remembered = top.lastFocus;
    const target = remembered && top.element.contains(remembered) && isFocusable(remembered)
        ? remembered : focusableChildren(top.element)[0] ?? top.element;
    target.focus({ preventScroll: true });
}

function restoreFocus(closed: ModalOwner): void {
    const top = getTopModal();
    if (closed.opener && isFocusable(closed.opener) && (!top || top.element.contains(closed.opener))) {
        closed.opener.focus({ preventScroll: true });
    } else if (top) {
        focusTopModal();
    } else {
        focusableChildren(document.body)[0]?.focus({ preventScroll: true });
    }
}

function handleFocusin(event: FocusEvent): void {
    const top = getTopModal();
    if (!top) {
        return;
    }
    if (event.target instanceof HTMLElement && top.element.contains(event.target)) {
        top.lastFocus = event.target;
    } else {
        event.stopImmediatePropagation();
        focusTopModal();
    }
}

function handlePointer(event: Event): void {
    const top = getTopModal();
    if (top && (!(event.target instanceof Node) || !top.element.contains(event.target))) {
        event.preventDefault();
        event.stopImmediatePropagation();
        focusTopModal();
    }
}

function handleKeydown(event: KeyboardEvent): void {
    const top = getTopModal();
    if (!top || (event.key !== "Escape" && event.key !== "Tab")) {
        return;
    }
    event.preventDefault();
    event.stopImmediatePropagation();
    if (event.key === "Escape") {
        if (!dismissHelpTooltip()) {
            top.api.hide();
        }
        return;
    }
    const elements = focusableChildren(top.element);
    if (elements.length === 0) {
        top.element.focus({ preventScroll: true });
        return;
    }
    const index = elements.indexOf(document.activeElement as HTMLElement);
    const next = event.shiftKey ? (index <= 0 ? elements.length - 1 : index - 1) : (index + 1) % elements.length;
    elements[next].focus({ preventScroll: true });
}
