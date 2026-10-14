# Регистрация и адаптеры Web

## Ответственность

Extensions настраивают слои, cookie, Razor, proxy, Data Protection, startup-БД и получение пользователя; enricher обогащает журнал.

## Контракты и границы

DI, HTTP и startup имеют разные побочные эффекты. InitializeDatabaseAsync делегирует DatabaseMaintenanceStartup и связывает CancellationToken с временными Ctrl+C/SIGTERM подписками StartupCancellation до старта ConsoleLifetime; подписки освобождаются после awaited operation/cleanup. bool=false запрещает RunAsync хоста. Task.Run/внешний WaitAsync/Environment.Exit и собственный migration runner запрещены. Изменения startup проверять изолированными lifecycle тестами и моделированием сигнала, без запуска WebApplication/БД/OS signals.

## Проверка

Подходящие изолированные проверки находятся в tests/CostVision.Web.UnitTests. HTTP-хост, браузер и production pipeline проверяются отдельным разрешённым контуром.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).

PageRequestValidationExtensions проверяет ModelState/форму и не раскрывает диагностику binding. CookieHttpContract подключён actual AddCookie: JSON/AJAX challenge/forbid возвращают 401/403 success=false/message; HTML сохраняет redirect/lifetime. JSON определяется Accept, X-Requested-With либо Content-Type. Antiforgery не отключается.
