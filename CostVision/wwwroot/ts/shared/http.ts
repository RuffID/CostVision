export async function sendJsonRequest(url: string, method = "GET", headers: Record<string, string> = {}, body: BodyInit | object | null = null): Promise<any> {
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
    if (!text || text.trim() === "") return {};

    try {
        return JSON.parse(text);
    }
    catch (error) {
        console.error("Failed to parse JSON", error);
        throw error;
    }
}

export function buildJsonHeaders(forgeryToken: string | null): Record<string, string> {
    let headers: Record<string, string> = {
        "Accept": "application/json",
        "Content-Type": "application/json"
    };

    if (forgeryToken) {
        headers["RequestVerificationToken"] = forgeryToken;
    }

    return headers;
}
