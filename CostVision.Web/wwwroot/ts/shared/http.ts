export type JsonHeaders = Record<string, string>;

export type JsonRequestBody = BodyInit | object | null;

export type ServiceFailure = {
    success: false;
    message: string;
};

export type ServiceResult = {
    success: true;
} | ServiceFailure;

export type ServiceResultWithData<TData> = {
    success: true;
    data: TData;
} | ServiceFailure;

const responseContexts = new WeakMap<object, { status: number; url: string }>();

export type HttpErrorKind = "http" | "contract" | "network" | "aborted";

export class HttpError extends Error {
    readonly kind: HttpErrorKind;
    readonly status: number | null;
    readonly url: string;

    constructor(message: string, kind: HttpErrorKind, status: number | null, url: string) {
        super(message);
        this.name = "HttpError";
        this.kind = kind;
        this.status = status;
        this.url = url;
    }
}

export async function sendJsonRequest<TResponse = unknown>(url: string, method = "GET", headers: JsonHeaders = {}, body: JsonRequestBody = null, signal?: AbortSignal): Promise<TResponse> {
    const requestHeaders: JsonHeaders = { "Accept": "application/json", ...headers };
    const options: RequestInit = {
        method: method,
        headers: requestHeaders,
        signal
    };

    if (body !== null) {
        if (isBodyInit(body)) {
            options.body = body;
        } else {
            options.body = JSON.stringify(body);

            if (!requestHeaders["Content-Type"]) {
                requestHeaders["Content-Type"] = "application/json";
            }
        }
    }

    let response: Response;
    let text: string;
    try {
        response = await fetch(url, options);
        text = await response.text();
    }
    catch {
        if (signal?.aborted) {
            throw new HttpError("Запрос отменён.", "aborted", null, url);
        }
        throw new HttpError("Не удалось связаться с сервером. Проверьте соединение.", "network", null, url);
    }

    if (!response.ok) {
        throw new HttpError(extractErrorMessage(text, response.status), "http", response.status, url);
    }
    if (!text.trim()) {
        throw new HttpError("Сервер вернул пустой ответ.", "contract", response.status, url);
    }

    let parsed: unknown;
    try {
        parsed = JSON.parse(text);
    }
    catch {
        if (response.redirected) {
            throw new HttpError("Сессия истекла. Войдите в систему снова.", "http", 401, url);
        }
        throw new HttpError("Сервер вернул ответ неверного формата.", "contract", response.status, url);
    }
    requireServiceResult(parsed, response.status, url);
    responseContexts.set(parsed, { status: response.status, url });
    if (parsed.success === false) {
        throw new HttpError(parsed.message, "contract", response.status, url);
    }
    return parsed as TResponse;
}

export function buildJsonHeaders(forgeryToken: string | null): JsonHeaders {
    const headers: JsonHeaders = {
        "Accept": "application/json",
        "Content-Type": "application/json"
    };

    if (forgeryToken) {
        headers["RequestVerificationToken"] = forgeryToken;
    }

    return headers;
}

export function buildFormHeaders(forgeryToken: string | null): JsonHeaders {
    const headers: JsonHeaders = {
        "Accept": "application/json"
    };

    if (forgeryToken) {
        headers["RequestVerificationToken"] = forgeryToken;
    }

    return headers;
}

export function unwrapServiceResult<TData>(result: ServiceResultWithData<TData>, validateData?: (value: unknown) => boolean): TData {
    requireServiceResult(result);
    const context = responseContexts.get(result);
    const status = context?.status ?? null;
    const url = context?.url ?? "";
    if (result.success === false) {
        throw new HttpError(result.message, "http", status, url);
    }
    if (!("data" in result) || result.data === null || result.data === undefined) {
        throw new HttpError("Сервер не передал обязательные данные ответа.", "contract", status, url);
    }
    if (validateData && !validateData(result.data)) {
        throw new HttpError("Сервер передал данные неверного формата.", "contract", status, url);
    }

    return result.data;
}

export function isResponseArray(value: unknown): boolean {
    return Array.isArray(value) && value.every(item => item !== null && item !== undefined);
}

export function hasResponseFields(value: unknown, arrays: readonly string[] = [], numbers: readonly string[] = [], strings: readonly string[] = [], objects: readonly string[] = []): value is Record<string, unknown> {
    if (typeof value !== "object" || value === null || Array.isArray(value)) {
        return false;
    }
    const data = value as Record<string, unknown>;
    return arrays.every(key => isResponseArray(data[key])) &&
        numbers.every(key => typeof data[key] === "number" && Number.isFinite(data[key])) &&
        strings.every(key => typeof data[key] === "string") &&
        objects.every(key => typeof data[key] === "object" && data[key] !== null && !Array.isArray(data[key]));
}

export function isPagedResponse(value: unknown, totals: readonly string[] = []): boolean {
    if (!hasResponseFields(value, ["items"], ["page", "pageSize", "totalCount", "totalPages", ...totals])) {
        return false;
    }
    const data = value as Record<string, unknown>;
    return typeof data.hasPreviousPage === "boolean" && typeof data.hasNextPage === "boolean" &&
        ["page", "pageSize", "totalPages"].every(key => Number.isInteger(data[key]) && Number(data[key]) >= 1) &&
        Number.isInteger(data.totalCount) && Number(data.totalCount) >= 0;
}

export function unwrapServiceSuccess(result: ServiceResult): void {
    requireServiceResult(result);
    if (result.success === false) {
        throw new HttpError(result.message, "http", null, "");
    }
}

function extractErrorMessage(text: string, status: number): string {
    try {
        const parsed: unknown = JSON.parse(text);
        if (isServiceFailure(parsed))
            return parsed.message;
    }
    catch {
        // HTML, framework diagnostics and proxy text are not user messages.
    }
    if (status === 401) {
        return "Сессия истекла. Войдите в систему снова.";
    }
    if (status === 403) {
        return "Недостаточно прав для выполнения операции.";
    }
    if (status === 400) {
        return "Некорректный запрос. Проверьте данные и обновите страницу.";
    }
    if (status === 413) {
        return "Превышен допустимый размер запроса.";
    }
    return "Не удалось выполнить запрос (HTTP " + status + ").";
}

function requireServiceResult(value: unknown, status: number | null = null, url = ""): asserts value is ServiceResult {
    if (typeof value !== "object" || value === null || Array.isArray(value)) {
        throw new HttpError("Сервер вернул ответ неверного формата.", "contract", status, url);
    }
    const candidate = value as Record<string, unknown>;
    if (candidate.success !== true && !isServiceFailure(value)) {
        throw new HttpError("Сервер вернул ответ неверного формата.", "contract", status, url);
    }
}

function isServiceFailure(value: unknown): value is ServiceFailure {
    if (typeof value !== "object" || value === null)
        return false;

    const candidate = value as Record<string, unknown>;
    return candidate.success === false && typeof candidate.message === "string" && candidate.message.trim().length > 0;
}

function isBodyInit(value: BodyInit | object): value is BodyInit {
    return typeof value === "string" ||
        value instanceof Blob ||
        value instanceof FormData ||
        value instanceof URLSearchParams ||
        value instanceof ArrayBuffer ||
        ArrayBuffer.isView(value) ||
        (typeof ReadableStream !== "undefined" && value instanceof ReadableStream);
}
