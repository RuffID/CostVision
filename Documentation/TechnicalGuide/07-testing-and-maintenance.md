# Тестирование и сопровождение

## Тестовые проекты

Актуальная проверка15.10.2026: полные isolated Application503/Infrastructure198/Web101/frontend137 — **939 passed,0 failed,0 skipped,0 cancelled/aborted**, strict TS exit0. Build конкретных Domain/Application/Infrastructure/Web и compile-only Integration —0errors; последние три имеют по2 прежних NU1510 из restore cache, warnings не подавлены. Точные команды/TRX/ограничения — [журнал15](../Plans/project-refactoring-2026-10-13/15-build-tests-and-final-acceptance.md). SQL/HTTP/browser/production и чистая поставка остаются открытыми по [матрице](../Plans/project-refactoring-2026-10-13/15-acceptance-matrix.md).

SDK10.0.401 закреплён в [global.json](../../global.json); после пользовательского обновления xunit.v3 до4.0.2 выбран Microsoft.Testing.Platform. Текущая команда — dotnet test --project <csproj>, отчёт — --report-xunit-trx (после отдельного Build: --no-build/--no-restore). Прежние команды VSTest в журналах — исторические. Node22.18.0 — [.node-version](../../.node-version), npm10.9.3 — packageManager и явная version check. [NuGet.Config](../../NuGet.Config) очищает inherited sources и задаёт nuget.org. Restore/install выполняются отдельно и требуют разрешения; .NET compile-check использует конкретный csproj/Build/no-restore/BaseOutputPath=artifacts/compile-check, для Web/Integration — SkipFrontendBuild=true. TypeScript MSBuild и host-based OpenAPI generation заблокированы; .NET build не устанавливает npm dependencies.

[CI](../../.github/workflows/main.yml) остаётся ручным workflow_dispatch: frontend выполняет явный npm ci без lifecycle scripts, strict build и node tests; matrix трёх UnitTests делает explicit project restore/build/test; Integration — отдельный compile-only job. Restore в CI явно использует NuGet.Config; build/test — no-restore, test — no-build. Внешние проверки не включены в автоматический запуск. CI не выполнялся в15. **CD удалён по решению пользователя**, deployment отсутствует.

| Проект | Назначение | Характерные зоны |
|---|---|---|
| `CostVision.Application.UnitTests` | Domain-инварианты, DTO mapping и use case на mock UoW | пользователи, счета, чеки, refresh, операции, import, reconciliation |
| `CostVision.Infrastructure.UnitTests` | configurations, actual DLL/DI/UoW/maintenance с fake границами | hashing, QR, converters, provider, receipt/target, readiness/cleanup, safe logs |
| `CostVision.Infrastructure.IntegrationTests` | SQL Server и составные компоненты | migrations, CRUD, constraints, cascade, PDF, auth HTTP; прежние собственные backup/checkup tests удалены в 08A |
| `CostVision.Web.UnitTests` | PageModel и web adapters | handlers, JSON mapping, middleware, forwarded headers |

Все проекты используют .NET 10 и xUnit v3. Unit tests используют Moq. Integration tests поднимают SQL Server через Testcontainers и требуют доступный Docker engine; часть component tests использует реальные файловые/процессные границы только внутри тестового окружения.

## Области, для которых существуют тесты

Список отражает наличие тестового кода, а не свежий результат выполнения или полноту защиты от регрессий. Фактические результаты и открытая приёмка приведены раздельно в журналах этапов.

- создание, редактирование, вход и активность пользователя;
- password validation и hashing;
- роли счёта, участники и защита владельца;
- создание, перенос, удаление и доступ к чекам;
- QR parsing, внешний mapping и полный refresh workflow;
- retry policy и hosted background service;
- ручные операции, PDF parser’ы всех трёх банков и импорт дублей;
- ручная и автоматическая сверка;
- финансовая изоляция: FinancialDataIsolationTests применяет фактические предикаты репозиториев к доменным графам и проверяет DTO списков, деталей, обоих направлений связей/кандидатов, фильтров, счётчиков и сумм; включает отзыв участия и Admin;
- EF mapping, migration path, repository CRUD и cascade delete;
- web authorization, `JsonResultMapper` и exception middleware.

## Зоны, которые особенно важно проверять при регрессии

1. Изоляция финансовых данных по owner/membership во всех read и mutation queries.
2. Разница прав Viewer, Editor и Owner, включая архивные счета.
3. Дедупликация одного чека внутри пользователя и внутри общего счёта.
4. Атомарная замена позиций при refresh и отсутствие частичного графа при плохом внешнем ответе.
5. Предел семи retry, UTC/local граница полуночи и повторный запуск hosted service.
6. PDF без text layer, изменение формата банка, дубли preview/БД и явная замена.
7. Однозначность auto-link: совпадение суммы и даты не должно выбирать случайную пару.
8. Отсутствие двойного расхода в combined dashboard и корректное заполнение пустых периодов.
9. EF unique constraints, delete behavior и rollback многошаговых сценариев на реальном SQL Server.
10. Backup до migration и fail-fast startup при ошибке БД.
11. Единый JSON/HTTP contract и antiforgery для изменяющих handlers.
12. Поколения независимых чтений, A-после-B/stale error/loading/finally, signal, close/reopen/hidden, captured command IDs и preview identity/unknown write outcome.

## Как выбирать уровень проверки

- Чистое правило сущности — быстрый unit test без EF.
- Application orchestration — unit test с mock repositories, проверяющий результат и отсутствие лишних writes.
- LINQ expression, index, cascade, transaction или migration — SQL Server integration test; EF InMemory не заменяет реляционную проверку.
- До SQL-приёмки финансовой изоляции выражения проверяются изолированно через Compile над доменными объектами. Это доказывает логическую фильтрацию и DTO, но не перевод EF-запроса, выполнение include или согласованность нескольких SQL-чтений при конкурентном отзыве участия.
- Page handler или error mapping — Web unit test; authentication/cookie boundary — integration test с TestServer.
- Изменение bank PDF parser — отдельные samples для успешных строк, комиссий/возвратов, многострочного текста и неизвестного формата.
- Изменение TypeScript — `npm run build:ts`; при существенном UI-потоке также ручная проверка модалок и AJAX без reload.

Собирать и тестировать следует затронутые `.csproj`, а не solution целиком. Для compile-check проекта рекомендуется отдельный `BaseOutputPath`/`BaseIntermediateOutputPath` под `artifacts/compile-check` и `--no-restore`, если зависимости уже восстановлены.

Изолированные frontend-проверки после build:ts запускаются через `node --experimental-strip-types --test tests/Frontend/*.test.mts`. requestCoordination.test.mts вызывает actual DOMContentLoaded/controller/API с управляемыми Promise и abort-ignoring fetch; moneyMovementImport.test.mts использует actual preview перед save. [Журнал10](../Plans/project-refactoring-2026-10-13/10-frontend-request-coordination.md) фиксирует новые76 passed/0 failed/0 skipped/0 cancelled и успешный TypeScript compile-check отдельно от старых .NET-результатов09; никаких hosting/browser/SQL/live-запусков10 не было.

12: receiptScanning.test.mts вызывает actual AddReceipt entry/controller/API/storage/camera/file adapter с fake DOM/fetch/vendor/URLs/FileReader. Проверены captures/busy/overlay, known/unknown outcome без retry, identity/legacy/retention/draft, camera revision/start-stop/stale callback/error, lifecycle/bfcache и decoder cleanup/fallback. Финальный весь frontend **124 passed / 0 failed / 0 skipped / 0 cancelled**, strict TS build exit 0. Необходимая identity handler правка проверена только AddReceiptModelTests **5/0/0** и Web build **0 errors / 2 existing NU1510 warnings**. Evidence/commands/open browser/camera/CSP/storage/auth procedure — [журнал 12](../Plans/project-refactoring-2026-10-13/12-qr-scanning-and-browser-storage.md). Неизменённые .NET-наборы, IntegrationTests, browser/камера/hosting/SQL/provider в 12 не запускались.

## Изменения модели данных

Entity configuration меняется вместе с Domain-моделью и integration tests. EF migration создаёт пользователь вручную; migration и model snapshot не редактируются вручную. После добавления migration необходимо проверить путь upgrade на SQL Server и работу backup-before-migrate.

## Сопровождение документации

При изменении поведения обновляются оба уровня, если изменение видно пользователю:

- простое продуктовое правило — [бизнес-логика](../Business/README.md);
- слои, DI или error contract — `02-architecture.md`;
- роли и доступ — `03-authorization-and-accounts.md`;
- чек, внешний API, retry или каталог — `04-receipts-and-catalogs.md`;
- операция, bank parser, reconciliation или dashboard — `05-money-movements-and-reporting.md`;
- handler и HTTP — [Web-слой](06-web-layer.md);
- TypeScript — [клиентский интерфейс](08-frontend.md);
- EF и сохранение — [хранение данных](09-data-and-persistence.md);
- внешний API, PDF и DLL — [интеграции](10-integrations.md);
- config, startup и Docker — [эксплуатация](11-configuration-and-operations.md);
- новый тестовый контур или правило проверки — этот документ.

Ссылки должны вести на исходные файлы, а не на generated migrations, `wwwroot/dist`, `bin` или `obj`. Документация описывает реализованное поведение; планы и предполагаемые функции должны быть явно помечены как будущие.

## Известные технические ограничения

- В solution нет отдельного Domain test project: чистые модели сейчас проверяются из `Application.UnitTests`.
- Нет архитектурного теста, автоматически запрещающего нежелательные project dependencies.
- Swagger metadata использует название `CostVision API`; JSON/UI по умолчанию включены, отключение задаёт оператор. Это не readiness endpoint.
- Фоновый refresh и его расписание находятся в одном web-процессе; распределённого scheduler/lock нет.
- При запуске нескольких web-экземпляров нужен общий Data Protection key ring, а фоновые задания требуют отдельной координации.
- Startup 08A использует единственный библиотечный coordinator до RunAsync хоста, требует явных server directory/SingleInitializer и прав на backup/изменение схемы. Реальные SQL/restore/сигналы shutdown проверяются отдельно; распределённой блокировки и автоматического restore API нет.
- Категории расходов покрыты Domain/EF-моделью, но не имеют пользовательского use case и страницы.
- Изолированные frontend HTTP/API/DOM/coordinator/import-тесты находятся в tests/Frontend. Их успех не закрывает actual HTTP/auth/browser/Bootstrap-потоки; финансовый расчёт и SQL-загрузка имеют отдельные контуры05/07.
- TestWebApplication собирает отдельный тестовый HTTP-конвейер: его успех сам по себе не подтверждает production-композицию Program.cs.
- Существующие тесты не закрывают все дефекты аудита: новые регрессионные сценарии перечислены в соответствующих этапах плана.

Stage02RegressionTests проверяет реальные предикаты доступных покупок, назначаемых ролей, входа и списков счетов на доменных графах, результат и отсутствие мутации/записи при отказе. AuthenticationPasswordCompatibilityTests использует настоящий Hasher и подставное хранение для create/update/login; Web unit tests проверяют передачу CurrentUser.Id и стандартный отказ. Фактические команды, результаты и открытая SQL-/браузерная приёмка указаны в [этапе 02](../Plans/project-refactoring-2026-10-13/02-catalogs-and-user-validation.md).

Stage04RegressionTests выполняет настоящие предикаты над сохранённым набором без pending Create до SaveChanges, проверяет результаты строк и состояние операций/связей. Stage04MoneyPersistenceTests проверяет EF metadata и перевод настоящего предиката импорта без SQL-выполнения. Frontend-тесты запускаются после `npm run build:ts` командой `node --experimental-strip-types --test tests/Frontend/moneyMovementImport.test.mts` с подставным DOM/fetch. Фактические результаты и незакрытая конкурентная SQL-/браузерная приёмка находятся в [этапе 04](../Plans/project-refactoring-2026-10-13/04-money-movement-integrity.md).

08A: actual DLL/DI и coordinator/SqlServer provider исполняются только на fake commands/provider/migration/pin; StartupCancellationTests моделирует отмену без OS signals/host. Infrastructure 168/0/0, Application 496/0/0, Web 86/0/0. Integration project собран, не запущен. Команды и TRX в [журнале 08A](../Plans/project-refactoring-2026-10-13/08a-efcorelibrary-upgrade-and-maintenance.md). Старая SQL/backup/restore/HTTP/browser/production-приёмка всех этапов остаётся открытой.
