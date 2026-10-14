import { initQrScan } from "./initialization.js";

document.addEventListener("DOMContentLoaded", () => {
    void initQrScan().catch((error: unknown) => {
        const message = document.getElementById("receipt-page-message");
        if (message) {
            message.textContent = error instanceof Error ? error.message : "Не удалось инициализировать добавление чека.";
            message.classList.remove("d-none");
        }
        console.error("QR scan: ошибка инициализации страницы", error);
    });
});

