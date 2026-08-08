import { buildJsonHeaders, sendJsonRequest, unwrapServiceSuccess, type ServiceResult } from "./http.js";
import { getRequestVerificationToken } from "./verificationToken.js";

const ACTIVITY_PING_INTERVAL_MS = 60000;

let antiForgeryToken: string | null = null;

document.addEventListener("DOMContentLoaded", () => {
    initUserActivityPing();
});

function initUserActivityPing(): void {
    antiForgeryToken = getRequestVerificationToken();
    void sendActivityPing();
    window.setInterval(() => {
        void sendActivityPing();
    }, ACTIVITY_PING_INTERVAL_MS);
}

async function sendActivityPing(): Promise<void> {
    try {
        const response = await sendJsonRequest<ServiceResult>("/user-activity?handler=Ping", "POST", buildJsonHeaders(antiForgeryToken));
        unwrapServiceSuccess(response);
    } catch (error) {
        console.error("Не удалось обновить последнюю активность пользователя.", error);
    }
}
