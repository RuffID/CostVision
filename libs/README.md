# Принятая поставка библиотек

Сверено 14.10.2026 по файлам в этой папке, project HintPath и фактическим типам/вызовам проекта. DLL не изменялись.

| Файл | AssemblyVersion | FileVersion | ProductVersion | SHA-256 |
|---|---|---|---|---|
| EFCoreLibrary.dll | 0.0.2.0 | 0.0.2.0 | 0.0.2+fc8acdd067bf9e58e5ece4e3aa28313ddad9be84 | 64A6540852CFD211D48DDE6DF09F3F0A5579C6EABC90E7C26095BFE897E5AB17 |
| HttpClientLibrary.dll | 1.0.0.0 | 0.0.0.3 | 1.0.0+52cd45378cbf3acb050bc886679ffda7ac0c2168 | 37B5EC623F8770CE66ADBCE0CECCB06F45368881AB07152AB5CFE94CC9C16925 |

Обе сборки имеют Culture=neutral и PublicKeyToken=null. Источник принятой поставки внутри репозитория — эти локальные файлы; конкретный upstream URL, процедура сборки, лицензия, проверенный исходный commit и гарантии совместимости этой поставки не установлены. Суффикс ProductVersion сам по себе не доказывает наличие или доступность соответствующего commit. В папке нет исходников, XML-документации или PDB; по имени DLL API не угадывается.

14.10.2026 пользователь указал исходники EFCoreLibrary: `D:\Media\User\source\repos\work\EFCoreLibrary`. Их базовый проект имеет Version `0.0.5`, EF Core/DI.Abstractions `10.0.12`, новые `IContext*Repository` и `IUnitOfWorkContext`; отдельные Maintenance/SqlServer-проекты используют Relational/Logging `10.0.12` и SqlClient `7.0.2`. Принятая DLL `0.0.2` устарела относительно этого источника. Её происхождение от конкретной ревизии ещё не подтверждено, а новые исходники не считаются уже поставленными в CostVision. Обновление base + maintenance, всех ссылок/DI/репозиториев/UoW и собственного backup/migration пути запланировано в [этапе 08A](../Documentation/Plans/project-refactoring-2026-10-13/08a-efcorelibrary-upgrade-and-maintenance.md); текущие DLL не заменены.

## Используемая поверхность EFCoreLibrary

- IEntity<Guid> имеет единственное свойство Guid Id с getter и setter. Сам интерфейс не содержит DbContext, но Domain ссылается на целую EFCoreLibrary; её metadata содержит зависимость Microsoft.EntityFrameworkCore 10.0.3.0 и System.Runtime 10.0.0.0. Поэтому полная независимость Domain от EF-сборки не утверждается.
- Application repository-интерфейсы наследуют ICreateItemRepository, IDeleteItemRepository, IGetItemByIdRepository, IGetItemByPredicateRepository с AppDbContextBase.
- List API возвращает Task<List<TEntity>>, не произвольный TResult. Defaults: predicate=null, skip=0, take=null, asNoTracking=false, include=null, ct=default. Include — Func<IQueryable<TEntity>, IQueryable<TEntity>>.
- IQueryRepository<TEntity,TContext>.Query(bool asNoTracking) возвращает IQueryable<TEntity>. Общая project projection 07 использует Query(true), predicate, composition и ToListAsync(ct); это project adapter, а не выдуманный агрегатный метод DLL.
- Infrastructure регистрирует AddEfCoreBaseRepositories<ApplicationContext>() и EfDbContextAdapter для ApplicationContext/AppDbContextBase, оба на одном scoped ApplicationContext. IAppDbContext<TContext> предоставляет DatabaseFacade Database и Task<int> SaveChanges(CancellationToken). Project IUnitOfWorkScope сохраняет прежний Task-контракт и не возвращает число записей; правила двух транзакционных перегрузок принадлежат проекту, а не обещаются библиотеке.

## Используемая поверхность HttpClientLibrary

Infrastructure регистрирует typed IHttpApiClient/HttpApiClient через AddHttpClient с timeout 180 секунд. ReceiptRequest вызывает PostAsync<TRequest,TResponse>(url, requestDto, ct:ct), в том числе PostAsync<HttpContent,ProverkachekaResponse> для multipart. Прочие возможности, автоматические retry, классификация ошибок и живой внешний контракт не предполагаются по имени библиотеки. Provider/refresh/retry проекта в 08 не меняются.

Actual generic PostAsync возвращает Task<TResponse> и принимает string, TRequest, string, IDictionary<string,string>, CancellationToken; есть отдельный негeneric PostAsync с object и Task. Названия/nullable/defaults дополнительных строковых параметров здесь не предполагаются. Metadata HttpClientLibrary ссылается на System.Runtime/System.Net.Http/System.Text.Json и Logging.Abstractions 10.0.0.0; прямой EF-ссылки в её списке зависимостей нет. Полный список и сигнатуры зафиксированы в TRX Stage08BoundaryTests.

## Проверка и обновление

Metadata/compile-check и изолированные actual DLL/DI/fake-connection проверки подтверждают только используемую поверхность. Они не доказывают SQL-материализацию, ограничения, атомарность/гонки, live HTTP или production. При обновлении отдельно сверять версии/хеши, сигнатуры/defaults, registrations, единый tracker, обе перегрузки transaction и regressions 01–07A.

14.10.2026 actual Stage08BoundaryTests выполнен вместе со всем Infrastructure-набором: 115 passed/0 failed/0 skipped. Проект использует EF Core 10.0.10; отличающаяся compiled-ссылка DLL 10.0.3.0 не объявляется универсальной гарантией совместимости. Проверены конкретные compile/query/DI/fake-connection сценарии; provider SQL и live HTTP остаются открытыми. TRX: artifacts/compile-check/test-results/infrastructure-stage08-final/Spike_PC_2026-10-14_16_57_42_net10.0.trx.

[Предложение границы 08](../Documentation/Plans/project-refactoring-2026-10-13/08-boundary-decision.md), [владельцы транзакций](../Documentation/Plans/project-refactoring-2026-10-13/07a-consumer-map.md), [описание интеграций](../Documentation/TechnicalGuide/10-integrations.md).
