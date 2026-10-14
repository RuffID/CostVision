import { getWriteErrorMessage } from "../../shared/requestCoordinator.js";
import { hideAlertMessage, showAlertMessage } from "../../shared/alerts.js";

import { requireElementById } from "../../shared/dom.js";

import { updateStoreAdaptiveName } from "./api.js";

import type { StoreListItem } from "./types.js";
import type { StoresSession } from "./state.js";
import type { StoresBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<StoresSession,
        "data" | "editedStoreId" | "namingContext" | "namingWrites">;
    bindings: Pick<StoresBindings,
        "adaptiveNameInput" | "adaptiveNameModal" | "alertElement" | "clearAdaptiveNameButton" | "originalNameInput" | "saveAdaptiveNameButton">;
    loadPage: (page: number) => Promise<void>;
}

export function createStoreNamingController(dependencies: Dependencies) {
    const { session, bindings } = dependencies;

    function openAdaptiveNameModal(store: StoreListItem): void {
        session.namingContext.invalidate();
        session.editedStoreId = store.id;
        bindings.saveAdaptiveNameButton.disabled = false;
        requireElementById<HTMLElement>("storeAdaptiveNameModalTitle").textContent = `Укажите общее наименование для магазина - ${store.name}`;
        bindings.originalNameInput.value = store.name;
        bindings.adaptiveNameInput.value = store.adaptiveName ?? "";
        bindings.clearAdaptiveNameButton.classList.toggle("d-none", !store.adaptiveName);
        bindings.adaptiveNameModal.show();
    }

    async function saveAdaptiveName(): Promise<void> {
        const id = session.editedStoreId;
        if (!id) { throw new Error("Не выбран объект для изменения наименования."); }
        if (session.namingWrites.has(id)) { return; }
        const adaptiveName = bindings.adaptiveNameInput.value;
        const isCurrent = session.namingContext.capture();
        session.namingWrites.add(id);
        bindings.saveAdaptiveNameButton.disabled = true;
        try {
            hideAlertMessage(bindings.alertElement);
            await updateStoreAdaptiveName(id, adaptiveName);
            if (!isCurrent()) { return; }
            await dependencies.loadPage(session.data.page);
            if (isCurrent()) { bindings.adaptiveNameModal.hide(); }
        }
        catch (error) {
            if (isCurrent()) { showAlertMessage(bindings.alertElement, getWriteErrorMessage(error)); }
        }
        finally {
            session.namingWrites.delete(id);
            if (isCurrent()) { bindings.saveAdaptiveNameButton.disabled = false; }
        }
    }

    return { saveAdaptiveName, openAdaptiveNameModal };
}
