import { buildJsonHeaders, sendJsonRequest, unwrapServiceResult, type ServiceResultWithData } from "./http.js";
import { getRequestVerificationToken } from "./verificationToken.js";

const ACTIVITY_PING_INTERVAL_MS = 60000;

type ActivityPingResponse = ServiceResultWithData<boolean>;

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
        const response = await sendJsonRequest<ActivityPingResponse>("/user-activity?handler=Ping", "POST", buildJsonHeaders(antiForgeryToken));
        unwrapServiceResult(response);
    } catch (error) {
        console.error("Не удалось обновить последнюю активность пользователя.", error);
    }
}
