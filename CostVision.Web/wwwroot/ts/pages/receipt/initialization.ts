import { requireElementById } from "../../shared/dom.js";
import { createReceiptController } from "./controller.js";
import { createReceiptStorage } from "./draftPersistence.js";

export async function initQrScan(): Promise<void> {
    const page = requireElementById("receipt-page");
    const userId = page.dataset.currentUserId;
    if (!userId) {
        throw new Error("Не определён текущий пользователь страницы.");
    }
    const controller = createReceiptController(userId, createReceiptStorage(userId));
    await controller.initialize();
}
