# Этап 14. Запуск и конфигурация

Статус: **Реализован, проверка не завершена**.
Дата начала и изолированной проверки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 06, 08, 08A, 09.

Переход на актуальные base/Maintenance/SqlServer, замена собственного backup/checkup и правила единого доступа к БД принадлежат [этапу 08A](08a-efcorelibrary-upgrade-and-maintenance.md). Ниже исходные проблемы сохранены как основание; после 08A этот этап завершает options/configuration и эксплуатационную приёмку, не создавая второй SQL/backup/migration путь.

## Найденные проблемы

1. [Program.cs](../../../CostVision.Web/Program.cs) добавляет обязательный `Config/config.json` после стандартных providers builder. При совпадающих ключах этот файл получает приоритет над ранее добавленными environment/command-line providers. Путь зависит от текущего рабочего каталога, а не явно от content root.
2. Общий лимит Kestrel — 1 ГБ; application-проверка preview принимает `FileLength`, но использует его только для отсутствия файла. [BankStatementPdfTextExtractor](../../../CostVision.Infrastructure/Services/MoneyMovements/BankStatementPdfTextExtractor.cs) синхронно открывает документ и читает все страницы. Cancellation проверяется только при обходе страниц; максимальный объём/число страниц и malformed PDF как ожидаемая ошибка не определены.
3. Исторический WebApplicationExtensions запускал удалённый в08A `DataBaseCheckUpService` через Task.Run/WaitAsync(60 секунд)/Environment.Exit. Актуальный async библиотечный lifecycle08A сохранён; старый сервис не восстановлен. Runtime SQL-user требует отдельно проверенных прав backup и изменения схемы.
4. Исторический `BackupService` вручную собирал SQL/путь и создавал локальный каталог. Он удалён в08A. Актуальный ServerBackupDirectory относится только к SQL Server; backup/target/receipt/migrations остаются ответственностью библиотеки.
5. Startup/backup path, HTTP timeout и расписание распределены между константами и DI. URL проверяется на абсолютность, но не на допустимый HTTP(S)-протокол. Data Protection и инфраструктура нуждаются в явном контракте путей/прав/единого key ring для нескольких экземпляров.
6. Swagger включён во всех средах с общим `My API`, при отсутствии отдельного описанного назначения контроллеров. Это область упрощения конфигурации, не доказанная утечка защищённых данных.

## План работ

- Описать порядок providers и использовать стабильный путь конфигурации; проверить перекрытие значений без вывода секретов.
- Ввести валидируемые options для загрузки файлов, PDF, startup и внешних запросов. Отклонять превышение до дорогостоящего разбора; ошибки PDF классифицировать на границе.
- Опираясь на 08A, отделить проверку готовности БД от обновления схемы либо явно оставить принятую автоматическую политику с корректным lifecycle через `IDatabaseMaintenance<TKey>.InspectAsync/UpdateExistingAsync`. Первую установку выполнять только явно выбранным `InitializeNewAsync`; ошибка подключения не разрешает создание БД. Изменение startup-policy согласовать; правило «миграции создаёт пользователь» не означает запрет применения уже созданных миграций библиотечным coordinator.
- Через валидируемые options передать `SqlServerMaintenanceOptions.ServerBackupDirectory`, конечный timeout и cancellation; проверить права, прямой endpoint, `ConnectRetryCount=0` и ограничения provider из 08A. Не исправлять старый BackupService новым ручным SQL: backup/receipt/target/migrations принадлежат библиотеке. SQL в этом этапе не выполнять без прямой просьбы.
- Согласовать readiness и порядок запуска HTTP/фонового refresh с SingleInitializer: другие экземпляры, writes и DDL остановлены на весь maintenance. Локальный gate не является распределённой блокировкой; неподдержанный контур — явный блокер. Не завершать только внешнее ожидание, оставляя SQL/cleanup работать в уже освобождённом scope.
- Сохранить fail-fast, ValidateScopes/ValidateOnBuild, Options ValidateOnStart и доверенные reverse proxies. Не подставлять фиктивную конфигурацию для успешного запуска.

## Проверки и приёмка

- [x] Приоритет конфигурации и обязательные options проверены изолированно без раскрытия значений секретов; production values/permissions отдельно открыты.
- [ ] Большой/повреждённый PDF даёт ограниченный по ресурсам понятный отказ.
- [ ] Timeout startup не объявляется отменой продолжающейся операции.
- [ ] Backup path и права проверены в реальном разрешённом контуре отдельно.
- [ ] Выбранная политика migrations и нескольких экземпляров документирована; миграции создаёт пользователь.
- [x] DI/options и startup используют поставку 08A и один maintenance coordinator; собственные SqlConnection/backup SQL/прямой Migrate не возвращены. Actual DI/fake maintenance проверены без SQL/host.
- [ ] SingleInitializer и readiness до допуска фоновых/HTTP writes проверены отдельно; логи maintenance не раскрывают секреты, пути или provider exception chains.

## Журнал выполнения

Дата начала: 14.10.2026. Разрешена изолированная реализация только 14 при открытой приёмке 01–13/07A/08A. Пользователь подтвердил config.json от content root → environment → command line, сохранение общего request limit 1 000 000 000 байт, PDF 20 МиБ/200 страниц и текущей автоматической maintenance-политики 08A. SQL, startup, backup и deployment не запускались; этап 15 не начат.

### Реализованный контракт14

- ConfigurationExtensions использует абсолютный content-root/Config/config.json, сохраняет base path стандартных providers и явно добавляет environment/command line последними. Обязательный файл не перезагружается; отсутствующий файл не обходится. Host content root выбирается ASP.NET Core/оператором; его стандартный начальный cwd не подменён вымышленным production каталогом.
- WebRuntimeServiceCollectionExtensions/Options владеют Uploads, DataProtection и Swagger. Kestrel default1 000 000 000 сохранён; multipart/proxy limits отдельно. KeyDirectory относительно content root либо абсолютный; прежние OS defaults, явный discriminator CostVision.Web, XmlRepository без directory creation при DI. Swagger defaulttrue сохраняет все среды, явный false отключает JSON/UI; название CostVision API. Собственных API-контроллеров не обнаружено, Razor handlers сохранены.
- Infrastructure options валидируют обязательные token/connection, абсолютный HTTP(S), конечный положительный представимый HttpClient timeout (default180), PDF byte/page defaults20 МиБ/200 и прежние maintenance prerequisites/timeout1–3600. Maintenance получает единственный registration snapshot operation/budget/directory/confirmation, согласованный с immutable library options. Reload не переключает operation/path в середине startup.
- InitializeDatabaseServicesAsync вызывает actual IStartupValidator всех модулей **до** DatabaseMaintenanceStartup: неверные API/PDF/Uploads/key-ring/proxy/maintenance options не запускают обслуживание. Program ловит registration/composition отказ закрытым кодом без исходной диагностики, ExitCode=1 и без RunAsync. Maintenance policy08A, caller/signal cancellation, одна попытка и ожидание cleanup/DisposeAsync до readiness сохранены.
- Extractor проверяет actual remaining stream bytes: seekable oversize до read, non-seekable максимум limit+1 до PdfPig; NumberOfPages до text extraction. Слишком короткий/известный format/encrypted PDF даёт безопасный BankStatementPdfException, preview преобразует только его в Validation400 до parser/duplicate queries. Caller stream остаётся открытым до Web disposal. Unknown I/O/ошибка и cancellation распространяются. PdfPig0.1.15 синхронен: cancellation между шагами, без гарантии прерывания open/page/decompression или CPU/memory ceiling. QR/camera/storage limits12 не изменены.
- DLL/XML/manifest08A и HttpClientLibrary не менялись; actual hashes совпадают с libs/README.md/manifest. Business query/UoW/tracker/transactions/import/refresh/schema/HTTP/frontend contracts01–13 не перестраивались. Migration/designer/snapshot не редактировались; AddReceiptRowVersion уже существует, применение не проверено.
- Owning/parent AGENTS, бизнес PDF-граница, интеграции/эксплуатация/навигация/slnx обновлены. [Самодостаточный промпт15](prompts/next-stage-15-prompt.md) подготовлен; реализация15 не начата.

### Фактические проверки

Из корня репозитория, конкретные csproj, без restore/host/SQL/сети/процессов. Финальные scoped .NET результаты, не полный набор проекта:

| Контур | Passed | Failed | Skipped | TRX под artifacts/compile-check/test-results |
|---|---:|---:|---:|---|
| Application: новые14 + preview/access/import regressions | 22 | 0 | 0 | application-stage14/Spike_PC_2026-10-14_22_42_03_net10.0.trx |
| Infrastructure: новые14 + actual08A startup/maintenance/DLL + provider/refresh DI | 102 | 0 | 0 | infrastructure-stage14-final/Spike_PC_2026-10-14_22_48_48_net10.0.trx |
| Web: новые14 + startup signals/proxy/MoneyMovements handlers/DI | 28 | 0 | 0 | web-stage14/Spike_PC_2026-10-14_22_41_09_net10.0.trx |
| IntegrationTests | не запускался | — | — | Только compile-check |

Новых случаев14: Application3, Infrastructure28, Web13. PDF tests используют настоящий PdfPig/generated memory PDF и actual extractor; mocks только streams/maintenance и application repositories/parser. Web конфигурация использует настоящий временный config.json, уникальную process environment variable и command line; хост/WebApplication не создаётся. Actual startup boundary проверяет безопасные logs/ноль maintenance calls при invalid options и awaited async disposal перед readiness. Прежние08A fake commands/clocks/target/backup/header/verify/failure/cleanup проверены как регрессии, без реального соединения.

Первый Infrastructure run: 25 passed/2 failed/0 skipped, TRX infrastructure-stage14-first. Выявлены PdfPig ArgumentOutOfRangeException на пустом файле и неверное тестовое предположение о разрешении immutable library options из DI; добавлена явная короткая-file проверка до PdfPig, тест исправлен по actual provider registration, unknown exceptions не перехвачены широким catch. Промежуточный Web compile отказ из-за отсутствующей test-only Maintenance reference устранён относительным HintPath; production DLL не менялась.

```powershell
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter 'FullyQualifiedName~Stage14PreviewTests|FullyQualifiedName~Stage08PreviewBoundaryTests|FullyQualifiedName~BankStatementImportUseCaseTests|FullyQualifiedName~Stage04RegressionTests.Preview' --logger trx --results-directory artifacts/compile-check/test-results/application-stage14 --verbosity quiet
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter 'FullyQualifiedName~Stage14|FullyQualifiedName~Stage08a|FullyQualifiedName~ExternalReceiptProviderTests|FullyQualifiedName~Stage06RefreshPersistenceTests' --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage14-final --verbosity quiet
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --filter 'FullyQualifiedName~Stage14|FullyQualifiedName~StartupCancellationTests|FullyQualifiedName~ForwardedHeadersServiceCollectionExtensionsTests|FullyQualifiedName~MoneyMovementsModelTests' --logger trx --results-directory artifacts/compile-check/test-results/web-stage14 --verbosity quiet
```

Final Build Application/Infrastructure/Web и compile-only IntegrationTests — **0 errors**. Application —0 warnings; Infrastructure/Web/Integration —по2 прежних NU1510 DI.Abstractions/Logging.Abstractions из existing assets, не подавлялись и не исправлялись вне14. Логи build-{application,infrastructure,web,integration}-stage14-final.txt в том же test-results. Каждая точная команда: `dotnet build <соответствующий csproj> --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet -flp:logfile=artifacts/compile-check/test-results/build-<имя>-stage14-final.txt`; Web/Integration дополнительно `-p:SkipFrontendBuild=true`. Domain собирается через ProjectReference closure; отдельный неизменённый набор Domain не запускался.

Frontend не менялся, npm/node/dist не запускались/не обновлялись. Старые13 frontend137/0/0/cancelled0 и08A полные496/168/86 являются историческими результатами, не новой проверкой14. App/hosting/dev-watch/browser/camera/device/Docker/БД/SQL/provider/network/deployment/EF tooling/прочие scripts не запускались. Git допускается только тремя read-командами в конце для requested commit message; stage/commit/push не выполняются.

Финальный статический аудит: все49 modified/untracked текстовых файлов проверены строгим UTF-8, U+FFFD/четырёх вопросительных знаков/проверенных mojibake patterns нет; ссылки затронутых Markdown и все Path-элементы slnx существуют. Исходные BOM/EOL сохранялись, новые файлы UTF-8 без BOM. Actual DLL assembly/file/product versions и все шесть DLL/XML hashes совпадают с manifest08A; HTTP hash прежний. Production scan не обнаружил SqlConnection/SqlCommand/Dapper/ExecuteSql/FromSql/direct Migrate вне разрешённых model/provider/generated границ. Выполнены только git status --short/git diff --stat/git diff --name-only:35 modified+14 untracked, прежних незакоммиченных08A файлов в выдаче нет. Git предупредил о будущей LF→CRLF нормализации существующих LF документов/project-файлов; окончания строк не нормализовались ради этого предупреждения. Commit message охватывает все49 файлов, stage/commit/push не выполнялись.

### Открытая приёмка14 и прежних этапов

Ни один production/SQL/OS/browser критерий не закрыт изолированными результатами. Конкретная процедура после отдельного разрешения:

1. В целевом deployment проверить actual content root/mount/необычный cwd, отсутствие/повреждение config.json и config→environment→CLI overrides на несекретном контрольном ключе. Проверить отсутствующий token/connection/invalid URI/timeouts/proxy/PDF settings: fail-fast до любых SQL/HTTP/hosted calls, ExitCode=1, безопасные logs без secret values/путей/exception chains. Operator values не публиковать в evidence.
2. Через actual authenticated multipart/antiforgery route проверить PDF ровно20 МиБ/набайт больше,200/201 страниц, empty/malformed/encrypted/безtext layer, отмену upload/preview и сбой stream. Проверить400 envelope/нет preview rows/нет записи и сохранённые import state/номерастрок04/10. Отдельно измерить сложный/compressed PDF: byte/page caps не являются доказательством CPU/memory/deadline. Прокси/form/Kestrel actual413 отдельно.
3. Оператор подтверждает прямой SQL endpoint/EngineEdition2/3/4, ConnectRetryCount=0, права runtime maintenance user/SQL service account, server-only directory/место/backup ключи и остановку **всех** других writers/DDL/процессов на весь maintenance. Connection/auth/permission отказ не разрешает InitializeNew. Поддержанного distributed lease/failover API нет: active multi-instance/rolling contour остаётся блокером, локальный gate не заменяет остановку.
4. На разрешённом SQL контуре проверить existing no-pending→Unchanged/no-backup, pending→pin/backup/receipt/HEADERONLY/VERIFYONLY/migrate/reinspect/cleanup, explicit InitializeNew и последующее возвращение operator Operation к UpdateExisting, безопасные отказ/отмена/unknown outcome без retry и без host admission. Оператор отдельно восстанавливает backup с нужными ключами; receipt/VERIFYONLY не доказывают restore. AddReceiptRowVersion application/rowversion/bigint/default/старые данные/индексы проверяются отдельно.
5. На разрешённом host/OS проверить Ctrl+C/Unix SIGTERM/ApplicationStopping до RunAsync, timeout/caller cancellation и длительный cleanup, отсутствие HTTP listener/hosted refresh до успешного awaited disposal. Кооперативный budget не гарантирует остановку server/OS I/O; supervisor grace/forced kill требуют отдельной проверки без обещания rollback.
6. Проверить DP ring persistence/permissions/encryption-at-rest/backup/совместимые cookie при restart и одинаковом discriminator, explicit path/content root, trusted reverse proxy Host/Proto/For и Swagger default/disabled. Совместимость ключей нескольких экземпляров не устраняет SingleInitializer блокер. HTTP/auth/antiforgery/browser acceptance остаётся отдельной.
7. Сохранить всю приёмку01–13/07A/08A: SQL materialization/predicate/include/projection/precision/collation/membership revoke/объём/планы/несколько чтений без snapshot; two-session identity/import/link/transfer/refresh/catalog races, rollback/1205/fixup/новый scope; live provider/hosted; actual HTTP cookies/shape/errors; UI generations/unknown writes/import drafts/paging/totals; camera/iframe/CSP/storage; Bootstrap animation/focus/Escape/accessibility. Полный перечень — журналы этапов и prompt15. Новых financial/recovery/retention/decoder/startup-policy решений не введено.
