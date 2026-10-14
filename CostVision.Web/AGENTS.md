# Сборка web-приложения

## Ответственность

Program.cs загружает конфигурацию, настраивает журналирование и DI, запускает проверку БД и строит HTTP-конвейер.

До RunAsync хоста await-обслуживание 08A и async cleanup должны завершиться успешно. При отказе ExitCode=1, возврат без HTTP/hosted refresh и DisposeAsync WebApplication; Environment.Exit не использовать. Production readiness управляет DatabaseMaintenanceStartup; эксплуатационные предпосылки не заменять локальным gate.

## Контракты и границы

Сохранять порядок middleware и приоритет config.json. Не переносить use case в Program; запуск хоста и startup-БД требует отдельной просьбы.

PageModel операций вызывает конкретные Application-сценарии без общего page-фасада; освобождение HTTP file stream принадлежит Web, проверки preview и финансовые права — Application. Регистрация конкретных сценариев/общего scope сохраняется в layer extensions. Независимость Application от EF не предполагается: пользователь сохранил её явную EF/DLL-границу в 08.

Добавление чека 12 разделяет controller/session, decoding, camera, submission, DOM и персональный storage. Авторизованный AddReceipt BrowserUser handler выдаёт no-store CurrentUser.Id; сравнение identity дополняет серверную авторизацию. Его прямой unit test не доказывает cookie/routing/antiforgery. Реальная camera/iframe/CSP/Bootstrap/storage приёмка открыта.

## Проверка

Подходящие изолированные проверки находятся в tests/CostVision.Web.UnitTests. HTTP-хост, браузер и production pipeline проверяются отдельным разрешённым контуром.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../Documentation/Plans/project-refactoring-2026-10-13/README.md).

Единственный владелец неожиданных HTTP-ошибок — ExceptionHandlingMiddleware; production UseExceptionHandler удалён. Адаптеры проверяют форму до Application, cookie HTTP-контракт находится в Extensions. Startup-БД не запускать для проверки.

Frontend dashboard/stores/receipt report/moneyMovements разделён на entry/controller/state/bindings/render и отдельные команды/окна. Общие календарь/preferences/paging/receipt workflow находятся в wwwroot/ts/shared; backend/handlers/схема не менялись в этапе 11. Strict build и подставные DOM/fetch тесты не закрывают browser/HTTP acceptance.

Shared Bootstrap adapter 13 владеет stack/backdrop/focus и отложенным dispose; callers сохраняют собственные locks/revisions. Layout объявляет русский язык, модалки — связанные заголовки. Реальная браузерная доступность/анимация проверяется отдельно от Razor/TS compile-check.
