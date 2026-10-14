# Регистрация инфраструктуры

## Ответственность

Extension связывает Application с SQL Server, адаптерами EFCoreLibrary, Options, HTTP, hashing, PDF, backup и hosted service.

## Контракты и границы

Регистрировать один scoped IUnitOfWorkScope и 14 узких adapters. AddEfCoreContext<ApplicationContext,CostVisionContextKey>, AddEfCoreBaseRepositories<CostVisionContextKey> и AddEfCoreSqlServerMaintenance<CostVisionContextKey>(SingleInitializer) используют один ApplicationContext/tracker. Старых дублирующих adapters, глобального UoW и собственных backup/checkup services нет.

DatabaseMaintenanceSettings привязывается здесь; server directory передаётся библиотечному immutable options при регистрации, без локальной файловой проверки. Изменение этой конфигурации требует перезапуска. Singleton DatabaseMaintenanceStartup получает scope factory, а не scoped context; EF logger factory выводит только EventCode. Регистрация/resolution не запускают maintenance/SQL/host. ConnectRetryCount и endpoint не исправлять скрытой нормализацией; unsupported configuration блокирует startup.

Не выполнять I/O при DI-регистрации. Сверять lifetime и ValidateOnStart; timeout HTTP 180 секунд не является timeout всего сценария.

## Проверка

Все поля DatabaseMaintenanceSettings захватываются одним снимком при регистрации, как immutable SqlServerMaintenanceOptions; reload не меняет startup operation/directory/budget отдельно от библиотечной поставки. ApiEndpoints допускает только абсолютный HTTP(S), ExternalRequests задаёт конечный положительный представимый HttpClient timeout (default180). BankStatementPdf задаёт согласованные byte/page limits (default20 МиБ/200). ValidateOnStart вызывается Web до maintenance; secret values и validation exception chains не выводить в startup-журнал.

Чистые преобразователи и модель проверяются в Infrastructure.UnitTests; SQL, backup и hosted-компоненты — отдельными интеграционными проверками с требуемым разрешением.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../Documentation/Plans/project-refactoring-2026-10-13/README.md).
