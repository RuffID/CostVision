# Этап 08A. Обновление EFCoreLibrary и единый доступ к БД

Статус: **Реализован, проверка не завершена**.
Дата подготовки: 14.10.2026.
Дата реализации и изолированной проверки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 07, 07A, 08.

## Проверенная исходная ситуация

Этот раздел фиксирует исходное состояние до реализации. Текущая поставка, изменения и результаты описаны в журнале выполнения ниже и [libs/README.md](../../../libs/README.md).

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
4. Исторический `BackupService` напрямую открывал `SqlConnection`, выполнял собственный BACKUP SQL и создавал локальную папку вместо явного server-side контракта. В 08A этот независимый обход удалён.
5. Исторический `DataBaseCheckUpService` получал DatabaseFacade через библиотечный adapter, но самостоятельно координировал `CanConnect/GetPendingMigrations/backup/Migrate`. В 08A он удалён; библиотечный coordinator стал единственным production-владельцем этого сценария.
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

- [x] Поставка base/Maintenance/SqlServer воспроизводима; metadata/хеши/зависимости и все production/test HintPath согласованы, старые generic-контракты удалены. Повторный Build дал те же DLL/XML SHA-256; source digest сохранён.
- [x] Actual DLL/DI проверки подтверждают один ApplicationContext/tracker, контекстные CRUD/query, узкий UoW и maintenance без запуска SQL или host.
- [x] Изолированные .NET regressions 01–10/07A/08 сохраняют доступ, идентичность, суммы, bounded reads, predicate/include/defaults/token, RowVersion/RefreshRevision и обе транзакционные перегрузки; assertions не ослаблены ради новой DLL. Frontend не менялся и не пересобирался; прежние результаты 10–11 сохранены, HTTP/browser приёмка открыта.
- [x] Изолированно проверены no-pending, backup-before-migrate, отказ/неверный receipt/target → запрет migrations, явный InitializeNew, cancellation/timeout/неопределённый исход → fail-fast без retry, безопасные логи и отсутствие вызовов при DI-регистрации.
- [x] Статический аудит всех production consumers подтверждает отсутствие собственных SQL-подключений, прямых бизнес-запросов через context и второго migration/backup runner; разрешённые границы явно перечислены.
- [x] Изолированные compile-check/test выполнены для конкретных затронутых проектов по AGENTS; библиотечный base build использует `GeneratePackageOnBuild=false`. Upstream/project скрипты, host, DB/Docker и внешние интеграции не запускались; NuGet restore использован только для сборки.
- [ ] Отдельно с разрешением пользователя проверены SQL Server версия/права/server backup path, существующая БД с pending/без pending, receipt и реальное восстановление оператором, новая БД, отказ/отмена и SingleInitializer с несколькими экземплярами. Fake connection не закрывает эти критерии.
- [ ] Сохранена история migrations; применение AddReceiptRowVersion и другой схемы подтверждено отдельной DB-приёмкой, а не наличием файла.
- [x] Статусы прежних этапов и открытая SQL/HTTP/browser/provider/production-приёмка сохранены; эксплуатационная policy и оставшиеся блокеры документированы. 12–15 не начаты.

## Журнал подготовки

14.10.2026: проверены текущая DLL, project references, указанные исходники base/maintenance и production-вызовы CostVision. Добавлен этап и зависимости 14–15. Код, DLL, packages и migrations не изменены; build/test/Git/SQL/backup/host/deployment не запускались. Это подготовка плана, не реализация 08A.

Этап 11 уже в работе по отдельному поручению; frontend-этапы 11–13 не заменяют backend adapters и не требуют новой зависимости только из-за поставки DLL. Их HTTP-контракты и открытая приёмка сохраняются; backend-регрессии после обновления входят в 08A и 15.

## Журнал выполнения 08A

14.10.2026: реализован только 08A. Перечитаны actual upstream csproj/API и владельцы исходников. Source inputs соседнего репозитория сохранены (71 файл); Git-команды, pack/publish, migrations/SQL/host/Docker/deployment и проектные скрипты не запускались. В Web compile-check установлен SkipFrontendBuild=true, поэтому npm hooks не исполнялись. Фактический статус 11 перепроверен: реализован, проверка не завершена. Статусы 01–11/07A и их исторические результаты/открытая приёмка сохранены; 12–15 не начаты.

### Подключённая поставка и код

- Base EFCoreLibrary `0.0.5.0`, Maintenance и Maintenance.SqlServer `1.0.0.0` (upstream default), EF runtime/Relational/provider/Design/Tools `10.0.12`, SqlClient `7.0.2`. HttpClientLibrary не обновлялась. SHA-256 DLL/XML, исходный digest `5BE84E7BE6E7111AAB7EBCCF7AC1609D012A6309634FCC7C8470973B04E19B58`, SDK/точные build команды, NuGet SHA-512/runtime assets: [manifest](../../../libs/efcorelibrary-delivery-08a.json), [описание](../../../libs/README.md). Свежие DLL/XML взяты из output конкретных проектов после Build; повторный Build --no-incremental дал те же 6 хешей.
- Application CRUD-контракты адаптированы к новым интерфейсам без key; AppDbContextBase/EF/predicate/include/tracking/GetProjectedItemsAsync сохранены по решению А. Infrastructure использует IContext*Repository с одним CostVisionContextKey, AddEfCoreContext и AddEfCoreBaseRepositories. Production HintPath не зависят от абсолютного upstream пути.
- UnitOfWorkScope использует IUnitOfWorkContext.SaveChangesAsync/Database. Сохранены все 14 сценарных UoW, общий tracker/флаг отказа, один transaction owner, Serializable/conflict classification, auto-save без isolation и explicit-save с isolation, отказ повторного использования и отсутствие hidden retries.
- Product/Store CountByPredicateAsync больше не используют context.Set: библиотечный Query(true)/Where/CountAsync(ct). Общая projection исполняется библиотечным query, не добавлены specialized выборки, независимый query-язык или materialization всех строк для count/page.
- BackupService/DataBaseCheckUpService, их interfaces/path builder, DI и собственные SQL component tests удалены. Наблюдаемый контракт перенесён в actual DLL coordinator/SqlServer tests с fake границами. Production SQL-пути обслуживания принадлежат только AddEfCoreSqlServerMaintenance/IDatabaseMaintenance той же scoped-сессии.
- DatabaseMaintenanceStartup/Settings выбирают UpdateExisting по умолчанию либо явный InitializeNew, передают конечный timeout/token и ждут cleanup/async scope disposal. Readiness становится true только после них; иначе ExitCode=1 и возврат до app.RunAsync/HTTP/hosted refresh. StartupCancellation передаёт caller/ApplicationStopping/Ctrl+C/Unix SIGTERM до запуска ConsoleLifetime, затем освобождает подписки. Повтор обслуживания в процессе запрещён; внешний WaitAsync/Task.Run/Environment.Exit удалён.
- Библиотечные stage/code и startup outcome логируются без exception chains; SafeDatabaseLoggerFactory пересылает только EF EventCode без SQL/category/scope/state/formatter/provider exceptions. Серверные backup-пути не вычисляются/не создаются на Web. Production config не заполнялась вымышленными путями, правами или подтверждениями SingleInitializer.

### Команды и результаты

Рабочий каталог всех CostVision команд — корень репозитория. Library команды с абсолютными csproj/PathMap записаны в manifest. Release Build Maintenance.SqlServer собирает base/Maintenance/SqlServer; 0 ошибок, **107 upstream CS1591** о недостающих base XML-комментариях. Base XML с assembly element поставлен, но не объявляется полноценной API-документацией. Pack/publish отключены.

CostVision compile-check выполнялся для конкретных SDK проектов под `artifacts/compile-check/costvision-08a`, включая их ProjectReference closure. Итоговый Build каждого из четырёх production и четырёх test проектов — 0 предупреждений, 0 ошибок; логи `artifacts/compile-check/stage08a/CostVision.<project>-final-build.log`. Использована команда `dotnet build <конкретный csproj> -c Debug --no-restore --artifacts-path artifacts\compile-check\costvision-08a -p:SkipFrontendBuild=true -v:quiet -flp:logfile=<соответствующий final-build.log>`. Runtime isolated tests: .NET/ASP.NET Core `10.0.12`.

```powershell
dotnet build tests\CostVision.Infrastructure.UnitTests\CostVision.Infrastructure.UnitTests.csproj -c Debug --no-restore --artifacts-path artifacts\compile-check\costvision-08a -v:minimal -flp:logfile=artifacts/compile-check/stage08a/infrastructure-tests-build.log
dotnet build tests\CostVision.Application.UnitTests\CostVision.Application.UnitTests.csproj -c Debug --artifacts-path artifacts\compile-check\costvision-08a -v:minimal -flp:logfile=artifacts/compile-check/stage08a/application-tests-build.log
dotnet build tests\CostVision.Web.UnitTests\CostVision.Web.UnitTests.csproj -c Debug --artifacts-path artifacts\compile-check\costvision-08a -p:SkipFrontendBuild=true -v:minimal -flp:logfile=artifacts/compile-check/stage08a/web-tests-build.log
dotnet build tests\CostVision.Infrastructure.IntegrationTests\CostVision.Infrastructure.IntegrationTests.csproj -c Debug --artifacts-path artifacts\compile-check\costvision-08a -p:SkipFrontendBuild=true -v:minimal -flp:logfile=artifacts/compile-check/stage08a/integration-tests-build.log

dotnet test tests\CostVision.Application.UnitTests\CostVision.Application.UnitTests.csproj -c Debug --no-build --no-restore --artifacts-path artifacts\compile-check\costvision-08a --logger trx --results-directory artifacts\compile-check\stage08a\test-results\application
dotnet test tests\CostVision.Infrastructure.UnitTests\CostVision.Infrastructure.UnitTests.csproj -c Debug --no-build --no-restore --artifacts-path artifacts\compile-check\costvision-08a --logger trx --results-directory artifacts\compile-check\stage08a\test-results\infrastructure-final-r3
dotnet test tests\CostVision.Web.UnitTests\CostVision.Web.UnitTests.csproj -c Debug --no-build --no-restore --artifacts-path artifacts\compile-check\costvision-08a -p:SkipFrontendBuild=true --logger trx --results-directory artifacts\compile-check\stage08a\test-results\web-final-r2
```

| Набор | Passed | Failed | Skipped | Evidence |
|---|---:|---:|---:|---|
| Application.UnitTests | 496 | 0 | 0 | `artifacts/compile-check/stage08a/test-results/application/Spike_PC_2026-10-14_20_40_47_net10.0.trx` |
| Infrastructure.UnitTests | 168 | 0 | 0 | `artifacts/compile-check/stage08a/test-results/infrastructure-final-r3/Spike_PC_2026-10-14_21_03_29_net10.0.trx` |
| Web.UnitTests | 86 | 0 | 0 | `artifacts/compile-check/stage08a/test-results/web-final-r2/Spike_PC_2026-10-14_21_03_32_net10.0.trx` |
| Infrastructure.IntegrationTests | не запускался | — | — | Только compile-check; SQL/Docker/hosting запрещены |

Actual DI/DLL tests: единый context/tracker/connection, CRUD/query/UoW/maintenance и независимость scopes; generic defaults/include/token, scalar count/page на fake connection. Все прежние финансовые/identity/access/bounded/RowVersion/RefreshRevision/transaction assertions сохранены. HasPendingModelChanges=false, migrations assembly прежний, AddReceiptRowVersion присутствует. Новая модель/migration не требуется; generated migration/designer/snapshot не изменены.

Maintenance tests: no-pending без backup; pin/backup/target/migrate/pending/cleanup порядок; invalid operation/target/scope/format/time/artifact receipt; target swap; connection/auth/permission и 4060 visibility/absence; explicit init без backup; failure/deadline/caller cancellation; ожидание cleanup, poisoned gate без повторных provider/migration calls; HEADERONLY/VERIFYONLY через fake IDatabaseCommands, неизвестный dispatch исход; finite timeout, safe logs, readiness и async disposal; signal моделируется без реальных OS signals. Изоляция не доказывает SQL Server отмену/backup/restore или несколько процессов.

### Аудит и открытая приёмка

Статически просмотрены все 384 production .cs Domain/Application/Infrastructure/Web (без generated/build). Нет собственных SqlConnection/SqlCommand/Dapper/ExecuteSql/FromSql, business context.Set/CRUD, прямых Migrate/GetPending/CanConnect и второго runner. Сохранения выполняются только через сценарный scope; DatabaseFacade используется только внутри технического UnitOfWorkScope для transactions. EF model/provider configuration и пользовательская история migrations — разрешённые границы. SHA-256 всех 41 migration/designer/snapshot файлов совпадают с исходным набором. Evidence: `artifacts/compile-check/stage08a/production-audit.json`. SqlClient нужен для existing exception classification и библиотечного SQL Server runtime, не для собственного подключения.

Блокеры эксплуатации: неизвестны реальный EngineEdition/version, прямой endpoint, ConnectRetryCount=0, права maintenance user/service account, ServerBackupDirectory/место/ключи и подтверждённая остановка других writers/DDL/процессов. До заполнения обязательных DatabaseMaintenance options readiness блокируется. SingleInitializerConfirmed — операторское подтверждение, а не автоматическая остановка/проверка процессов; library gate локален root DI container. Active multi-instance/rolling/failover deployment неподдержан: distributed lease API отсутствует. Автоматическое SQL Server restore/recovery также заблокировано отсутствием API; обход не создан.

Реальные SQL/backup/header/verify/restore (включая восстановимость ключей), migrations upgrade/первая установка/отказы/отмена и OS/supervisor shutdown, multiple instances/SingleInitializer, SQL concurrency/rollback/materialization, live provider HTTP, auth/antiforgery/browser/production deployment не выполнялись. Budget кооперативный и не гарантирует остановку server/OS I/O за фиксированные 60 секунд; приложение ждёт cleanup и не повторяет неопределённый исход. Применение AddReceiptRowVersion к БД не подтверждено. Все прежние открытые проверки сохранены; 08A не является полной приёмкой.
