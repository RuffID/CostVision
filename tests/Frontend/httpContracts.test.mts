import { test } from "node:test";
import assert from "node:assert/strict";
import { sendJsonRequest, unwrapServiceResult, unwrapServiceSuccess, HttpError, buildFormHeaders } from "../../CostVision.Web/wwwroot/dist/shared/http.js";
import { loadMoneyMovements } from "../../CostVision.Web/wwwroot/dist/pages/moneyMovements/api.js";
import { loadAvailableAccountsApi } from "../../CostVision.Web/wwwroot/dist/pages/reports/receipts/api.js";

test("helper forwards AbortSignal, antiforgery, JSON and multipart without overriding boundary", async () => {
    // Arrange
    const original = globalThis.fetch;
    const calls: RequestInit[] = [];
    globalThis.fetch = async (_url, options) => {
        calls.push(options!);
        return new Response(JSON.stringify({ success: true }));
    };
    const controller = new AbortController();
    const form = new FormData();
    form.set("file", new Blob(["pdf"]), "statement.pdf");
    try {
        // Act
        await sendJsonRequest("/json", "POST", { RequestVerificationToken: "token" }, { amount: 100 }, controller.signal);
        await sendJsonRequest("/form", "POST", buildFormHeaders("token"), form, controller.signal);

        // Assert
        assert.equal(calls[0].signal, controller.signal);
        assert.equal(calls[0].body, '{"amount":100}');
        assert.equal((calls[0].headers as Record<string, string>)["Accept"], "application/json");
        assert.equal((calls[0].headers as Record<string, string>)["RequestVerificationToken"], "token");
        assert.equal(calls[1].body, form);
        assert.equal((calls[1].headers as Record<string, string>)["Content-Type"], undefined);
    } finally { globalThis.fetch = original; }
});

for (const status of [400, 401, 403, 409, 413, 500, 502, 504]) {
    test(`HTTP ${status} preserves structured status and hides HTML diagnostics`, async () => {
        // Arrange
        const original = globalThis.fetch;
        globalThis.fetch = async () => new Response("<html>secret SQL diagnostic</html>", { status });
        try {
            // Act
            const error = await sendJsonRequest("/request").catch(value => value);

            // Assert
            assert.ok(error instanceof HttpError);
            assert.equal(error.kind, "http");
            assert.equal(error.status, status);
            assert.equal(error.url, "/request");
            assert.doesNotMatch(error.message, /secret|html|SQL/);
        } finally { globalThis.fetch = original; }
    });
}

test("valid error envelope retains server message for Conflict", async () => {
    // Arrange
    const original = globalThis.fetch;
    globalThis.fetch = async () => new Response(JSON.stringify({ success: false, message: "Конкурентный импорт отменён." }), { status: 409 });
    try {
        // Act
        const error = await sendJsonRequest("/import").catch(value => value);

        // Assert
        assert.ok(error instanceof HttpError);
        assert.equal(error.status, 409);
        assert.equal(error.message, "Конкурентный импорт отменён.");
    } finally { globalThis.fetch = original; }
});

test("legacy cookie HTML redirect reports expired session without displaying login markup", async () => {
    // Arrange
    const original = globalThis.fetch;
    globalThis.fetch = async () => {
        const response = new Response("<html>login secret</html>");
        Object.defineProperty(response, "redirected", { value: true });
        return response;
    };
    try {
        // Act
        const error = await sendJsonRequest("/receipts").catch(value => value);

        // Assert
        assert.ok(error instanceof HttpError);
        assert.equal(error.status, 401);
        assert.match(error.message, /Сессия истекла/);
        assert.doesNotMatch(error.message, /secret|html|login/);
    } finally { globalThis.fetch = original; }
});

for (const payload of ["", "<html>secret</html>", "{}", "null", "[]", '{"success":"true"}', '{"success":false}', '{"success":false,"message":""}']) {
    test(`malformed successful response is rejected: ${payload}`, async () => {
        // Arrange
        const original = globalThis.fetch;
        globalThis.fetch = async () => new Response(payload);
        try {
            // Act
            const error = await sendJsonRequest("/request").catch(value => value);

            // Assert
            assert.ok(error instanceof HttpError);
            assert.equal(error.kind, "contract");
            assert.equal(error.status, 200);
            assert.doesNotMatch(error.message, /secret|html/);
        } finally { globalThis.fetch = original; }
    });
}

test("actual consumers reject missing data without empty success or zero totals", async () => {
    // Arrange
    const original = globalThis.fetch;
    globalThis.fetch = async () => new Response(JSON.stringify({ success: true }));
    try {
        // Act
        const movementError = await loadMoneyMovements(null, "2026-01-01", "2026-01-31", "").catch(value => value);
        const accountsError = await loadAvailableAccountsApi(null).catch(value => value);

        // Assert
        assert.ok(movementError instanceof HttpError);
        assert.equal(movementError.kind, "contract");
        assert.equal(movementError.status, 200);
        assert.match(movementError.url, /handler=List/);
        assert.ok(accountsError instanceof HttpError);
        assert.equal(accountsError.kind, "contract");
    } finally { globalThis.fetch = original; }
});

test("actual page rejects missing totals and invalid collections instead of inventing data", async () => {
    // Arrange
    const original = globalThis.fetch;
    globalThis.fetch = async () => new Response(JSON.stringify({ success: true, data: {
        items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 1, hasNextPage: false, hasPreviousPage: false
    } }));
    try {
        // Act
        const totalsError = await loadMoneyMovements(null, "2026-01-01", "2026-01-31", "").catch(value => value);

        // Assert
        assert.ok(totalsError instanceof HttpError);
        assert.equal(totalsError.kind, "contract");

        // Arrange
        globalThis.fetch = async () => new Response(JSON.stringify({ success: true, data: {} }));

        // Act
        const arrayError = await loadAvailableAccountsApi(null).catch(value => value);

        // Assert
        assert.ok(arrayError instanceof HttpError);
        assert.equal(arrayError.kind, "contract");
    } finally { globalThis.fetch = original; }
});

test("unwrap keeps valid empty collections and boolean false, requires data and envelope", () => {
    // Arrange
    const empty = { success: true as const, data: [] };
    const falseValue = { success: true as const, data: false };

    // Act
    const items = unwrapServiceResult(empty);
    const boolean = unwrapServiceResult(falseValue);

    // Assert
    assert.deepEqual(items, []);
    assert.equal(boolean, false);
    assert.throws(() => unwrapServiceResult({ success: true, data: null }), HttpError);
    assert.throws(() => unwrapServiceSuccess({ success: "true" } as never), HttpError);
});

test("abort during fetch and response stream is structured separately from network failure", async () => {
    // Arrange
    const original = globalThis.fetch;
    const controller = new AbortController();
    globalThis.fetch = async () => {
        controller.abort();
        throw new DOMException("Aborted", "AbortError");
    };
    try {
        // Act
        const aborted = await sendJsonRequest("/request", "GET", {}, null, controller.signal).catch(value => value);

        // Assert
        assert.ok(aborted instanceof HttpError);
        assert.equal(aborted.kind, "aborted");
        assert.equal(aborted.status, null);

        // Arrange
        const streamController = new AbortController();
        globalThis.fetch = async () => new Response(new ReadableStream({
            start(stream) {
                streamController.abort();
                stream.error(new DOMException("Aborted", "AbortError"));
            }
        }));

        // Act
        const streamError = await sendJsonRequest("/request", "GET", {}, null, streamController.signal).catch(value => value);

        // Assert
        assert.equal(streamError.kind, "aborted");

        // Arrange
        globalThis.fetch = async () => { throw new TypeError("Network secret"); };

        // Act
        const network = await sendJsonRequest("/request").catch(value => value);

        // Assert
        assert.equal(network.kind, "network");
        assert.doesNotMatch(network.message, /secret/);
    } finally { globalThis.fetch = original; }
});
