export interface TableLoadingIndicator {
    show(): void;
    hide(): void;
}

export function createTableLoadingIndicator(tableContainer: HTMLElement, message: string, minimumHeightPx: number): TableLoadingIndicator {
    if (!Number.isFinite(minimumHeightPx) || minimumHeightPx <= 0) {
        throw new Error("Минимальная высота индикатора загрузки должна быть положительным числом.");
    }

    const table = tableContainer.querySelector<HTMLElement>("[data-table-loading-content]");
    if (!table) {
        throw new Error("Не найдено содержимое таблицы для индикатора загрузки.");
    }

    const loadingElement = document.createElement("div");
    loadingElement.className = "d-flex flex-column align-items-center justify-content-center gap-2 py-5";
    loadingElement.style.minHeight = `${minimumHeightPx}px`;
    loadingElement.setAttribute("role", "status");

    const spinner = document.createElement("div");
    spinner.className = "spinner-border text-primary";
    spinner.setAttribute("aria-hidden", "true");

    const text = document.createElement("span");
    text.className = "text-muted";
    text.textContent = message;

    loadingElement.append(spinner, text);
    tableContainer.append(loadingElement);

    return {
        show(): void {
            table.classList.add("d-none");
            loadingElement.classList.remove("d-none");
        },
        hide(): void {
            loadingElement.classList.add("d-none");
            table.classList.remove("d-none");
        }
    };
}
