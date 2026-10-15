# Принятая поставка библиотек

Поставка 08A подготовлена 14.10.2026 из указанных пользователем исходников `D:\Media\User\source\repos\work\EFCoreLibrary`. Production/test HintPath остаются относительными к `libs`; исходники соседнего репозитория и HttpClientLibrary не изменены.

| Файл | AssemblyVersion | FileVersion | ProductVersion | SHA-256 |
|---|---|---|---|---|
| EFCoreLibrary.dll | 0.0.5.0 | 0.0.5.0 | 0.0.5 | A2D5C72E7B30EC8886D70AFBD9B30F1A424EAD4402F37C5174A58601AD77A11F |
| EFCoreLibrary.Maintenance.dll | 1.0.0.0 | 1.0.0.0 | 1.0.0 | 24B9B4DF25E519833A25C12DDD036EA9B397AA4DE2E6202B2B5D2DC66B6F3CDB |
| EFCoreLibrary.Maintenance.SqlServer.dll | 1.0.0.0 | 1.0.0.0 | 1.0.0 | 76C83AAAAB8522F598605A3D5E55976E71C6E84ACC51B0CA513E260E9C2C2E4B |
| HttpClientLibrary.dll | 1.0.0.0 | 0.0.0.3 | 1.0.0+52cd45378cbf3acb050bc886679ffda7ac0c2168 | 37B5EC623F8770CE66ADBCE0CECCB06F45368881AB07152AB5CFE94CC9C16925 |

Сборки имеют Culture=neutral и PublicKeyToken=null. Maintenance-проекты не задают Version и фактически получают стандартную `1.0.0`, а не версию base `0.0.5`. Проверенный Git commit не заявляется: Git-команды не выполнялись. Upstream URL и лицензия отдельно не установлены; для HttpClientLibrary процедура сборки и соответствующий исходный набор по-прежнему неизвестны.

## Воспроизведение поставки 08A

[Manifest](efcorelibrary-delivery-08a.json) содержит 71 build input с SHA-256, точные команды, SDK `10.0.401`, DLL/XML metadata, resolved NuGet closure с package SHA-512 и runtime assets Web. Digest исходного набора: `5BE84E7BE6E7111AAB7EBCCF7AC1609D012A6309634FCC7C8470973B04E19B58`. Алгоритм — ordinal-сортировка относительных путей с `/`, затем SHA-256 UTF-8 конкатенации `path + TAB + uppercase file SHA256 + LF`. Parent build/package configs проверены; дополнительных файлов не обнаружено. `bin/obj/artifacts/tests/.git` и неподключённые optional provider-проекты в исходный набор не входят.

Сборка конкретного Maintenance.SqlServer.csproj собирает также base и Maintenance по ProjectReference. Использованы Release/net10.0, отдельный `artifacts/compile-check/efcorelibrary-08a`, `GeneratePackageOnBuild=false`, `GenerateDocumentationFile=true`, `Deterministic=true`, PathMap и отключённые source-control queries/informational revision. `pack/publish` не выполнялись. После первого Build скопированы только три одноимённых DLL/XML из свежих выходов своих проектов. Повторный Build `--no-restore --no-incremental` дал те же шесть SHA-256; исходники повторно проверены и не изменились. При воспроизведении сравнить не только DLL, но и manifest resolved packages: повторный restore сам по себе не является locked-поставкой.

XML base содержит только assembly element: исходники не имеют публичных XML-комментариев, сборка сообщает 107 upstream CS1591. Это не полная API-документация. XML Maintenance и SqlServer содержит комментарии фактической поставки. Все три XML и их хеши присутствуют в manifest; PDB не входит в поставку.

Историческая проверка08A использовала .NET/ASP.NET Core10.0.12, EF Core/Relational/SqlServer10.0.12, DI.Abstractions/Logging.Abstractions10.0.12, SqlClient7.0.2 и его transitive/runtime/native assets. DLL-reference не восстанавливает NuGet автоматически: EF/provider и SqlClient явно подключены в CostVision, DI/logging доступны через согласованный Microsoft.AspNetCore.App. SDK pruning удаляет эти framework-пакеты из Web closure; standalone library closure сохранён в manifest отдельно. В той проверке Design/Tools также10.0.12, PrivateAssets=all. Текущий consumer graph после обновления пользователем описан ниже. Копирование только трёх DLL не является полноценным runtime deployment; Linux/publish приёмка остаётся открытой.

## Используемая поверхность EFCoreLibrary

После согласованного пользователем обновления зависимостей15.10.2026 текущие версии пакетов приложения фиксируются отдельно в [consumer manifest](costvision-consumer-packages.json): в том числе EF/provider10.0.13, SqlClient7.1.1, Bcl.Cryptography9.0.18 и Pkcs10.0.13. Его21 package identity/NuGet content hash сверены с Infrastructure project.assets.json и metadata локального NuGet cache. Исторический manifest08A описывает сборку неизменённых DLL и не задаёт точные версии текущего consumer graph. Stage15DeliveryTests сохраняет точное сравнение identities/SHA-512 и обязательное наличие всех21 packages, а runtime requirements разрешает через принятый consumer graph либо явный shared framework. Отсутствие уязвимостей live audit и совместимость SQL/production этим тестом не подтверждаются.

- IEntity<Guid> имеет единственное свойство Guid Id с getter и setter. Domain по-прежнему ссылается на всю DLL. Actual metadata base содержит Microsoft.EntityFrameworkCore `10.0.12.0`, DI.Abstractions/System.Runtime `10.0.0.0`; package version и AssemblyVersion различаются. Полная независимость Domain от EF не утверждается.
- Application repository-интерфейсы наследуют ICreateItemRepository<TEntity>, IDeleteItemRepository<TEntity>, IGetItemByIdRepository<TEntity,TId>, IGetItemByPredicateRepository<TEntity> без context key. AppDbContextBase остаётся в Application, как и EF/predicate/include/tracking/projection по решению А.
- List API возвращает Task<List<TEntity>>, не произвольный TResult. Defaults: predicate=null, skip=0, take=null, asNoTracking=false, include=null, ct=default. Include — Func<IQueryable<TEntity>, IQueryable<TEntity>>.
- Infrastructure внедряет IContext*Repository<TEntity,CostVisionContextKey> (для ID также TId); IContextQueryRepository наследует IQueryRepository<TEntity>. Query(true), predicate, composition и ToListAsync(ct) исполняют общий projection. Product/Store count также используют библиотечный Query(true), без DbContext.Set.
- AddEfCoreContext<ApplicationContext,CostVisionContextKey>() и AddEfCoreBaseRepositories<CostVisionContextKey>() связывают все repositories, IUnitOfWorkContext и maintenance с одним scoped ApplicationContext. Дублирующие старые adapters удалены. UnitOfWorkScope использует IUnitOfWorkContext.SaveChangesAsync/Database, сохраняет общий флаг отказа, 14 adapters, auto-save/explicit-save перегрузки и отсутствие retry.
- IDatabaseMaintenance<CostVisionContextKey> — единственный inspection/backup/create/migration coordinator; подключён только SQL Server optional provider. Регистрация не выполняет I/O. UpdateExistingAsync сохраняет backup-before-migrate, InitializeNewAsync выбирается явно; no-pending → Unchanged без backup. SingleInitializer, поддержанные endpoint/права/backup directory и readiness описаны в [эксплуатации](../Documentation/TechnicalGuide/11-configuration-and-operations.md).

## Используемая поверхность HttpClientLibrary

Infrastructure регистрирует typed IHttpApiClient/HttpApiClient через AddHttpClient с timeout из валидируемых ExternalRequestOptions, default180 секунд сохранён в14. ReceiptRequest вызывает PostAsync<TRequest,TResponse>(url, requestDto, ct:ct), в том числе PostAsync<HttpContent,ProverkachekaResponse> для multipart. Прочие возможности, автоматические retry, классификация ошибок и живой внешний контракт не предполагаются по имени библиотеки. Provider/refresh/retry проекта в 08 не меняются.

Actual generic PostAsync возвращает Task<TResponse> и принимает string, TRequest, string, IDictionary<string,string>, CancellationToken; есть отдельный негeneric PostAsync с object и Task. Названия/nullable/defaults дополнительных строковых параметров здесь не предполагаются. Metadata HttpClientLibrary ссылается на System.Runtime/System.Net.Http/System.Text.Json и Logging.Abstractions 10.0.0.0; прямой EF-ссылки в её списке зависимостей нет. Полный список и сигнатуры зафиксированы в TRX Stage08BoundaryTests.

## Проверка и обновление

Повторная проверка15.10.2026: исходные и loaded/output DLL/XML hashes/versions совпали с принятым manifest08A; HTTP hash сохранён. Stage15DeliveryTests проверяет actual loaded файлы и восстановленный Infrastructure package graph (identities/SHA-512/runtime requirements). Полный Infrastructure isolated suite198/0/0. Отдельный локальный аудит Web assets подтвердил58 consumer runtime identities/paths,20 общих standalone package SHA-512 и наличие72 final Web.deps runtime/native paths. Это build output, не clean publish; restore/переаттестация upstream/Docker/Linux/ARM64/network не выполнялись. Точные evidence/ограничения — [журнал15](../Documentation/Plans/project-refactoring-2026-10-13/15-build-tests-and-final-acceptance.md), дальнейшая проверка — [матрица](../Documentation/Plans/project-refactoring-2026-10-13/15-acceptance-matrix.md). DLL/XML/manifest не обновлялись в15.

Metadata/compile-check и изолированные actual DLL/DI/fake-connection проверки подтверждают только используемую поверхность. Они не доказывают SQL-материализацию, ограничения, атомарность/гонки, live HTTP или production. При обновлении отдельно сверять версии/хеши, сигнатуры/defaults, registrations, единый tracker, обе перегрузки transaction и regressions 01–07A.

Историческая проверка 08: 115/0/0 с EF Core 10.0.10 и DLL 0.0.2; TRX `artifacts/compile-check/test-results/infrastructure-stage08-final/Spike_PC_2026-10-14_16_57_42_net10.0.trx` сохранён как прежнее evidence. Проверка новой поставки 08A: Infrastructure 168/0/0, Application 496/0/0, Web 86/0/0; команды и актуальные TRX указаны в [журнале 08A](../Documentation/Plans/project-refactoring-2026-10-13/08a-efcorelibrary-upgrade-and-maintenance.md). Изолированные actual DI/metadata/fake command tests не закрывают SQL/backup/restore/HTTP/browser/production-приёмку.

[Предложение границы 08](../Documentation/Plans/project-refactoring-2026-10-13/08-boundary-decision.md), [владельцы транзакций](../Documentation/Plans/project-refactoring-2026-10-13/07a-consumer-map.md), [описание интеграций](../Documentation/TechnicalGuide/10-integrations.md).
