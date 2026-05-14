export type JsonHeaders = Record<string, string>;

export type JsonRequestBody = BodyInit | object | null;

export interface ServiceResult {
    success: boolean;
    message?: string;
}

export interface ServiceResultWithData<TData> extends ServiceResult {
    data?: TData;
}

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

    let response = await fetch(url, options);

    if (!response.ok) {
        let errorText = await response.text();
        let message;

        try {
            const parsed = JSON.parse(errorText);
            message = (parsed && parsed.message) ? parsed.message : errorText;
        }
        catch {
            message = errorText || ("HTTP error " + response.status);
        }

        throw new Error(message);
    }

    let text = await response.text();
    if (!text || text.trim() === "") return {} as TResponse;

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

export function unwrapServiceResult<TData>(result: ServiceResultWithData<TData>): TData {
    if (!result.success) {
        throw new Error(result.message || "Ошибка выполнения запроса.");
    }

    return result.data as TData;
}

export function unwrapServiceSuccess(result: ServiceResult): void {
    if (!result.success) {
        throw new Error(result.message || "Ошибка выполнения запроса.");
    }
}
