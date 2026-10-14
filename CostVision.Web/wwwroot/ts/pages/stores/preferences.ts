

import type { StoresSession } from "./state.js";
import type { StoresBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<StoresSession,
        "GROUP_BY_NAME_COOKIE_NAME" | "PAGE_SIZE_COOKIE_NAME" | "SHOW_ORIGINAL_NAMES_COOKIE_NAME" | "SORT_BY_COOKIE_NAME" | "SORT_DIRECTION_COOKIE_NAME" | "data">;
    bindings: Pick<StoresBindings,
        "groupByNameInput" | "pageSizeInputs" | "showOriginalNamesInput">;
    getCookie: (name: string) => string | null;
    setCookie: (name: string, value: string) => void;
}

export function createStoresPreferences(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function restorePreferences(): void {
        const sortBy = dependencies.getCookie(session.SORT_BY_COOKIE_NAME);
        const sortDirection = dependencies.getCookie(session.SORT_DIRECTION_COOKIE_NAME);
        const pageSize = Number(dependencies.getCookie(session.PAGE_SIZE_COOKIE_NAME));

        if (sortBy === "name" || sortBy === "receiptCount" || sortBy === "totalSpent") {
            session.data.sortBy = sortBy;
        }

        if (sortDirection === "asc" || sortDirection === "desc") {
            session.data.sortDirection = sortDirection;
        }

        if (pageSize === 20 || pageSize === 50 || pageSize === 100) {
            session.data.pageSize = pageSize;
        }

        session.data.showOriginalNames = dependencies.getCookie(session.SHOW_ORIGINAL_NAMES_COOKIE_NAME) === "true";
        session.data.groupByName = dependencies.getCookie(session.GROUP_BY_NAME_COOKIE_NAME) === "true";
        bindings.showOriginalNamesInput.checked = session.data.showOriginalNames;
        bindings.groupByNameInput.checked = session.data.groupByName;
    }

    function savePreferences(): void {
        dependencies.setCookie(session.SORT_BY_COOKIE_NAME, session.data.sortBy);
        dependencies.setCookie(session.SORT_DIRECTION_COOKIE_NAME, session.data.sortDirection);
        dependencies.setCookie(session.PAGE_SIZE_COOKIE_NAME, String(session.data.pageSize));
        dependencies.setCookie(session.SHOW_ORIGINAL_NAMES_COOKIE_NAME, String(session.data.showOriginalNames));
        dependencies.setCookie(session.GROUP_BY_NAME_COOKIE_NAME, String(session.data.groupByName));
    }

    function syncPageSizeInputs(pageSize: number): void {
        for (const pageSizeInput of bindings.pageSizeInputs) {
            pageSizeInput.value = String(pageSize);
        }
    }

    return { restorePreferences, syncPageSizeInputs, savePreferences };
}
