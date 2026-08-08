import * as initialization from "./initialization.js";
import * as manualForm from "./manualForm.js";

document.addEventListener("DOMContentLoaded", () => {
    void initialization.initQrScan().catch((error: unknown) => {
        console.error("QR scan: ошибка инициализации страницы", error);
    });
    manualForm.initManualCheckValidation();
    initialization.initCollapseSections();
    initialization.initGlobalPasteHandler();
});

