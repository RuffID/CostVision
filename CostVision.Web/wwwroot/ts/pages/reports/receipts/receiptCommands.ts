import { createReceiptWorkflow } from "../../../shared/receipts/workflow.js";
import { openReceiptApi, refreshReceiptApi } from "../../../shared/receipts/api.js";
import { updateCardFromDto } from "./ui/receiptCards.js";
import { renderReceiptDetails } from "./ui/receiptDetailsModal.js";
import type { ReceiptsSession } from "./state.js";
import type { ReceiptsBindings } from "./bindings.js";

interface Dependencies {
    session: Pick<ReceiptsSession,
        "detailRequests" | "forgeryToken" | "listRequests" | "pageState" | "receiptWrites">;
    bindings: Pick<ReceiptsBindings,
        "bootstrapModal" | "detailsList" | "modalHeader" | "modalTotal">;
    formatNumber(value: number): string;
    formatCurrency(value: number): string;
}

export function createReportReceiptCommands(dependencies: Dependencies) {
    const { session, bindings } = dependencies;
    const workflow = createReceiptWorkflow({
        detailRequests: session.detailRequests,
        writes: session.receiptWrites,
        captureReadContext: () => session.listRequests.capture(),
        captureWriteContext: () => session.listRequests.capture(),
        open: (id, signal) => openReceiptApi(id, session.forgeryToken, signal),
        refresh: id => refreshReceiptApi(id, session.forgeryToken),
        showDetails: receipt => renderReceiptDetails(receipt, {
            detailsList: bindings.detailsList,
            modalHeader: bindings.modalHeader,
            modalTotal: bindings.modalTotal,
            bootstrapModal: bindings.bootstrapModal
        }, dependencies.formatNumber, dependencies.formatCurrency),
        applyRefresh: (card, receipt) => {
            updateCardFromDto(card, receipt, dependencies.formatCurrency);
            session.pageState.receipts = session.pageState.receipts.map(item => item.id === receipt.id ? receipt : item);
        },
        showError: message => alert(message)
    });

    return { openReceipt: workflow.open, refreshReceipt: workflow.refresh };
}
