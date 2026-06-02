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

declare global {
    interface Window {
        bootstrap?: BootstrapApi;
    }
}

export function createBootstrapModal(element: Element): BootstrapModal {
    if (!window.bootstrap?.Modal) {
        throw new Error("Bootstrap Modal API недоступен.");
    }

    return new window.bootstrap.Modal(element);
}

export function getOrCreateBootstrapModal(element: Element): BootstrapModal {
    if (!window.bootstrap?.Modal) {
        throw new Error("Bootstrap Modal API недоступен.");
    }

    return window.bootstrap.Modal.getOrCreateInstance(element);
}
