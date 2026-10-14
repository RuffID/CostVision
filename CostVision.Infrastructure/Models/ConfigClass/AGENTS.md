# Настройки внешнего API

## Ответственность

Options хранят имя секции, адрес сервиса чеков и токен. DatabaseMaintenanceSettings задаёт Operation, конечный TimeoutSeconds (1–3600), серверный BackupDirectory и явное SingleInitializerConfirmed; DatabaseStartupOperation различает UpdateExisting и InitializeNew без fallback.

## Контракты и границы

Не добавлять реальные значения в примеры. Изменение binding согласовать с регистрацией и приоритетами источников Program.cs.

ExternalRequestOptions.TimeoutSeconds default180 должен помещаться в HttpClient.Timeout и не округляться в нулевой TimeSpan. BankStatementPdfSettings MaxBytes/MaxPages положительны, defaults20 МиБ/200 согласованы пользователем; это только выписки, не decoder/camera/storage limits. DatabaseMaintenanceSettings и library directory — один registration snapshot, изменения требуют перезапуска.

## Проверка

Чистые преобразователи и модель проверяются в Infrastructure.UnitTests; SQL, backup и hosted-компоненты — отдельными интеграционными проверками с требуемым разрешением.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).
