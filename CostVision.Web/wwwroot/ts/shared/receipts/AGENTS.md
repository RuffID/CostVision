# Общие клиентские контракты чека

## Ответственность

types содержит единственный ReceiptDto/items/accounts и AvailableAccountDto. api сохраняет относительные handlers open/refresh/move/remove/delete, payload, antiforgery и проверки HTTP09. workflow объединяет открытие/refresh и командный поток переноса/отвязки отчёта и магазинов.

## Контракты и границы

Страница явно передаёт доступный набор, транспорт, координатор details, Set записей, capture контекста чтения/записи и callbacks UI/server reload. Workflow не создаёт глобальное состояние, не расширяет права и не объединяет ReceiptId/identity/account IDs. Проверка актуальности после await обязательна. Изменяющий POST не отменяется и не повторяется; неизвестный исход сохраняет сообщение10 о сверке. Контекст finally подтверждения задаёт страница.

## Проверка

npm run build:ts; tests/Frontend/frontendDecomposition.test.mts и actual entry-point регрессии requestCoordination.test.mts с DOM/fetch. SQL/HTTP/provider/Bootstrap/browser/production приёмку эти проверки не закрывают.
