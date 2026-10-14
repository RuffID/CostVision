import { createReceiptsPage } from "./controller.js";

const page = createReceiptsPage();
document.addEventListener("DOMContentLoaded", () => {
    void page.init();
});
