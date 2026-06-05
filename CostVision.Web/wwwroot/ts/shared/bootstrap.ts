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

let modalStack: HTMLElement[] = [];
let stackedModalElements = new WeakSet<HTMLElement>();
let isStackedModalEscapeHandlerBound = false;

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
    modalStack = modalStack.filter(modal => modal.classList.contains("show"));

    const backdrops = Array.from(document.querySelectorAll<HTMLElement>(".modal-backdrop"));

    modalStack.forEach((modal, index) => {
        modal.style.setProperty("z-index", String(MODAL_Z_INDEX + index * MODAL_STACK_STEP), "important");
        modal.style.pointerEvents = index === modalStack.length - 1 ? "auto" : "none";
    });

    backdrops.forEach(backdrop => {
        backdrop.style.setProperty("display", "none", "important");
    });

    if (modalStack.length > 0) {
        document.body.classList.add("modal-open");
    }
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
