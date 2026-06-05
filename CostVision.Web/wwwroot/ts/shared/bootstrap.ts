export interface BootstrapModal {
    show(): void;
    hide(): void;
}

interface BootstrapModalConstructor {
    new (element: Element): BootstrapModal;
    getOrCreateInstance(element: Element): BootstrapModal;
}

interface BootstrapApi {
    Modal: BootstrapModalConstructor;
}

const MODAL_Z_INDEX = 2010;
const MODAL_STACK_STEP = 30;
const MODAL_BACKDROP_Z_INDEX_OFFSET = 15;
const MODAL_BACKDROP_TRANSITION_MS = 150;

let modalStack: HTMLElement[] = [];
let stackedModalElements = new WeakSet<HTMLElement>();
let isStackedModalEscapeHandlerBound = false;
let modalBackdrop: HTMLElement | null = null;
let modalBackdropHideTimer: number | null = null;

declare global {
    interface Window {
        bootstrap?: BootstrapApi;
    }
}

export function createBootstrapModal(element: Element): BootstrapModal {
    if (!window.bootstrap?.Modal) {
        throw new Error("Bootstrap Modal API недоступен.");
    }

    bindStackedModal(element);
    return new window.bootstrap.Modal(element);
}

export function getOrCreateBootstrapModal(element: Element): BootstrapModal {
    if (!window.bootstrap?.Modal) {
        throw new Error("Bootstrap Modal API недоступен.");
    }

    bindStackedModal(element);
    return window.bootstrap.Modal.getOrCreateInstance(element);
}

function bindStackedModal(element: Element): void {
    if (!(element instanceof HTMLElement) || stackedModalElements.has(element)) {
        return;
    }

    stackedModalElements.add(element);
    bindStackedModalEscapeHandler();

    element.addEventListener("show.bs.modal", () => {
        modalStack = modalStack.filter(modal => modal !== element);
        modalStack.push(element);
        scheduleStackedModalUpdate();
    });

    element.addEventListener("shown.bs.modal", () => {
        modalStack = modalStack.filter(modal => modal !== element);
        modalStack.push(element);
        scheduleStackedModalUpdate();
        focusTopModal();
    });

    element.addEventListener("hidden.bs.modal", () => {
        modalStack = modalStack.filter(modal => modal !== element);
        element.style.removeProperty("z-index");
        element.style.pointerEvents = "";
        window.setTimeout(() => {
            scheduleStackedModalUpdate();
            focusTopModal();
        }, 0);
    });
}

function bindStackedModalEscapeHandler(): void {
    if (isStackedModalEscapeHandlerBound) {
        return;
    }

    document.addEventListener("keydown", handleStackedModalEscape, true);
    isStackedModalEscapeHandlerBound = true;
}

function handleStackedModalEscape(event: KeyboardEvent): void {
    if (event.key !== "Escape") {
        return;
    }

    const topModal = getTopModal();
    if (!topModal) {
        return;
    }

    const target = event.target;
    if (target instanceof Node && topModal.contains(target)) {
        return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    focusTopModal();
    window.bootstrap?.Modal.getOrCreateInstance(topModal).hide();
}

function scheduleStackedModalUpdate(): void {
    updateStackedModals();
    window.setTimeout(updateStackedModals, 0);
    window.setTimeout(updateStackedModals, 50);
}

function updateStackedModals(): void {
    const backdrops = Array.from(document.querySelectorAll<HTMLElement>(".modal-backdrop"))
        .filter(backdrop => backdrop !== modalBackdrop);

    modalStack.forEach((modal, index) => {
        modal.style.setProperty("z-index", String(MODAL_Z_INDEX + index * MODAL_STACK_STEP), "important");
        modal.style.pointerEvents = index === modalStack.length - 1 ? "auto" : "none";
    });

    backdrops.forEach(backdrop => {
        backdrop.style.setProperty("display", "none", "important");
        backdrop.style.removeProperty("z-index");
    });

    updateModalBackdrop();

    if (modalStack.length > 0) {
        document.body.classList.add("modal-open");
    }
}

function updateModalBackdrop(): void {
    if (modalStack.length === 0) {
        hideModalBackdrop();
        modalBackdrop = null;
        return;
    }

    if (!modalBackdrop) {
        modalBackdrop = document.createElement("div");
        modalBackdrop.classList.add("modal-backdrop", "fade");
        document.body.append(modalBackdrop);
        window.requestAnimationFrame(() => {
            modalBackdrop?.classList.add("show");
        });
    }
    else {
        clearModalBackdropHideTimer();
        modalBackdrop.classList.add("show");
    }

    const backdropZIndex = MODAL_Z_INDEX + (modalStack.length - 1) * MODAL_STACK_STEP - MODAL_BACKDROP_Z_INDEX_OFFSET;
    modalBackdrop.style.setProperty("display", "block", "important");
    modalBackdrop.style.setProperty("z-index", String(backdropZIndex), "important");
}

function hideModalBackdrop(): void {
    if (!modalBackdrop) {
        return;
    }

    const backdrop = modalBackdrop;
    clearModalBackdropHideTimer();
    backdrop.classList.remove("show");
    modalBackdropHideTimer = window.setTimeout(() => {
        backdrop.remove();
        if (modalBackdrop === backdrop) {
            modalBackdrop = null;
        }
        modalBackdropHideTimer = null;
    }, MODAL_BACKDROP_TRANSITION_MS);
}

function clearModalBackdropHideTimer(): void {
    if (modalBackdropHideTimer === null) {
        return;
    }

    window.clearTimeout(modalBackdropHideTimer);
    modalBackdropHideTimer = null;
}

function focusTopModal(): void {
    const topModal = getTopModal();
    if (!topModal) {
        return;
    }

    if (document.activeElement instanceof HTMLElement && topModal.contains(document.activeElement)) {
        return;
    }

    topModal.focus({ preventScroll: true });
}

function getTopModal(): HTMLElement | null {
    return modalStack.length === 0 ? null : modalStack[modalStack.length - 1];
}
