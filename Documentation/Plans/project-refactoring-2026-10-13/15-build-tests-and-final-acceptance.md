# Этап 15. Сборка, проверки и итоговая приёмка

Статус: **Реализован, проверка не завершена**.
Начало изолированной реализации: **15.10.2026**. Внешняя приёмка не начата.
Приоритет: **П2/П3**.
Зависимости: 01–14, включая 07A и 08A.

## Исходные проблемы аудита

Это исторические основания, а не текущий список дефектов. Фактический остаток на 15.10.2026 и результаты приведены ниже.

1. В `tests` нет собственного frontend-набора и отдельного теста расчёта дашборда. TypeScript compile-check не проверяет порядок Promise, выбор объекта модалки, результат импорта или financial totals.
2. В [AutoLinkExactMoneyMovementReceiptsUseCaseTests](../../../tests/CostVision.Application.UnitTests/UseCases/MoneyMovements/AutoLinkExactMoneyMovementReceiptsUseCaseTests.cs) и ряде связанных тестов repository mocks принимают любой predicate и возвращают готовый список. Они проверяют алгоритм, но не доказывают фильтрацию Viewer, закрытых счетов или исключённых пользователей.
3. [TestWebApplication](../../../tests/CostVision.Infrastructure.IntegrationTests/Web/TestWebApplication.cs) вручную собирает альтернативные services/pipeline. Тест auth через него не доказывает производственную композицию middleware, startup и административной политики целиком.
4. [.github/workflows/main.yml](../../../.github/workflows/main.yml) запускается только вручную, собирает и тестирует solution целиком; unit, hosting, PDF/процессы и SQL/Docker-контуры не разделены явно по заданиям. Node перед косвенным frontend build не фиксируется отдельной настройкой.
5. [CostVision.Web.csproj](../../../CostVision.Web/CostVision.Web.csproj) может вызвать `npm ci` при `dotnet build`, если нет TypeScript. Разрешённый compile-check .NET сам по себе не должен неожиданно стать сетевым восстановлением или запуском зависимых скриптов. `SkipFrontendBuild` уже предусмотрен, его нужно учитывать в изолированной проверке.
6. [Dockerfile](../../../CostVision.Web/Dockerfile) ставит Node/npm из apt без явно закреплённой версии. [libs/README.md](../../../libs/README.md) уже фиксирует версии/хеши принятой поставки и ограничения проверки. 14.10.2026 выявлено отставание EFCoreLibrary `0.0.2` от предоставленных исходников `0.0.5`; воспроизводимая base/Maintenance/SqlServer поставка и переход consumers запланированы в 08A и ещё не выполнены.
7. Прежний `.github/workflows/cd.yml` останавливал и удалял старый контейнер до проверки готовности нового; readiness/rollback отсутствовали. **15.10.2026 пользователь поручил полностью удалить CD**, workflow удалён. Будущее возвращение deployment automation требует отдельного решения.
8. Документация местами опережает контракт: server-side pagination кандидатов и существование всех role ID заявлены, но текущие реализации этого не обеспечивают. Полезно разделять описание работающего поведения, план и непройденную приёмку.

## План работ

- Дополнять регрессионные тесты непосредственно в этапах 01–14, включая 07A и 08A. Здесь проверить общий набор и отсутствие пропущенных критериев.
- Добавить изолированные frontend-тесты в `tests/Frontend/*.test.mts` для чистой логики, подставного HTTP и управления запросами. DOM/таймерные doubles не объявлять браузерной приёмкой.
- Проверять передаваемые predicates и ограничения результата. SQL-трансляцию, уникальность, rollback и конкуренцию подтверждать SQL Server integration tests только с отдельным разрешением.
- Разделить CI-контуры и закрепить toolchain. Решение о запуске на push/PR оформить как изменение политики проекта, не включать автоматически при подготовке плана.
- Для compile-check использовать конкретные `.csproj`, отдельный `artifacts/compile-check`, по возможности `--no-restore` и контроль frontend-targets. Сборку TypeScript запускать по правилам проекта; generated JS вручную не редактировать.
- Закрепить результат 08A в CI/поставке: одна проверенная версия base/Maintenance/Maintenance.SqlServer, версии/хеши/XML/runtime dependencies и согласованный EF runtime/provider/Design/Tools граф. Проверить production и test references, Docker/publish contents и отсутствие старой DLL в выходной поставке. Не использовать upstream абсолютный путь как deploy reference; HttpClientLibrary не обновлять заодно без отдельной причины.
- Добавить проверку отсутствия несогласованных DB-обходов по правилам 08A: business reads/writes через библиотечные repositories/UoW, обслуживание через maintenance coordinator; прямые SqlConnection/ручной SQL/DbContext CRUD и второй migration/backup runner в production запрещены. Регрессии actual DLL/DI/query/tracker и обеих транзакционных перегрузок обязательны; fake DB не закрывает SQL-приёмку.
- Отдельно закрыть эксплуатационные критерии 08A/14 в разрешённом SQL Server контуре: существующая/новая БД, backup receipt/target, сохранность migration history, операторская проверка восстановления, отмена/неопределённый исход и SingleInitializer. Не обещать rollback схемы одной заменой DLL/container; совместимость данных и recovery должны быть проверены до deployment.
- Решение15.10.2026: полностью удалить CD. Возврат deployment automation требует отдельного решения; требования readiness/recovery и сохранности Config/Logs/key ring остаются для будущей эксплуатации.
- После проверки обновить технические требования и владеющие документы фактическим состоянием.

## Итоговая приёмка

- [ ] Все критичные сценарии 01–06 закрыты проверенными результатами.
- [x] Затронутые .NET unit tests и TypeScript compile-check выполнены без снижения строгости/assertions.
- [x] Есть точные результаты: команда, проект/контур, passed/failed/skipped, дата и ограничения.
- [x] SQL, hosting, browser, камера и deployment отмечены отдельно; непройденные проверки не названы успешными.
- [ ] Права, дедупликация, финансовые суммы и конкуренция проверены на подходящем уровне.
- [ ] Разделение UoW по этапу 07A проверено: минимальные зависимости, единая сессия, производственная DI-регистрация, семантика сохранения и отказов, отдельная SQL-приёмка.
- [ ] Обновление 08A проверено: воспроизводимая base/Maintenance/SqlServer поставка, согласованные references/dependencies, единый tracker/UoW/maintenance и отсутствие несогласованных обходов БД.
- [ ] Backup/migrations/SingleInitializer и восстановление оператором имеют отдельные фактические результаты; файл AddReceiptRowVersion не объявлен доказательством её применения.
- [x] Документация описывает реализованное; статусы этапов и навигация синхронизированы.
- [x] Остаточные блокеры и согласованные отложенные продуктовые решения записаны в соответствующих этапах.

## Журнал выполнения

Историческая подготовка 14.10.2026: добавлены критерии поставки и отсутствия обходов, зависимость от 08A и отдельная SQL/backup/recovery-приёмка; в ходе подготовки build/test не запускались.

### Actual остаток и реализация, 15.10.2026

| Основание аудита | Проверенное состояние / результат15 |
|---|---|
| Нет frontend/dashboard tests | Уже устранено01–14: tests/Frontend содержит137 cases, Stage05RegressionTests проверяет actual dashboard/calculator/coverage; не реализовано повторно |
| Predicate-ignorant mocks auto-link | Два старых теста переведены на actual predicate и полные Account/Members графы. Четыре новых cases проверяют отзыв Editor membership при сохранённом авторстве и выбранный/все счета; нулевой результат не сохраняется. Матрица Owner/Editor/Viewer и ambiguity уже есть в Stage04RegressionTests |
| TestWebApplication вместо production | Подтверждено: отдельный HostBuilder/services/cookie/pipeline, другой DP discriminator, нет production options/startup/admin composition. Не запускался и не принят как production evidence; требуется отдельный HTTP-контур |
| CI смешивает solution/unit/external | Ручной workflow_dispatch сохранён. Отдельные jobs: frontend; matrix трёх UnitTests; IntegrationTests только Build. Каждый .NET project восстанавливается явно, Build/test — no-restore, SkipFrontendBuild=true, отдельный artifacts output. CI не запускался |
| Неявный npm ci | Удалён из Web target: отсутствие tsc — Error с явной командой установки. BuildFrontend перед PrepareForBuild; IncludeFrontendAssets перед AssignTargetPaths добавляет dist без duplicate Content, в том числе при изначально отсутствующем dist. SkipFrontendBuild не исключает готовые assets. OpenAPI generation on build явно false, TypeScript MSBuild blocked сохранён |
| Toolchain / старая DLL | Старая DLL уже заменена08A. Новые global.json10.0.401, .node-version22.18.0, packageManager npm10.9.3 согласованы с установленными версиями. Корневой NuGet.Config очищает inherited sources и задаёт nuget.org; restore в CI/Docker использует его явно. .npmrc и npm ci отключают lifecycle scripts/audit/fund; type=module соответствует ES-модулям и устраняет MODULE_TYPELESS_PACKAGE_JSON без подавления diagnostics |
| Docker / closure | Node22.18.0 из отдельного build-platform stage, проверка npm10.9.3; SDK10.0.401/aspnet10.0.12. libs/config скопированы до restore; TS до publish; publish --no-restore/SkipFrontendBuild=true, копируется весь publish directory. Runtime stage не получает Node. Это статическая проверка, образ не собирался |
| CD / документация | По отдельной просьбе пользователя CD удалён полностью. Readiness/rollback workflow не выдуман. Уточнены текущие инструкции, описание сборки/тестов и устаревшие утверждения о Swagger/provider/import; бизнес-правила не изменены |

Stage15DeliveryTests добавляет две исполняемые изолированные проверки: реальные загруженные DLL/соседние XML против manifest и HTTP hash; actual восстановленный Infrastructure package graph против принятых package identities/SHA-512 и runtime requirements. Они требуют source checkout, не открывают соединения и не запускают host. Supplier/HTTP/package upgrades не выполнялись.

### Команды и свежие результаты

Дата: **15.10.2026**. Cwd — корень CostVision, кроме npm. .NET/ASP.NET runtime10.0.12, SDK10.0.401, Node22.18.0, npm10.9.3, TypeScript6.0.3. Перед запуском проверены parent/repo build/package configs, csproj/imports/targets и test boundaries. Пользовательский NuGet source Telegram.Bot отключён; no-restore не обращается к feeds. Swashbuckle props уже отключал host-based OpenAPI generation; теперь запрет явный в Web. Контейнерный ToolsTarget не активирован, project hooks/scripts не запускались.

```powershell
dotnet build CostVision.Domain/CostVision.Domain.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet -flp:logfile=artifacts/compile-check/test-results/build-CostVision.Domain-stage15.txt
dotnet build CostVision.Application/CostVision.Application.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet -flp:logfile=artifacts/compile-check/test-results/build-CostVision.Application-stage15.txt
dotnet build CostVision.Infrastructure/CostVision.Infrastructure.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet -flp:logfile=artifacts/compile-check/test-results/build-CostVision.Infrastructure-stage15.txt
dotnet build CostVision.Web/CostVision.Web.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet -flp:logfile=artifacts/compile-check/test-results/build-web-stage15.txt
dotnet build tests/CostVision.Infrastructure.IntegrationTests/CostVision.Infrastructure.IntegrationTests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet -flp:logfile=artifacts/compile-check/test-results/build-integration-stage15.txt
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage15 --verbosity quiet
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage15-final-r2 --verbosity quiet
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --logger trx --results-directory artifacts/compile-check/test-results/web-stage15 --verbosity quiet
# cwd: CostVision.Web
npm run build:ts
# cwd: корень CostVision
node --experimental-strip-types --test tests/Frontend/*.test.mts
```

| Контур | Passed | Failed | Skipped | Cancelled / aborted | Evidence в artifacts/compile-check/test-results |
|---|---:|---:|---:|---:|---|
| Application, полный isolated suite | 503 | 0 | 0 | 0 | application-stage15/Spike_PC_2026-10-15_09_03_51_net10.0.trx |
| Infrastructure, полный финальный isolated suite | 198 | 0 | 0 | 0 | infrastructure-stage15-final-r2/Spike_PC_2026-10-15_09_09_01_net10.0.trx |
| Web, полный isolated suite | 101 | 0 | 0 | 0 | web-stage15/Spike_PC_2026-10-15_09_04_23_net10.0.trx |
| Frontend, весь tests/Frontend | 137 | 0 | 0 | 0 | frontend-stage15.txt |
| Strict TS/noEmitOnError | exit0 | — | — | — | build-typescript-stage15.txt |
| Domain/Application Build | 0 errors / 0 warnings | — | — | — | build-CostVision.{Domain,Application}-stage15.txt |
| Infrastructure/Web/Integration Build | 0 errors / по2 NU1510 | — | — | — | build-CostVision.Infrastructure-stage15.txt, build-web-stage15.txt, build-integration-stage15.txt |

В .NET TRX также error/timeout=0. Полные unit-проекты выбраны после проверки actual изоляции: actual PdfPig — memory streams; provider/maintenance/SQL commands — doubles; DI/context — без открытого connection; configuration — временный файл/уникальный environment key; Web — direct handlers/options/context, без WebApplication/host. Frontend использует actual compiled modules/fake DOM/fetch/streams/vendor, не браузер. В сумме 939 passed,0 failed,0 skipped,0 cancelled/aborted; количество не означает общую приёмку.

Все build/test exit0 с первого запуска; Infrastructure сначала196, затем197 и финально198 после добавления двух delivery tests. Ранее08A полные496/168/86 и14 scoped22/102/28,13 frontend137 — исторические результаты, не свежие15. Новых cases15 —4 Application и2 Infrastructure, прежние assertions/strict не ослаблялись. Generated dist обновлён только npm build:ts.

Два NU1510 относятся к DI.Abstractions/Logging.Abstractions из существующего restore cache; в текущем Infrastructure csproj прямых references уже нет. Новый restore/package upgrade в15 не выполнялся: warnings не подавлялись и не объявлены исправленными. Проверить свежий restore/closure отдельно при явном разрешении сети. MODULE_TYPELESS_PACKAGE_JSON в свежем node-прогоне отсутствует после явного type=module; ожидаемый console error отрицательного receipt-user case не означает падение теста.

### Поставка и статические доказательства

`delivery-stage15.json`: все6 DLL/XML hashes и assembly/file versions совпадают с manifest08A, HTTP hash прежний; те же файлы фактического build output совпали. Все58 consumer runtime identities/asset paths совпали,20 общих library package SHA-512 совпали;10 standalone packages отсутствуют в Web из-за shared-framework/platform closure. Все72 runtime/native paths фактического Web.deps.json существуют в output. Pkcs9.0.13 из assets не входит в Web.deps.json: conflict resolution использует Pkcs10.0.12 из Microsoft.AspNetCore.App10.0.12. Простое требование копировать каждый assets-файл дало ложные два missing; исправлен способ проверки по final deps/framework, поставка не менялась. Первая read-only проверка JSON ошиблась на индексировании PSObject.Properties; исправлена на actual target, не ослаблением сравнений.

Manifest SHA-256 `C5AFD6A8E373C2839A4E25066FF0C75CCAAAB9E937DF84104CB10295E6235FBA`, исходный digest/71 inputs остаются принятой идентичностью08A; upstream заново не собирался и не переаттестовывался. Base XML остаётся assembly-only; полнота API docs/неизвестные upstream URL/license/HTTP build source не выдуманы. Все production/test HintPath относительны к libs. Actual DLL/DI/query/tracker/transaction регрессии включены в свежие полные suites.

Аудит392 production .cs вне generated/build: своих SqlConnection/SqlCommand/Dapper/FromSql/ExecuteSql/context.Set/прямого Migrate/GetPending/CanConnect не обнаружено. Save остаётся в сценарном scope, DatabaseFacade — только технический UoW; maintenance — IDatabaseMaintenance. EF model/provider registration и41 generated migration/designer/snapshot файлов сохранены. Evidence: production-db-boundary-stage15.txt и финальный static-stage15.json. Это статический аудит, не SQL acceptance.

Docker/publish/source-feed install/CI на Linux/ARM64 **не запускались**. Общий build output разных csproj не является чистым publish artifact; final target RID/native resolution, static web assets и отсутствие лишних файлов надо проверить отдельно. Проверка clean publish/нового npm ci не подменяется локальным build с готовыми dependencies/dist. Зафиксированные tags не являются immutable image digests.

### Открытая приёмка

Дополнение15.10.2026 после пользовательского security update packages: consumer graph приложения больше не сравнивается с историческим libraryResolvedPackages08A. Отдельный [consumer manifest](../../../libs/costvision-consumer-packages.json) фиксирует21 точную identity/NuGet content hash, включая Bcl.Cryptography9.0.18, EF/provider10.0.13, SqlClient7.1.1 и Pkcs10.0.13; values сверены с текущими Infrastructure assets и metadata локального NuGet cache. Исторический manifest/DLL/XML не изменены. Тест требует все21 packages и сохраняет framework exceptions; откатывать зависимости или принимать произвольные новые версии не требуется.

Текущий xunit.v3 4.0.2 требует MTP на .NET10: первая попытка прежней команды test завершилась ошибкой VSTest target до тестов. global.json переключён на Microsoft.Testing.Platform, ручной CI использует --project. Попытка с --report-trx дала exit5/ноль тестов (нет соответствующего report extension); по actual --help исправлено на встроенный --report-xunit-trx, дополнительных packages не устанавливалось. Финальный Infrastructure suite **198 passed /0 failed /0 skipped**, exit0; TRX: artifacts/compile-check/test-results/infrastructure-consumer-update/Spike_PC_2026-10-15_03_23_20.727951.trx. Build тестового csproj —0errors/7 CS8602 в существующих Stage08aStartupTests/Stage08aLibraryTests; warnings не подавлялись. Другие suites/CI/external acceptance не запускались повторно; прежние939 — исторический результат до обновления пакетов. Точные новые команды:

```powershell
dotnet build tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet
dotnet test --project tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj -c Debug --no-build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --report-xunit-trx --results-directory artifacts/compile-check/test-results/infrastructure-consumer-update
```

Полная [матрица оставшейся приёмки](15-acceptance-matrix.md) сохраняет критерии01–14/07A/08A, порядок, ожидаемые результаты и причины unrun. SQL/схема/AddReceiptRowVersion/старые данные/constraints/materialization/concurrency/backup/restore/SingleInitializer, production HTTP/auth/options/OS/provider, browser/camera/CSP/storage/Bootstrap/accessibility, clean publish/Docker/Linux/ARM64/свежий restore и deployment не проверены. Локальный gate не distributed lease, active rolling/multi-instance/failover остаётся блокером, container/DLL replacement не rollback схемы. Config/Logs/key ring mounts, uid/права/discriminator остаются обязательными для будущего deployment после удаления CD.

Подготовлен [следующий самостоятельный промпт](prompts/next-stage-15-acceptance-prompt.md) только для оставшейся работы. Его подготовка не разрешает внешние действия. Модель, financial/access/identity/ownership/manual pairs/import/refresh/retry/transaction/EF/query/HTTP/coordinator/scanning/storage/modal/startup-policy контракты не менялись. Статусы01–15/07A/08A — «Реализован, проверка не завершена» до обязательной actual приёмки.
