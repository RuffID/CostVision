import { createStoresPage } from "./controller.js";

const page = createStoresPage();
document.addEventListener("DOMContentLoaded", () => {
    void page.init();
});
