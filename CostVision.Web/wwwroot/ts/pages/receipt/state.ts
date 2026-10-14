import { createRequestCoordinator } from "../../shared/requestCoordinator.js";

export function createReceiptSession() {
    return {
        accounts: createRequestCoordinator(),
        runs: createRequestCoordinator(),
        active: true,
        busy: false,
        lastCameraText: null as string | null,
        lastCameraTime: 0,
        requiresReconciliation: false
    };
}
