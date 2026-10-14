export interface FileDropzoneOptions {
    dropzone: HTMLElement;
    fileInput: HTMLInputElement;
    multiple: boolean;
    onFilesSelected: (files: File[]) => void;
}

export function initFileDropzone(options: FileDropzoneOptions): () => void {
    applyDropzoneActiveState(options.dropzone, false);
    const subscriptions: (() => void)[] = [];
    function bind<TEvent extends Event>(target: EventTarget, name: string, handler: (event: TEvent) => void): void {
        const listener = handler as EventListener;
        target.addEventListener(name, listener);
        subscriptions.push(() => target.removeEventListener(name, listener));
    }

    if (!options.dropzone.hasAttribute("tabindex")) {
        options.dropzone.setAttribute("tabindex", "0");
    }

    bind(options.dropzone, "click", () => {
        options.fileInput.click();
    });

    bind<KeyboardEvent>(options.dropzone, "keydown", event => {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        event.preventDefault();
        options.fileInput.click();
    });

    bind<DragEvent>(options.dropzone, "dragenter", event => handleDragEnterOrOver(event, options.dropzone));
    bind<DragEvent>(options.dropzone, "dragover", event => handleDragEnterOrOver(event, options.dropzone));
    bind<DragEvent>(options.dropzone, "dragleave", event => handleDragLeaveOrEnd(event, options.dropzone));
    bind<DragEvent>(options.dropzone, "dragend", event => handleDragLeaveOrEnd(event, options.dropzone));
    bind<DragEvent>(options.dropzone, "drop", event => handleDrop(event, options));

    bind(options.fileInput, "change", () => {
        const files = options.fileInput.files ? Array.from(options.fileInput.files) : [];
        if (files.length === 0) {
            return;
        }

        options.onFilesSelected(options.multiple ? files : files.slice(0, 1));
    });
    return () => {
        for (const unsubscribe of subscriptions) {
            unsubscribe();
        }
        applyDropzoneActiveState(options.dropzone, false);
    };
}

function handleDragEnterOrOver(event: DragEvent, dropzone: HTMLElement): void {
    event.preventDefault();
    event.stopPropagation();
    applyDropzoneActiveState(dropzone, true);
}

function handleDragLeaveOrEnd(event: DragEvent, dropzone: HTMLElement): void {
    event.preventDefault();
    event.stopPropagation();

    if (event.type === "dragleave" && event.relatedTarget instanceof Node && dropzone.contains(event.relatedTarget)) {
        return;
    }

    applyDropzoneActiveState(dropzone, false);
}

function handleDrop(event: DragEvent, options: FileDropzoneOptions): void {
    event.preventDefault();
    event.stopPropagation();
    applyDropzoneActiveState(options.dropzone, false);

    const files = event.dataTransfer?.files ? Array.from(event.dataTransfer.files) : [];
    if (files.length === 0) {
        return;
    }

    options.onFilesSelected(options.multiple ? files : files.slice(0, 1));
}

function applyDropzoneActiveState(dropzone: HTMLElement, isActive: boolean): void {
    dropzone.classList.toggle("border-primary", isActive);
    dropzone.classList.toggle("bg-primary-subtle", isActive);
    dropzone.classList.toggle("shadow-sm", isActive);
    dropzone.style.setProperty("border-style", "dashed", "important");
    dropzone.style.setProperty("border-color", isActive ? "#3b82f6" : "#212529", "important");
    dropzone.style.setProperty("background-color", isActive ? "#dbeafe" : "#f8f9fa", "important");
}
