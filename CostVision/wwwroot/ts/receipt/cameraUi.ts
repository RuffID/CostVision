export function setCameraButtonState(button: HTMLButtonElement | null, isDisabled: boolean): void {
    if (button) {
        button.disabled = isDisabled;
    }
}
