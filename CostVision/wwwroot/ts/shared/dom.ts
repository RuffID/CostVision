export function requireElementById<TElement extends HTMLElement>(id: string): TElement {
    const element = document.getElementById(id);

    if (!element) {
        throw new Error(`Не найден обязательный элемент: ${id}`);
    }

    return element as TElement;
}

export function requireQuerySelector<TElement extends Element>(selector: string, root: ParentNode = document): TElement {
    const element = root.querySelector(selector);

    if (!element) {
        throw new Error(`Не найден обязательный элемент: ${selector}`);
    }

    return element as TElement;
}

export function clearElement(element: Element): void {
    element.replaceChildren();
}

export function setElementVisible(element: HTMLElement, isVisible: boolean): void {
    element.classList.toggle("d-none", !isVisible);
}

export function setElementText(element: HTMLElement, text: string): void {
    element.textContent = text;
}
