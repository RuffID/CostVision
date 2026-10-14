export interface PagedList<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

// Каталоги сохраняют окно до пяти кнопок; отчёт — прежнее окно page±2.
export function getPageRange(currentPage: number, totalPages: number, mode: "catalog" | "centered" = "catalog"): number[] {
    let start: number;
    let end: number;
    if (mode === "centered") {
        start = Math.max(1, currentPage - 2);
        end = Math.min(totalPages, currentPage + 2);
    } else {
        start = currentPage >= 5 ? currentPage - 2 : 1;
        if (start + 4 > totalPages) {
            start = Math.max(1, totalPages - 4);
        }
        end = Math.min(totalPages, start + 4);
    }

    const pages: number[] = [];
    for (let page = start; page <= end; page++) {
        pages.push(page);
    }
    return pages;
}

export function createPageButton(text: string, onClick: () => void, isActive = false): HTMLButtonElement {
    const button = document.createElement("button");
    button.type = "button";
    button.className = isActive ? "btn btn-primary" : "btn btn-outline-secondary";
    button.textContent = text;
    button.addEventListener("click", onClick);
    return button;
}

export function createPageNavigation(after: HTMLElement, load: (page: number) => Promise<void>,
    onError: (error: unknown) => void, countLabel = "Всего"): {
        render: (page: PagedList<unknown>) => void;
        clear: () => void;
    } {
    const container = document.createElement("nav");
    container.classList.add("d-flex", "gap-2", "align-items-center", "my-2");
    container.setAttribute("aria-label", "Страницы списка");
    const previous = document.createElement("button");
    const next = document.createElement("button");
    const label = document.createElement("span");
    previous.type = next.type = "button";
    previous.textContent = "Назад";
    next.textContent = "Далее";
    previous.classList.add("btn", "btn-sm", "btn-outline-secondary");
    next.classList.add("btn", "btn-sm", "btn-outline-secondary");
    label.classList.add("small", "text-muted");
    container.append(previous, label, next);
    after.after(container);
    let current: PagedList<unknown> | null = null;
    let busy = false;
    let revision = 0;
    container.hidden = true;

    async function navigate(delta: number): Promise<void> {
        if (!current || busy) {
            return;
        }

        const activeRevision = revision;
        busy = true;
        previous.disabled = next.disabled = true;
        try {
            await load(current.page + delta);
        } catch (error) {
            if (activeRevision === revision) { onError(error); }
        } finally {
            if (activeRevision !== revision) { return; }
            busy = false;
            if (current) {
                previous.disabled = !current.hasPreviousPage;
                next.disabled = !current.hasNextPage;
            }
        }
    }

    previous.addEventListener("click", () => { void navigate(-1); });
    next.addEventListener("click", () => { void navigate(1); });
    return {
        render(page): void {
            current = page;
            container.hidden = false;
            label.textContent = `Страница ${page.page} из ${page.totalPages} · ${countLabel}: ${page.totalCount}`;
            previous.disabled = busy || !page.hasPreviousPage;
            next.disabled = busy || !page.hasNextPage;
        },
        clear(): void {
            revision++;
            busy = false;
            current = null;
            container.hidden = true;
            previous.disabled = next.disabled = true;
        }
    };
}
