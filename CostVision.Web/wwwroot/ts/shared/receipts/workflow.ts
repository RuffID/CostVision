import { getWriteErrorMessage, isIntentionalAbort, type createRequestCoordinator } from "../requestCoordinator.js";
import type { ReceiptDto } from "./types.js";

interface ReceiptWorkflowOptions {
    detailRequests: ReturnType<typeof createRequestCoordinator>;
    writes: Set<string>;
    captureReadContext(): () => boolean;
    captureWriteContext(): () => boolean;
    open(receiptId: string, signal: AbortSignal): Promise<ReceiptDto>;
    refresh(receiptId: string): Promise<ReceiptDto>;
    showDetails(receipt: ReceiptDto): void;
    applyRefresh(card: HTMLElement, receipt: ReceiptDto): void;
    showError(message: string): void;
}

// Страница задаёт доступный набор и владельцев контекста. Здесь нет собственного store.
export function createReceiptWorkflow(options: ReceiptWorkflowOptions) {
    async function open(receiptId: string): Promise<void> {
        const request = options.detailRequests.begin(receiptId);
        const matchesContext = options.captureReadContext();
        try {
            const receipt = await options.open(request.parameters, request.signal);
            if (!request.isCurrent() || !matchesContext()) {
                return;
            }
            options.showDetails(receipt);
        } catch (error) {
            if (request.isCurrent() && matchesContext() && !isIntentionalAbort(error)) {
                options.showError(error instanceof Error ? error.message : "Не удалось выполнить операцию.");
            }
        }
    }

    async function refresh(receiptId: string, card: HTMLElement, button: HTMLButtonElement): Promise<void> {
        if (button.disabled || options.writes.has(receiptId)) {
            return;
        }
        options.writes.add(receiptId);
        const isCurrent = options.captureWriteContext();
        const originalText = button.textContent;
        button.disabled = true;
        button.textContent = "Обновление...";
        try {
            const receipt = await options.refresh(receiptId);
            if (isCurrent()) {
                options.applyRefresh(card, receipt);
            }
        } catch (error) {
            if (isCurrent()) {
                options.showError(getWriteErrorMessage(error));
            }
        } finally {
            options.writes.delete(receiptId);
            if (isCurrent()) {
                button.disabled = false;
                button.textContent = originalText;
            }
        }
    }

    return { open, refresh };
}

export type ReceiptAccountCommand = {
    kind: "move";
    receiptId: string;
    sourceAccountId: string;
    targetAccountId: string;
} | {
    kind: "remove";
    receiptId: string;
    accountId: string;
} | {
    kind: "delete";
    receiptId: string;
};

interface ReceiptAccountCommandOptions {
    command: ReceiptAccountCommand;
    writes: Set<string>;
    isCurrent(): boolean;
    send(command: ReceiptAccountCommand): Promise<void>;
    onStart(): void;
    onSuccess(): Promise<void>;
    onError(message: string): void;
    onFinally(): void;
}

// Не отменять POST и не повторять его. UI/reload и финальный контекст принадлежат странице.
export async function runReceiptAccountCommand(options: ReceiptAccountCommandOptions): Promise<void> {
    const command = { ...options.command };
    if (options.writes.has(command.receiptId)) {
        return;
    }
    options.writes.add(command.receiptId);
    try {
        options.onStart();
        await options.send(command);
        if (options.isCurrent()) {
            await options.onSuccess();
        }
    } catch (error) {
        if (options.isCurrent()) {
            options.onError(getWriteErrorMessage(error));
        }
    } finally {
        options.writes.delete(command.receiptId);
        options.onFinally();
    }
}
