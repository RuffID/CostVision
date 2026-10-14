# Карта тестовых контуров

## Ответственность

Application.UnitTests проверяет модели/сценарии; Infrastructure.UnitTests — чистые компоненты и metadata; Web.UnitTests — адаптеры; Infrastructure.IntegrationTests — SQL и составные границы.

Frontend содержит изолированные node-тесты HTTP/API, серверных итогов, навигации, импортного контроллера и actual точек входа dashboard/products/stores/reports/receipts/moneyMovements с подставным DOM/fetch. Они требуют предварительной сборки TypeScript, не запускают браузер и не заменяют его приёмку.

receiptScanning.test.mts добавляет actual AddReceipt entry/controller/API/storage/camera/resource boundary с подставным DOM/fetch/vendor; AddReceiptModelTests проверяет BrowserUser identity/no-store/binding без бизнес-вызова. Это изолированная проверка 12, не camera/HTTP/SQL acceptance.

## Контракты и границы

modalStack.test.mts вызывает actual shared Bootstrap/tooltip helpers на подставном DOM/events/timers/public Bootstrap 5.1 lifecycle; проверяет cancelled events, ownership, focus/Tab/Escape и teardown. Это не actual browser/native focus trap/animation acceptance. Прежние page-entry тесты сохраняют собственные проверки 10–12.

Выбирать минимальный подходящий контур. Не запускать всё дерево автоматически: наличие БД, Docker, hosting, сети или процессов требует отдельной просьбы пользователя.

## Проверка

ITestStorage и TestUseCaseFactory — общий только тестовый набор mocks и его разворачивание в actual узкие constructor-зависимости, linked в Application/Infrastructure.UnitTests. Production DI не регистрирует этот набор. Тестовые сценарные adapters через TestScope делегируют технические операции общему ITestStorage без собственного флага отказа; он не заменяет проверки производственного UnitOfWorkScope.

Не считать список тестов отчётом о выполнении. Результаты и непройденные контуры фиксировать раздельно.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../Documentation/Plans/project-refactoring-2026-10-13/README.md).

Frontend дополнительно проверяет production shared boundaries и независимые page sessions этапа 11. Полные isolated regressions запускаются после strict TS build; фактическое evidence и оставшаяся браузерная процедура находятся в журнале 11. Не подменять .NET/SQL/HTTP-приёмку количеством node-тестов.
