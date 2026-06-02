export function formatMoneyRub(value: unknown): string {
    const number = Number(value || 0);
    return number.toLocaleString("ru-RU", { style: "currency", currency: "RUB" });
}

export function formatRuNumber(value: unknown): string {
    const number = Number(value || 0);
    return number.toLocaleString("ru-RU", { maximumFractionDigits: 3 });
}

export function normalizeSingleLineTextValue(value: unknown): string {
    return String(value || "").replace(/\s+/g, " ").trim();
}
