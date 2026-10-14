import type { BootstrapModal } from "../../shared/bootstrap.js";
import type { createPageNavigation } from "../../shared/paging.js";
import type { MoneyMovementsUi } from "./ui.js";
import type { MoneyMovementImportModalController } from "./importModal.js";

export class MoneyMovementsBindings {
    movementNavigation!: ReturnType<typeof createPageNavigation>;
    candidateNavigation!: ReturnType<typeof createPageNavigation>;
    ui!: MoneyMovementsUi;
    moveAccountModal!: BootstrapModal;
    deleteModal!: BootstrapModal;
    importModal!: BootstrapModal;
    receiptsModal!: BootstrapModal;
    receiptDetailsModal!: BootstrapModal;
    importController!: MoneyMovementImportModalController;
}
