export function formatMoneyRub(value: unknown): string {
    const number = requireFiniteNumber(value);
    return number.toLocaleString("ru-RU", { style: "currency", currency: "RUB" });
}

export function formatRuNumber(value: unknown): string {
    const number = requireFiniteNumber(value);
    return number.toLocaleString("ru-RU", { maximumFractionDigits: 3 });
}

export function normalizeSingleLineTextValue(value: unknown): string {
    return String(value || "").replace(/\s+/g, " ").trim();
}

export function requireFiniteNumber(value: unknown): number {
    if (typeof value !== "number" || !Number.isFinite(value)) {
        throw new Error("Отсутствует обязательное конечное число.");
    }

    return value;
}
