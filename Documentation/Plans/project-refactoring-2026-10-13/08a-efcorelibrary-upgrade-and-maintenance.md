# Этап 08A. Обновление EFCoreLibrary и единый доступ к БД

Статус: **Не начат**.
Дата подготовки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 07, 07A, 08.

## Проверенная исходная ситуация

Исходники для перехода: `D:\Media\User\source\repos\work\EFCoreLibrary`. Проверка выполнена чтением исходников, project references и metadata файлов, без сборки библиотеки, выполнения Git-команд или подключения к БД.

| Область | Сейчас в CostVision | В указанных исходниках |
|---|---|---|
| Базовая библиотека | `libs/EFCoreLibrary.dll`, FileVersion `0.0.2.0`, ProductVersion `0.0.2+fc8acdd067bf9e58e5ece4e3aa28313ddad9be84`, SHA-256 `64A6540852CFD211D48DDE6DF09F3F0A5579C6EABC90E7C26095BFE897E5AB17` | `EFCoreLibrary.csproj`: net10.0, Version `0.0.5` |
| EF-зависимости | EF Core/Relational/SqlServer/Tools и Web Design `10.0.10`; compiled EF-reference старой DLL описан в `libs/README.md` | Базовый проект: EF Core и DI.Abstractions `10.0.12`; Maintenance: Relational и Logging.Abstractions `10.0.12`; Maintenance.SqlServer: SqlClient `7.0.2` |
| Repository API | `ICreateItemRepository<TEntity, TContext>`, `IGetItemByPredicateRepository<TEntity, TContext>` и другие старые контекстные интерфейсы | Application-facing интерфейсы без context key и `IContext*Repository<TEntity, TContextKey>` для Infrastructure; marker key с `notnull` |
| DI и сохранение | Два вручную зарегистрированных `EfDbContextAdapter` над одним ApplicationContext, `IAppDbContext` в техническом scope | `AddEfCoreContext<TContext,TContextKey>()`, `AddEfCoreBaseRepositories<TContextKey>()`, `IUnitOfWorkContext<TContextKey>.SaveChangesAsync/Database` |
| Обслуживание БД | Собственные BackupService и DataBaseCheckUpService | Отдельные `EFCoreLibrary.Maintenance` и `EFCoreLibrary.Maintenance.SqlServer`, `IDatabaseMaintenance<TContextKey>` |

DLL устарела относительно предоставленных исходников; замена одного файла несовместима с текущими generic-контрактами. Существующая `bin/Debug/net10.0/EFCoreLibrary.dll` upstream имеет FileVersion `0.0.5.0`, но её соответствие текущим исходникам и полная воспроизводимость не проверены. ProductVersion не заменяет проверку исходной ревизии. README upstream местами ещё указывает maintenance EF `10.0.11` и SqlClient `6.1.6`; целевой граф определять по фактическим `.csproj`, исходникам и проверенной поставке.

## Найденные точки перехода и обходы

1. [InfrastructureServiceCollectionExtensions](../../../CostVision.Infrastructure/Extensions/InfrastructureServiceCollectionExtensions.cs) регистрирует старые adapters/base repositories, собственный backup и checkup. Domain, Application, Infrastructure, Web и три тестовых проекта имеют прямые HintPath к старой DLL; весь граф нужно обновить согласованно.
2. [ProjectedReadQuery](../../../CostVision.Infrastructure/DataBase/Repositories/ProjectedReadQuery.cs) уже исполняет общую projection через библиотечный Query. Сохранить этот путь, адаптировав тип к новой поставке. [ProductRepository](../../../CostVision.Infrastructure/DataBase/Repositories/Receipts/ProductRepository.cs) и [StoreRepository](../../../CostVision.Infrastructure/DataBase/Repositories/Receipts/StoreRepository.cs) считают записи через `IAppDbContext.Set`, минуя query repository; перевести эти чтения на библиотечный query/projection с тем же predicate.
3. [UnitOfWorkScope](../../../CostVision.Infrastructure/DataBase/Repositories/UnitOfWorkScope.cs) использует библиотечный context, но старый широкий контракт. Перевести технические SaveChanges/Database на узкий библиотечный `IUnitOfWorkContext`; не переносить бизнес-сценарии в библиотеку.
4. [BackupService](../../../CostVision.Infrastructure/Services/DataBase/BackupService.cs) напрямую открывает `SqlConnection`, выполняет собственный BACKUP SQL и создаёт локальную папку вместо явного server-side контракта. Это независимый обход библиотечной границы.
5. [DataBaseCheckUpService](../../../CostVision.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs) получает DatabaseFacade через библиотечный adapter, но самостоятельно координирует `CanConnect/GetPendingMigrations/backup/Migrate`. Новый maintenance coordinator должен стать единственным production-владельцем этого сценария.
6. [WebApplicationExtensions](../../../CostVision.Web/Web/Extensions/WebApplicationExtensions.cs) использует `Task.Run` + `WaitAsync(60 секунд)` + `Environment.Exit`; окончание ожидания не подтверждает завершение SQL и cleanup. Этот вызов заменить явным async maintenance lifecycle.
7. [AddReceiptRowVersion](../../../CostVision.Infrastructure/DataBase/Migrations/20261014121704_AddReceiptRowVersion.cs) уже присутствует в исходниках и добавляет RowVersion/RefreshRevision. Факт применения к БД не проверен; не создавать эту migration повторно и не сбрасывать существующую историю.

## План работ

1. Зафиксировать воспроизводимую поставку базовой DLL, Maintenance и Maintenance.SqlServer из указанных исходников: ревизия либо digest исходного набора, build-команда, версии, SHA-256, XML-документация и runtime dependencies. Не брать произвольную DLL из bin/artifacts. Соседнюю библиотеку не изменять в рамках CostVision. Согласовать единый EF Core `10.0.12` граф runtime/provider/Relational/Design/Tools и SqlClient `7.0.2` с проверенной поставкой; upstream абсолютный путь использовать как источник подготовки поставки, не как production HintPath.
2. Обновить все production/test references и библиотечные contracts/adapters. Использовать один marker key в Infrastructure, `AddEfCoreContext<ApplicationContext,TKey>()` и `AddEfCoreBaseRepositories<TKey>()`; все repository/UoW/maintenance зависимости связывать с тем же scoped ApplicationContext. Убрать старые дублирующие регистрации. Domain продолжает использовать фактический IEntity; его независимость от всей EF DLL не объявлять.
3. Сохранить принятое [решение А этапа 08](08-boundary-decision.md): EF/predicate/include/tracking и `GetProjectedItemsAsync<TResult>` остаются. Application repository-интерфейсы могут перейти на новые базовые интерфейсы без context key; это адаптация API, а не разрешение на независимый query-язык или массовый перенос AppDbContextBase. Infrastructure использует конкретные `IContext*Repository` для CRUD/query, `Query(true)` для read projection; ни specialized Get...With, ни загрузка всех строк для count/page не допускаются.
4. Сохранить 14 узких сценарных UoW, один технический scope, общий tracker/флаг отказа и одного владельца составной транзакции. Перегрузка ExecuteInTransaction без isolation сохраняет после action, перегрузка с isolation требует явного SaveChanges владельцем. Сохранить Serializable, классификацию конфликтов/отказов, запрет повторного использования отказавшего scope и отсутствие скрытого retry. Библиотечная очистка tracker не должна автоматически делать такой scope пригодным для повторной записи.
5. Подключить только Maintenance и Maintenance.SqlServer; зарегистрировать `AddEfCoreSqlServerMaintenance<TKey>(SqlServerMaintenanceOptions, MaintenanceExecutionMode.SingleInitializer)` и вызывать `IDatabaseMaintenance<TKey>`. Регистрация ничего не запускает. `InspectAsync` проверяет состояние, `UpdateExistingAsync` обновляет существующую БД после подтверждённого backup, `InitializeNewAsync` выбирается явно только для первой установки. Ошибка подключения/прав не означает Missing и не запускает создание БД автоматически.
6. Заменить собственный backup/checkup и их DI-потребителей библиотечным coordinator. Удалить недействующий SQL-путь и тесты, закрепляющие его, сохранив проверки наблюдаемого контракта. Для существующей БД сохранить backup-before-migrate: нет pending → Unchanged без backup; есть pending → проверка target, backup receipt, повторная проверка target, migrations, отсутствие pending. Путь задаётся `SqlServerMaintenanceOptions.ServerBackupDirectory` на SQL Server; локальное создание каталога приложения не подтверждает доступность этого пути.
7. Описать SQL Server предпосылки: поддержанные EngineEdition 2/3/4, стабильный прямой endpoint, `ConnectRetryCount=0`, без routing/failover/reconnect и повторяющей EF execution strategy/внешней транзакции. Подтверждение backup внутри библиотеки — COPY_ONLY/CHECKSUM, подходящий HEADERONLY set и VERIFYONLY. Receipt не доказывает успешное восстановление; автоматического restore API нет.
8. Явно обеспечить SingleInitializer: другие экземпляры, фоновые/HTTP writes и DDL остановлены на всё обслуживание; singleton gate локален одному root DI container и не является распределённой блокировкой. Приложение выбирает режим startup/deployment и границы readiness до допуска запросов и фонового refresh; неподдержанный контур считать блокером, не обходить библиотеку своим SQL или вторым runner.
9. Передавать конечный timeout и CancellationToken в библиотеку, ожидать завершения операции/cleanup и сохранять fail-fast. Не заменять это внешним WaitAsync, скрытым fallback или повтором после неопределённого исхода. Maintenance-логи не должны раскрывать connection string, пути, provider messages или цепочки исключений; обычный лог получает безопасные коды/стадии.
10. Сохранить ApplicationContext, migrations assembly и историю: сверить текущую модель/snapshot/designer, provider и design-time workflow. Миграции создаёт пользователь; смена DLL сама по себе не требует новой migration. Если действительно меняется модель, описать необходимую пользовательскую migration отдельно. Не редактировать generated migrations/snapshot и не вызывать Migrate напрямую из другого production-сервиса.
11. Актуализировать `libs/README.md`, решение/проверки границы 08 и локальные AGENTS владельцев изменённого кода; после реализации обновить TechnicalGuide о persistence, startup и эксплуатации. Этап 14 использует уже подключённый maintenance и завершает общие options/configuration; этап 15 закрепляет поставку/CI/CD и итоговую приёмку.

## Правило единого доступа к БД

Business CRUD/query выполняются через EFCoreLibrary repositories; сохранение и транзакции — через сценарный UoW и общий scope над библиотечным IUnitOfWorkContext. Maintenance inspection/backup/create/migrate принадлежат EFCoreLibrary.Maintenance.SqlServer и общему coordinator. EF model configuration, provider registration и пользовательские generated migrations остаются ответственностью приложения и не являются обходом.

Запретить production `new SqlConnection/SqlCommand`, ручной SQL/Dapper, прямые DbContext/Set/SaveChanges из use case/PageModel/background consumers и самостоятельную координацию Migrate/backup. Library DatabaseFacade допускается только внутри утверждённого технического scope для транзакций; query-композиция Application сохраняется. Дополнительные подключения внутри библиотечного maintenance provider допустимы по его контракту, но не дают потребителю права открывать свои подключения. Недостающую возможность библиотеки фиксировать как блокер и сообщать пользователю; не создавать несогласованный обход.

## Проверки и приёмка

- [ ] Поставка base/Maintenance/SqlServer воспроизводима; metadata/хеши/зависимости и все production/test HintPath согласованы, старые generic-контракты удалены.
- [ ] Actual DLL/DI проверки подтверждают один ApplicationContext/tracker, контекстные CRUD/query, узкий UoW и maintenance без запуска SQL или host.
- [ ] Regression 01–10/07A/08 сохраняет доступ, идентичность, суммы, bounded reads, predicate/include/defaults/token, RowVersion/RefreshRevision и обе транзакционные перегрузки; assertions не ослаблены ради новой DLL.
- [ ] Изолированно проверены no-pending, backup-before-migrate, отказ/неверный receipt/target → запрет migrations, явный InitializeNew, cancellation/timeout/неопределённый исход → fail-fast без retry, безопасные логи и отсутствие вызовов при DI-регистрации.
- [ ] Статический аудит всех production consumers подтверждает отсутствие собственных SQL-подключений, прямых бизнес-запросов через context и второго migration/backup runner; разрешённые границы явно перечислены.
- [ ] Изолированные compile-check/test выполнены для конкретных затронутых проектов по AGENTS; библиотечный base build использует `GeneratePackageOnBuild=false`. Не запускать автоматически upstream-скрипты, host, сеть, DB/Docker или процессы.
- [ ] Отдельно с разрешением пользователя проверены SQL Server версия/права/server backup path, существующая БД с pending/без pending, receipt и реальное восстановление оператором, новая БД, отказ/отмена и SingleInitializer с несколькими экземплярами. Fake connection не закрывает эти критерии.
- [ ] Сохранена история migrations; применение AddReceiptRowVersion и другой схемы подтверждено отдельной DB-приёмкой, а не наличием файла.
- [ ] Статусы прежних этапов и открытая SQL/HTTP/browser/provider/production-приёмка сохранены; эксплуатационная policy и оставшиеся блокеры документированы.

## Журнал подготовки

14.10.2026: проверены текущая DLL, project references, указанные исходники base/maintenance и production-вызовы CostVision. Добавлен этап и зависимости 14–15. Код, DLL, packages и migrations не изменены; build/test/Git/SQL/backup/host/deployment не запускались. Это подготовка плана, не реализация 08A.

Этап 11 уже в работе по отдельному поручению; frontend-этапы 11–13 не заменяют backend adapters и не требуют новой зависимости только из-за поставки DLL. Их HTTP-контракты и открытая приёмка сохраняются; backend-регрессии после обновления входят в 08A и 15.
