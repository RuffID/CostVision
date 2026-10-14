import { createDashboardPage } from "./controller.js";

const page = createDashboardPage();
document.addEventListener("DOMContentLoaded", () => {
    page.init().catch(error => console.error(error));
});
