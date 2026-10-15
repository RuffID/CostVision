# Проверки EF-модели без рабочей БД

## Ответственность

Тесты проверяют metadata конфигураций пользователей, счетов, чеков и операций.

## Контракты и границы

ScenarioUnitOfWorkScopeTests выполняет actual AddApplication/AddInfrastructure/DLL без hosting: все узкие adapters, общий tracker, независимые scopes, token/owner/isolation, auto/explicit Save, промежуточный ID, rollback и fail-fast через другую обёртку. RecordingContext и DbConnection/DbTransaction подставные: generated ID и Save наблюдаются без SQL. Перехваченный отказ и конкурирующая/вложенная транзакция не допускают commit. Эти результаты не доказывают реальный SQL rollback, EF fixup или отсутствие гонки.

Это проверка построенной модели, не миграции и не фактической уникальности SQL. Изменение relationship согласовать с интеграционными сценариями.

ExternalLibraryContractTests закрепляет принятый вариант А: actual DbContext-base, DLL CRUD без key, Expression/IQueryable и generic projection остаются в Application. Проверяет IEntity getter/setter, IUnitOfWorkContext Database/SaveChangesAsync и generic HTTP Post/token; metadata принятых DLL выводит в TRX. Reflection используется только для inspection в тесте, не в production. Ни независимость Domain от всей EF-сборки, ни live SQL/HTTP-совместимость этим не доказываются.

ContextRepositoryAndMaintenanceLibraryTests разрешает actual DLL CRUD/query/UoW/SQL Server maintenance с CostVisionContextKey и одним tracker/connection, проверяет отсутствие I/O при DI, историю/HasPendingModelChanges, SqlClient reconnect запреты, 4060/права/отсутствие БД, HEADERONLY/VERIFYONLY и безопасный EF logger. Все команды подставные: реальное соединение остаётся закрытым. UnitOfWorkTransactionFailureTests предоставляет internal factory синтетических SqlException только тестам.

DatabaseMaintenanceCoordinatorTests исполняет actual coordinator на fake provider/migration/pin: no-pending, backup-before-migrate, invalid receipt/target, explicit init, timeout/cancellation, await cleanup и poisoned gate. DatabaseMaintenanceStartupTests проверяет operation/timeout/token, конфигурационные блокеры, readiness после DisposeAsync, отмену, отказ и запрет retry/unsafe logs. Эти тесты заменяют assertions удалённого собственного SQL backup/checkup, а не SQL/restore/production-приёмку.

MoneyMovementImportPersistenceTests подтверждает отсутствие уникального импортного ключа, составной PK денежной связи и precision/длину полей. Реальный предикат ImportMoneyMovements переводится через ToQueryString в ru-RU/en-US без выполнения SQL. Эти проверки не подтверждают диапазонные блокировки, фактическую схему, колляцию или отсутствие конкурентного дубля.

ReceiptIdentityPersistenceTests проверяет фактические author index/PK, SQL-перевод идентичности через ToQueryString без выполнения и узкое распознавание имён ограничений. UnitOfWorkTransactionFailureTests использует реальный DatabaseFacade и подставные DbConnection/DbTransaction: подтверждает передачу изоляции и обработку отказа, не отсутствие гонок на SQL Server. Открытая SQL-приёмка не закрывается этими тестами.

Синтетические SqlException создаются в тесте через внутренние конструкторы установленного SqlClient; при изменении версии проверять их сигнатуры. Это проверка классификации/rollback без SQL, а не доказательство фактических сообщений сервера или конкурентных исходов.

ProjectedReadQueryTranslationTests проверяет фактический API DLL и перевод root filters/count/totals/Skip/Take/групп всех затронутых чтений через SQL Server ToQueryString без соединения. ProjectedQueryCapture материализует только LINQ samples. ProjectedReadRepositoryExecutionTests исполняет общий adapter через настоящую DI/DLL на подставном DbConnection, проверяя форму COUNT/страницы, тип результата, отмену и отсутствие tracking. Эти проверки не подтверждают реальные планы, строки, snapshot, collation или производительность.

## Проверка

ReceiptRefreshPersistenceTests проверяет RowVersion/RefreshRevision metadata и SQL-перевод actual пакетного predicate/order/take/Guid cursor без соединения. Actual AddApplication/AddInfrastructure и поставленные DLL разрешаются в независимых scopes с разными ApplicationContext/UoW; hosting не запускается. Отдельный тест actual DLL выполняет пакетный запрос на подставном DbConnection и проверяет ORDER BY/TOP(32) без открытия реального соединения. Известные normalized-индексы распознаются отдельно от unknown/AdaptiveName; UnitOfWorkTransactionFailureTests проверяет их перевод/rollback синтетическими SqlException. Это не применённая миграция или проверка конкуренции на двух соединениях.

FinancialReportQueryTranslationTests передаёт actual predicate/include dashboard, getter кандидатов, отчётного счётчика и document projection деталей/refresh в SQL Server ToQueryString. Соединение не открывается, SQL не исполняется. Проверки перевода и наличия графа не доказывают фактическую материализацию, precision/collation, snapshot агрегатов или отзыв доступа.

Проверять тестовые утверждения и границы их изоляции; запуск выбирать по правилам родительского AGENTS.md. Не ослаблять ожидания и не сообщать о прохождении без выполнения.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).
