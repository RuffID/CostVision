export function showAlertMessage(element: HTMLElement, message: string): void {
    element.textContent = message;
    element.classList.remove("d-none");
}

export function hideAlertMessage(element: HTMLElement): void {
    element.textContent = "";
    element.classList.add("d-none");
}
