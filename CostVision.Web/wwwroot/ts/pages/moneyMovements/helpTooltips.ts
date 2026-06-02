import { renderHelpTooltip } from "../../shared/helpTooltip.js";
import type { MoneyMovementsUi } from "./ui.js";

export function initMoneyMovementHelpTooltips(ui: MoneyMovementsUi): void {
    renderHelpTooltip(ui.receiptFilterHelp, {
        title: "Связь с чеками",
        text: [
            "Все операции — без фильтра.",
            "Без чеков — операции без привязанных чеков.",
            "С чеками — операции с одним или несколькими чеками.",
            "Расхождение суммы — сумма привязанных чеков отличается от суммы операции."
        ]
    });
    renderHelpTooltip(ui.receiptsAmountToleranceHelp, {
        title: "Допуск суммы",
        text: "Разрешённая разница между суммой операции и суммой чека при поиске кандидатов."
    });
    renderHelpTooltip(ui.receiptsTimeWindowHoursHelp, {
        title: "Допуск времени",
        text: "Размер окна поиска по времени в часах до и после времени операции. Используется только при включённом окне времени."
    });
    renderHelpTooltip(ui.receiptsUseTimeWindowHelp, {
        title: "Окно времени",
        text: "Искать чеки в пределах указанного допуска времени до и после времени операции. По умолчанию выключено: банковская операция может пройти совсем в другое время, чем чек, и включённое окно времени может скрыть подходящий чек."
    });
    renderHelpTooltip(ui.receiptsUseAmountFilterHelp, {
        title: "По сумме",
        text: "Показывать только чеки, сумма которых близка к сумме операции с учётом допуска."
    });
    renderHelpTooltip(ui.receiptsExcludeLinkedHelp, {
        title: "Без привязанных",
        text: "Скрывать чеки, которые уже связаны с любой операцией. Отключите, если чек можно привязать повторно."
    });
}
