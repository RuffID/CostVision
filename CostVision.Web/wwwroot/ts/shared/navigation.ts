import { requireElementById } from "./dom.js";

const NAVIGATION_EXPANDED_COOKIE_NAME = "mainNavigationExpanded";
const ONE_YEAR_IN_SECONDS = 31536000;
const MOBILE_NAVIGATION_MEDIA_QUERY = "(max-width: 991.98px)";

document.addEventListener("DOMContentLoaded", initMainNavigation);

function initMainNavigation(): void {
    const navigation = requireElementById<HTMLElement>("mainNavigation");
    const toggleButton = document.querySelector<HTMLButtonElement>("[data-main-navigation-toggle]");

    if (!toggleButton) {
        throw new Error("Не найдена кнопка управления основным меню.");
    }

    toggleButton.addEventListener("click", () => {
        const expanded = !navigation.classList.contains("main-navigation--expanded");
        setNavigationExpanded(navigation, toggleButton, expanded);
        saveNavigationExpandedState(expanded);
    });

    navigation.addEventListener("click", (event) => {
        if (!window.matchMedia(MOBILE_NAVIGATION_MEDIA_QUERY).matches || !(event.target instanceof Element) || !event.target.closest("a[href]")) {
            return;
        }

        setNavigationExpanded(navigation, toggleButton, false);
        saveNavigationExpandedState(false);
    });
}

function setNavigationExpanded(navigation: HTMLElement, toggleButton: HTMLButtonElement, expanded: boolean): void {
    navigation.classList.toggle("main-navigation--expanded", expanded);
    toggleButton.classList.toggle("main-navigation__toggle--expanded", expanded);
    toggleButton.textContent = expanded ? "‹" : "›";
    toggleButton.setAttribute("aria-label", expanded ? "Свернуть меню" : "Развернуть меню");
    toggleButton.setAttribute("aria-expanded", String(expanded));
}

function saveNavigationExpandedState(expanded: boolean): void {
    document.cookie = `${NAVIGATION_EXPANDED_COOKIE_NAME}=${expanded}; path=/; max-age=${ONE_YEAR_IN_SECONDS}; samesite=lax`;
}
