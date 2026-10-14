export interface HelpTooltipOptions {
    title: string;
    text: string | string[];
}

interface ActiveHelpTooltip {
    trigger: HTMLElement;
    popup: HTMLElement;
    pinned: boolean;
    describedBy: string | null;
}

const HOVER_OPEN_DELAY_MS = 500;

let activeHelpTooltip: ActiveHelpTooltip | null = null;
let hoverOpenTimerId: number | null = null;
let hoverTrigger: HTMLElement | null = null;
let hoverCloseTimerId: number | null = null;
let popupSequence = 0;
const bindings = new Map<HTMLElement, { trigger: HTMLElement; dispose: () => void }>();
let removalObserver: MutationObserver | null = null;

export function renderHelpTooltip(host: HTMLElement, options: HelpTooltipOptions): HTMLButtonElement {
    bindings.get(host)?.dispose();
    host.replaceChildren();

    let trigger = document.createElement("button");
    trigger.type = "button";
    trigger.classList.add("btn", "btn-sm", "btn-outline-secondary", "rounded-circle", "d-inline-flex", "align-items-center", "justify-content-center", "ms-1", "p-0");
    trigger.style.width = "1.25rem";
    trigger.style.height = "1.25rem";
    trigger.style.lineHeight = "1";
    trigger.setAttribute("aria-label", options.title);
    trigger.textContent = "?";

    trigger.setAttribute("aria-expanded", "false");
    bindings.set(host, { trigger, dispose: bindHelpTooltip(host, trigger, options) });
    host.append(trigger);

    return trigger;
}

function bindHelpTooltip(host: HTMLElement, trigger: HTMLElement, options: HelpTooltipOptions): () => void {
    const onEnter = () => {
        clearHoverCloseTimer();
        scheduleHoverHelpTooltip(trigger, options);
    };
    const onLeave = () => {
        if (hoverTrigger === trigger) {
            clearHoverOpenTimer();
        }
        if (document.activeElement !== trigger) {
            scheduleHoverClose(trigger);
        }
    };
    const onClick = (event: Event) => {
        event.preventDefault();
        event.stopPropagation();
        clearHoverOpenTimer();
        if (activeHelpTooltip?.trigger === trigger && activeHelpTooltip.pinned) {
            closeActiveHelpTooltip();
        } else {
            showHelpTooltip(trigger, options, true);
        }
    };
    const onFocus = () => showHelpTooltip(trigger, options, false);
    const onBlur = () => {
        if (activeHelpTooltip?.trigger === trigger) {
            closeActiveHelpTooltip();
        }
    };
    const onModalHide = () => {
        if (hoverTrigger === trigger || activeHelpTooltip?.trigger === trigger) {
            dismissHelpTooltip();
        }
    };
    const modal = trigger.closest(".modal") ?? host.closest(".modal");
    const dispose = () => {
        onModalHide();
        trigger.removeEventListener("mouseenter", onEnter);
        trigger.removeEventListener("mouseleave", onLeave);
        trigger.removeEventListener("click", onClick);
        trigger.removeEventListener("focus", onFocus);
        trigger.removeEventListener("blur", onBlur);
        modal?.removeEventListener("hidden.bs.modal", onModalHide);
        bindings.delete(host);
        if (bindings.size === 0) {
            window.removeEventListener("pagehide", handlePagehide);
            removalObserver?.disconnect();
            removalObserver = null;
        }
    };
    trigger.addEventListener("mouseenter", onEnter);
    trigger.addEventListener("mouseleave", onLeave);
    trigger.addEventListener("click", onClick);
    trigger.addEventListener("focus", onFocus);
    trigger.addEventListener("blur", onBlur);
    modal?.addEventListener("hidden.bs.modal", onModalHide);
    if (bindings.size === 0) {
        window.addEventListener("pagehide", handlePagehide);
        removalObserver = new MutationObserver(() => {
            for (const [boundHost, binding] of bindings) {
                if (!boundHost.isConnected || !boundHost.contains(binding.trigger)) {
                    binding.dispose();
                }
            }
        });
        removalObserver.observe(document.body, { childList: true, subtree: true });
    }
    return dispose;
}

function handlePagehide(event: PageTransitionEvent): void {
    dismissHelpTooltip();
    if (!event.persisted) {
        for (const binding of bindings.values()) {
            binding.dispose();
        }
    }
}

export function dismissHelpTooltip(): boolean {
    const wasOpen = activeHelpTooltip !== null;
    clearHoverOpenTimer();
    clearHoverCloseTimer();
    closeActiveHelpTooltip();
    return wasOpen;
}

function clearHoverCloseTimer(): void {
    if (hoverCloseTimerId !== null) {
        window.clearTimeout(hoverCloseTimerId);
        hoverCloseTimerId = null;
    }
}

function scheduleHoverClose(trigger: HTMLElement): void {
    if (!activeHelpTooltip || activeHelpTooltip.trigger !== trigger || activeHelpTooltip.pinned
        || document.activeElement === trigger) {
        return;
    }
    clearHoverCloseTimer();
    hoverCloseTimerId = window.setTimeout(() => {
        hoverCloseTimerId = null;
        hideHoverHelpTooltip(trigger);
    }, 100);
}

function scheduleHoverHelpTooltip(trigger: HTMLElement, options: HelpTooltipOptions): void {
    clearHoverOpenTimer();
    hoverTrigger = trigger;
    hoverOpenTimerId = window.setTimeout(() => {
        hoverOpenTimerId = null;
        hoverTrigger = null;
        if (trigger.isConnected) {
            showHelpTooltip(trigger, options, false);
        }
    }, HOVER_OPEN_DELAY_MS);
}

function clearHoverOpenTimer(): void {
    if (hoverOpenTimerId === null) {
        return;
    }

    window.clearTimeout(hoverOpenTimerId);
    hoverOpenTimerId = null;
    hoverTrigger = null;
}

function showHelpTooltip(trigger: HTMLElement, options: HelpTooltipOptions, pinned: boolean): void {
    clearHoverOpenTimer();
    if (activeHelpTooltip && activeHelpTooltip.trigger !== trigger) {
        closeActiveHelpTooltip();
    }

    if (!activeHelpTooltip) {
        activeHelpTooltip = {
            trigger: trigger,
            popup: createHelpTooltipPopup(options),
            pinned: pinned,
            describedBy: trigger.getAttribute("aria-describedby")
        };
        const popup = activeHelpTooltip.popup;
        popup.id = `help-tooltip-${++popupSequence}`;
        popup.setAttribute("role", "tooltip");
        trigger.setAttribute("aria-describedby", [activeHelpTooltip.describedBy, popup.id].filter(Boolean).join(" "));
        trigger.setAttribute("aria-controls", popup.id);
        trigger.setAttribute("aria-expanded", "true");
        (trigger.closest(".modal") ?? document.body).append(popup);
        popup.addEventListener("mouseenter", clearHoverCloseTimer);
        popup.addEventListener("mouseleave", () => scheduleHoverClose(trigger));
    } else {
        activeHelpTooltip.pinned = activeHelpTooltip.pinned || pinned;
    }

    positionHelpTooltip(trigger, activeHelpTooltip.popup);
    window.addEventListener("resize", handleWindowResize);
    document.addEventListener("pointerdown", handleDocumentPointerDown, true);
    document.addEventListener("keydown", handleDocumentKeydown);
    window.addEventListener("scroll", handleWindowResize, true);
}

function handleDocumentKeydown(event: KeyboardEvent): void {
    if (event.key === "Escape" && dismissHelpTooltip()) {
        event.preventDefault();
        event.stopImmediatePropagation();
    }
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
    popup.style.zIndex = getHelpTooltipZIndex(trigger);

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

function getHelpTooltipZIndex(trigger: HTMLElement): string {
    const modal = trigger.closest<HTMLElement>(".modal");
    if (!modal) {
        return "1080";
    }

    const modalZIndex = Number(window.getComputedStyle(modal).zIndex);
    return Number.isFinite(modalZIndex) ? String(modalZIndex + 10) : "1080";
}

function handleWindowResize(): void {
    if (!activeHelpTooltip) {
        return;
    }

    positionHelpTooltip(activeHelpTooltip.trigger, activeHelpTooltip.popup);
}

function handleDocumentPointerDown(event: PointerEvent): void {
    if (!activeHelpTooltip) {
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
    clearHoverCloseTimer();
    const { trigger, describedBy } = activeHelpTooltip;
    const descriptions = (trigger.getAttribute("aria-describedby") ?? describedBy ?? "")
        .split(/\s+/).filter(id => id && id !== activeHelpTooltip!.popup.id);
    if (descriptions.length === 0) {
        trigger.removeAttribute("aria-describedby");
    } else {
        trigger.setAttribute("aria-describedby", descriptions.join(" "));
    }
    trigger.removeAttribute("aria-controls");
    trigger.setAttribute("aria-expanded", "false");
    activeHelpTooltip.popup.remove();
    activeHelpTooltip = null;
    window.removeEventListener("resize", handleWindowResize);
    document.removeEventListener("pointerdown", handleDocumentPointerDown, true);
    document.removeEventListener("keydown", handleDocumentKeydown);
    window.removeEventListener("scroll", handleWindowResize, true);
}
