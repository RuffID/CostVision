import { HttpError } from "./http.js";

export interface RequestTicket<T> {
    readonly parameters: T;
    readonly signal: AbortSignal;
    isCurrent(): boolean;
}

// Один экземпляр принадлежит одной области UI. Abort не гарантирует остановку ответа.
export function createRequestCoordinator(): {
    begin<T>(parameters: T): RequestTicket<T>;
    invalidate(): void;
    capture(): () => boolean;
} {
    let generation = 0;
    let controller: AbortController | null = null;

    function invalidate(): void {
        generation++;
        controller?.abort();
        controller = null;
    }

    function capture(): () => boolean {
        const capturedGeneration = generation;
        return () => capturedGeneration === generation;
    }

    return {
        invalidate,
        capture,
        begin<T>(parameters: T): RequestTicket<T> {
            invalidate();
            const current = new AbortController();
            controller = current;
            const matchesGeneration = capture();
            return {
                parameters,
                signal: current.signal,
                isCurrent: () => matchesGeneration() && !current.signal.aborted
            };
        }
    };
}

export function isIntentionalAbort(error: unknown): boolean {
    return error instanceof HttpError && error.kind === "aborted";
}

export function isUnknownWriteOutcome(error: unknown): boolean {
    return error instanceof HttpError && (error.kind === "network" || error.kind === "aborted"
        || error.kind === "contract" || (error.status !== null && error.status >= 500));
}

export function getWriteErrorMessage(error: unknown): string {
    if (isUnknownWriteOutcome(error)) {
        return "Результат записи неизвестен. Обновите список и проверьте данные перед повтором действия.";
    }
    return error instanceof Error ? error.message : "Не удалось выполнить операцию.";
}
