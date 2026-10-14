import { createMoneyMovementsPage } from "./controller.js";

const page = createMoneyMovementsPage();
document.addEventListener("DOMContentLoaded", () => {
    void page.init();
});
