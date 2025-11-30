async function sendJsonRequest(url, method = "GET", headers = {}, body = null) {
    const options = {
        method: method,
        headers: headers
    };

    if (body !== null) {
        if (typeof body === "object" && !(body instanceof FormData)) {
            options.body = JSON.stringify(body);

            if (!options.headers["Content-Type"]) {
                options.headers["Content-Type"] = "application/json";
            }
        } else {
            options.body = body;
        }
    }

    let response = await fetch(url, options);

    if (!response.ok) {
        let errorText = await response.text();
        throw new Error(errorText || ("HTTP error " + response.status));
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

function buildJsonHeaders(forgeryToken) {
    let headers = {
        "Accept": "application/json",
        "Content-Type": "application/json"
    };

    if (forgeryToken) {
        headers["RequestVerificationToken"] = forgeryToken;
    }

    return headers;
}