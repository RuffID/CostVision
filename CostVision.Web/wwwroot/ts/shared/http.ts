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

export async function sendJsonRequest<TResponse = unknown>(url: string, method = "GET", headers: JsonHeaders = {}, body: JsonRequestBody = null): Promise<TResponse> {
    const options: RequestInit = {
        method: method,
        headers: headers
    };

    if (body !== null) {
        if (typeof body === "object" && !(body instanceof FormData)) {
            options.body = JSON.stringify(body);

            if (!headers["Content-Type"]) {
                headers["Content-Type"] = "application/json";
            }
        } else {
            options.body = body;
        }
    }

    const response = await fetch(url, options);

    if (!response.ok) {
        const errorText = await response.text();
        throw new Error(extractErrorMessage(errorText, response.status));
    }

    const text = await response.text();
    if (!text.trim())
        throw new Error("Сервер вернул пустой ответ.");

    try {
        return JSON.parse(text) as TResponse;
    }
    catch (error) {
        console.error("Failed to parse JSON", error);
        throw error;
    }
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

export function unwrapServiceResult<TData>(result: ServiceResultWithData<TData>): TData {
    if (result.success === false) {
        throw new Error(result.message);
    }

    return result.data;
}

export function unwrapServiceSuccess(result: ServiceResult): void {
    if (result.success === false) {
        throw new Error(result.message);
    }
}

function extractErrorMessage(text: string, status: number): string {
    if (!text.trim())
        return "HTTP error " + status;

    try {
        const parsed: unknown = JSON.parse(text);
        if (isServiceFailure(parsed))
            return parsed.message;
    }
    catch {
        return text;
    }

    return text;
}

function isServiceFailure(value: unknown): value is ServiceFailure {
    if (typeof value !== "object" || value === null)
        return false;

    const candidate = value as Record<string, unknown>;
    return candidate.success === false && typeof candidate.message === "string" && candidate.message.trim().length > 0;
}
