export interface HelpTooltipOptions {
    title: string;
    text: string | string[];
}

interface ActiveHelpTooltip {
    trigger: HTMLElement;
    popup: HTMLElement;
    pinned: boolean;
}

const HOVER_OPEN_DELAY_MS = 500;

let activeHelpTooltip: ActiveHelpTooltip | null = null;
let hoverOpenTimerId: number | null = null;

export function renderHelpTooltip(host: HTMLElement, options: HelpTooltipOptions): HTMLButtonElement {
    host.replaceChildren();

    let trigger = document.createElement("button");
    trigger.type = "button";
    trigger.classList.add("btn", "btn-sm", "btn-outline-secondary", "rounded-circle", "d-inline-flex", "align-items-center", "justify-content-center", "ms-1", "p-0");
    trigger.style.width = "1.25rem";
    trigger.style.height = "1.25rem";
    trigger.style.lineHeight = "1";
    trigger.setAttribute("aria-label", options.title);
    trigger.textContent = "?";

    bindHelpTooltip(trigger, options);
    host.append(trigger);

    return trigger;
}

function bindHelpTooltip(trigger: HTMLElement, options: HelpTooltipOptions): void {
    trigger.addEventListener("mouseenter", () => scheduleHoverHelpTooltip(trigger, options));
    trigger.addEventListener("mouseleave", () => {
        clearHoverOpenTimer();
        hideHoverHelpTooltip(trigger);
    });
    trigger.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();
        clearHoverOpenTimer();
        showHelpTooltip(trigger, options, true);
    });
}

function scheduleHoverHelpTooltip(trigger: HTMLElement, options: HelpTooltipOptions): void {
    clearHoverOpenTimer();

    hoverOpenTimerId = window.setTimeout(() => {
        hoverOpenTimerId = null;
        showHelpTooltip(trigger, options, false);
    }, HOVER_OPEN_DELAY_MS);
}

function clearHoverOpenTimer(): void {
    if (hoverOpenTimerId === null) {
        return;
    }

    window.clearTimeout(hoverOpenTimerId);
    hoverOpenTimerId = null;
}

function showHelpTooltip(trigger: HTMLElement, options: HelpTooltipOptions, pinned: boolean): void {
    if (activeHelpTooltip && activeHelpTooltip.trigger !== trigger) {
        closeActiveHelpTooltip();
    }

    if (!activeHelpTooltip) {
        activeHelpTooltip = {
            trigger: trigger,
            popup: createHelpTooltipPopup(options),
            pinned: pinned
        };

        document.body.append(activeHelpTooltip.popup);
    } else {
        activeHelpTooltip.pinned = activeHelpTooltip.pinned || pinned;
    }

    positionHelpTooltip(trigger, activeHelpTooltip.popup);
    window.addEventListener("resize", handleWindowResize);
    document.addEventListener("pointerdown", handleDocumentPointerDown, true);
}

function hideHoverHelpTooltip(trigger: HTMLElement): void {
    if (!activeHelpTooltip || activeHelpTooltip.trigger !== trigger || activeHelpTooltip.pinned) {
        return;
    }

    closeActiveHelpTooltip();
}

function createHelpTooltipPopup(options: HelpTooltipOptions): HTMLElement {
    let popup = document.createElement("div");
    popup.classList.add("position-fixed", "border", "rounded-3", "shadow", "bg-white", "overflow-hidden");
    popup.style.maxWidth = "320px";
    popup.style.minWidth = "240px";
    popup.style.zIndex = "1080";

    let header = document.createElement("div");
    header.classList.add("bg-light", "border-bottom", "fw-semibold", "text-dark", "px-3", "py-2");
    header.textContent = options.title;

    let body = document.createElement("div");
    body.classList.add("text-black", "px-3", "py-2");
    appendHelpTooltipBodyText(body, options.text);

    popup.append(header, body);

    return popup;
}

function appendHelpTooltipBodyText(body: HTMLElement, text: string | string[]): void {
    if (typeof text === "string") {
        body.textContent = text;
        return;
    }

    body.classList.add("d-flex", "flex-column", "gap-2");

    for (let lineText of text) {
        let line = document.createElement("div");
        line.textContent = lineText;
        body.append(line);
    }
}

function positionHelpTooltip(trigger: HTMLElement, popup: HTMLElement): void {
    let triggerRect = trigger.getBoundingClientRect();
    let popupRect = popup.getBoundingClientRect();
    let viewportPadding = 8;
    let top = triggerRect.bottom + viewportPadding;
    let left = triggerRect.left;

    if (left + popupRect.width > window.innerWidth - viewportPadding) {
        left = window.innerWidth - popupRect.width - viewportPadding;
    }

    if (left < viewportPadding) {
        left = viewportPadding;
    }

    if (top + popupRect.height > window.innerHeight - viewportPadding) {
        top = triggerRect.top - popupRect.height - viewportPadding;
    }

    if (top < viewportPadding) {
        top = viewportPadding;
    }

    popup.style.left = `${left}px`;
    popup.style.top = `${top}px`;
}

function handleWindowResize(): void {
    if (!activeHelpTooltip) {
        return;
    }

    positionHelpTooltip(activeHelpTooltip.trigger, activeHelpTooltip.popup);
}

function handleDocumentPointerDown(event: PointerEvent): void {
    if (!activeHelpTooltip || !activeHelpTooltip.pinned) {
        return;
    }

    let target = event.target;
    if (!(target instanceof Node)) {
        return;
    }

    if (activeHelpTooltip.popup.contains(target) || activeHelpTooltip.trigger.contains(target)) {
        return;
    }

    closeActiveHelpTooltip();
}

function closeActiveHelpTooltip(): void {
    if (!activeHelpTooltip) {
        return;
    }

    clearHoverOpenTimer();
    activeHelpTooltip.popup.remove();
    activeHelpTooltip = null;
    window.removeEventListener("resize", handleWindowResize);
    document.removeEventListener("pointerdown", handleDocumentPointerDown, true);
}
