# Проверка базы и backup

## Ответственность

DatabaseMaintenanceStartup выбирает явный UpdateExisting либо InitializeNew, передаёт timeout/token единственному библиотечному coordinator и владеет async scope. SafeDatabaseLoggerFactory пересылает только EF event ID. Собственный SQL backup/checkup/path builder удалён.

## Контракты и границы

Не открывать SQL-соединения, не вызывать DbContext.Migrate и не создавать локальный backup-каталог. Backup receipt, target и migrations проверяет библиотека. Startup допускает host только после operation/cleanup/async disposal; отказ оставляет readiness=false и не допускает retry в том же процессе. Обычные логи содержат только закрытые коды, без путей, SQL, formatter/state и exception chains.

SingleInitializerConfirmed означает внешнюю предпосылку остановки других процессов/writes/DDL, а не распределённую блокировку. Неподтверждённый endpoint, серверный путь/права и active multi-instance deployment — эксплуатационные блокеры. Проверки только actual DLL с fake provider/migration/command boundaries в Infrastructure.UnitTests; SQL/restore/HTTP/hosting проверяются отдельно.

## Проверка

Чистые преобразователи и модель проверяются в Infrastructure.UnitTests; SQL, backup и hosted-компоненты — отдельными интеграционными проверками с требуемым разрешением.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).
