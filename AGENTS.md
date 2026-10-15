# Правила для агентов

## Описание проекта

CostVision - веб-приложение для учёта расходов по чекам и счетам.

Технический стек:

- ASP.NET Core Razor Pages в проекте `CostVision.Web`.
- TypeScript для клиентской логики в `CostVision.Web/wwwroot/ts`.
- Слои `Domain`, `Application`, `Infrastructure`, `Web`.
- Entity Framework Core через `ApplicationContext`.
- Доступ к данным через узкие сценарные UoW и общий `IUnitOfWorkScope`; read-only получает репозитории напрямую.
- Результаты use case возвращаются через `ServiceResult` / `ServiceResult<T>` и мапятся в JSON через `JsonResultMapper`.

Основные предметные области:

- Пользователи, роли и авторизация.
- Счета (`Account`) и участники счетов (`AccountMember`).
- Чеки (`Receipt`), позиции чеков (`ReceiptItem`), товары (`Product`) и связи чеков со счетами (`ReceiptAccount`).
- Добавление чеков вручную и через QR-код.
- Отчёты по чекам и счетам.

## Архитектура слоёв

- `CostVision.Domain` содержит доменные модели и enum.
- `CostVision.Application` содержит use case, DTO, request/response модели, абстракции сервисов и репозиториев.
- `CostVision.Infrastructure` содержит реализации репозиториев, EF Core configuration, миграции, внешние API и фоновые сервисы.
- `CostVision.Web` содержит Razor Pages, frontend-скрипты, middleware, авторизационные атрибуты и web-инфраструктуру.

## Документация

- [Documentation/README.md](Documentation/README.md) — общая навигация.
- [Documentation/Business](Documentation/Business/README.md) — бизнес-правила простым языком.
- [Documentation/TechnicalGuide](Documentation/TechnicalGuide/README.md) — фактическое устройство и ограничения реализации.
- [Documentation/Plans](Documentation/Plans/README.md) — этапы будущих изменений, статусы и критерии приёмки.
- Файлы и папки документации именовать на английском, содержимое писать на русском. Не выдавать запланированные исправления за выполненные.

## Локальные AGENTS.md

- Каждая папка с собственными исходниками имеет локальный AGENTS.md с назначением, контрактами, границами и подходящими проверками. Инструкции родителей продолжают действовать.
- После изменения .cs, .ts, .cshtml, .css и других собственных исходников в той же задаче сверить и актуализировать AGENTS.md папки-владельца. При изменении общей границы обновить также соответствующий родительский документ.
- При создании папки исходников создать её AGENTS.md; при перемещении кода актуализировать описания прежнего и нового владельца. При изменении публичного поведения обновить также бизнес- и техническую документацию.
- Не вести фиктивный журнал каждой правки, не добавлять неподтверждённые проверки и не менять описание на предполагаемую будущую реализацию.
- Не создавать такие инструкции в bin, obj, artifacts, node_modules, generated migrations, wwwroot/dist, стороннем wwwroot/lib, каталогах runtime-данных.
- Это обязательное сопровождение в рамках изменений, а не автоматический генератор по событию сохранения файла.
- Локальные AGENTS.md хранить непосредственно в папках исходников и отображать внутри соответствующих проектов. Не создавать отдельную папку или виртуальную группу solution, дублирующую дерево кода ради инструкций.
- После добавления документа обновить навигацию и соответствующие элементы CostVision.slnx. Корневые README.md и AGENTS.md находятся в SolutionItems; локальные инструкции не дублировать как отдельные элементы solution.

## Репозитории и доступ к данным

- В репозиториях не добавлять специализированные методы для конкретных выборок вроде `GetByIdWith...`, `Get...With...`, `GetAccessible...`, `GetWithout...` и похожих.
- Для чтения использовать базовые методы репозитория: `GetItemByPredicateAsync`, `GetItemsByPredicateAsync` или `GetItemByIdAsync` с `predicate` и нужным `include`.
- Для count, скалярных агрегатов и страниц разрешён согласованный общий `GetProjectedItemsAsync<TResult>(predicate, query, ct)`: query-композицию задаёт Application, Infrastructure исполняет её без tracking через `IQueryRepository.Query` поставленной DLL. Специализированные выборки и SQL вручную не добавлять.
- Фильтры и графы загрузки (`Include`, `ThenInclude`, `AsSplitQuery`) описывать на стороне вызывающего кода, если это не нарушает существующий контракт слоя.
- Репозитории должны оставаться тонкими обертками над базовыми CRUD/query-абстракциями и не содержать бизнес-сценарии.

Архитектурное решение 08 (14.10.2026): пользователь принял сохранение явной EF/DLL-границы Application. AppDbContextBase, DLL CRUD, predicate/include/tracking и общий projection-контракт остаются. Их массовую замену, включая независимый query-язык, не начинать без отдельного решения. Контракты и неизвестные сведения поставки: [libs/README.md](libs/README.md); принятое решение: [08-boundary-decision.md](Documentation/Plans/project-refactoring-2026-10-13/08-boundary-decision.md).

После 08A все Infrastructure repositories используют IContext*Repository с единственным CostVisionContextKey; сохранение/транзакции — сценарный UoW над IUnitOfWorkContext того же scoped ApplicationContext. Inspection/backup/create/migrate принадлежат только IDatabaseMaintenance и SQL Server-модулю. Собственные SqlConnection/SqlCommand, SQL/Dapper, business DbContext.Set/CRUD и второй maintenance runner запрещены. Модель/provider registration и generated migrations не являются обходом. Поставка и блокеры SingleInitializer/readiness описаны в libs/README.md и технической эксплуатации; изолированная реализация не подтверждает SQL/production.

## Миграции

- Миграции Entity Framework создаёт пользователь вручную через консоль.
- Агент не должен создавать миграции сам и не должен выполнять команды добавления миграций.
- При изменении модели данных агент должен указать, что после правок пользователю нужно создать миграцию.

## Проверка сборки и тесты

После 15 toolchain задают global.json (SDK 10.0.401), .node-version (22.18.0) и packageManager npm@10.9.3. NuGet.Config задаёт единственный явный source. .NET build не вызывает npm ci; npm dependencies устанавливаются отдельно без lifecycle scripts. Ручной CI разделяет frontend/unit/Integration compile-only. CD workflow удалён по просьбе пользователя; возвращение deployment automation требует отдельного решения. Успех isolated CI не закрывает SQL/HTTP/browser/production-приёмку и SingleInitializer.

- Агент может по своему усмотрению без отдельного разрешения выполнять compile-check затронутых SDK-style .NET-проектов через `dotnet build` и запускать относящиеся к изменениям изолированные тесты через `dotnet test`.
- Агент может по своему усмотрению без отдельного разрешения выполнять `npm run build:ts` из каталога `CostVision.Web` для compile-check TypeScript и обновления генерируемых frontend-файлов.
- Собирать и тестировать конкретные `.csproj`, не solution целиком. Для compile-check использовать `Build`, отдельный каталог `artifacts/compile-check` и по возможности `--no-restore`.
- Не запускать приложение, hosting, Docker, внешние интеграции, тесты с реальной БД, сетью или системными процессами без отдельного прямого разрешения пользователя.
- Не выполнять EF Core migrations и команды их создания.

## Use Case и бизнес-логика

- Бизнес-сценарии размещать в use case слоя `Application`.
- PageModel не должен содержать бизнес-логику: он принимает request, вызывает use case и возвращает JSON/result.
- AJAX handlers проверяют binding/ModelState и обязательную форму request до вызова сценария. Cookie сохраняет HTML-редиректы, для JSON/AJAX возвращает 401/403; неожиданные HTTP-ошибки принадлежат ExceptionHandlingMiddleware с проверкой RequestAborted/Response.HasStarted. Общий HTTP-helper проверяет envelope и принимает необязательный AbortSignal. Dashboard, каталоги, отчёт чеков и операции используют независимые поколения чтений и снимки параметров; закрытие/смена контекста и pagehide инвалидируют их. Команды захватывают ID/вход до await, не отменяют запись и не повторяют POST автоматически; неизвестный исход требует сверки по серверному списку.
- Проверки доступа пользователя к счетам и чекам держать в use case или специализированных application helper/service, а не в Razor Page.
- Транзакционные сценарии выполнять через сценарный UoW / `IUnitOfWorkScope.ExecuteInTransaction`. Все обёртки одного DI scope используют один контекст и общий флаг отказа; у составной операции один владелец транзакции.

## Frontend

После 14 config.json читается от content root, environment и command line имеют больший приоритет. Все startup options проверяются до библиотечного maintenance и host. Согласованные лимиты банковских PDF — 20 МиБ/200 страниц; они не относятся к QR/camera/storage. Автоматическая startup-policy 08A и её эксплуатационные блокеры сохранены; кооперативный budget не гарантирует остановку OS/server I/O.

Добавление чека после 12 имеет независимую session/controller и общий busy/lifecycle для QR/manual. Чувствительные browser keys персональны по проверенному CurrentUser.Id; legacy не читать/не мигрировать/не удалять, TTL и logout deletion не вводить. Новых лимитов decoding нет по решению пользователя. POST не отменяется и не повторяется автоматически; неизвестный исход требует сверки. Браузерная/camera/storage приёмка открыта.

- Для новых клиентских скриптов использовать TypeScript.
- AJAX-обмен строить через общие HTTP helper из `wwwroot/ts/shared`.
- Динамический DOM собирать через DOM API, без строковой HTML-разметки.
- Страницы с AJAX-логикой должны работать без полной перезагрузки для основных действий.
- При открытии Bootstrap-модальных окон использовать общий стек модалок из `wwwroot/ts/shared/bootstrap.ts`: для первого окна backdrop должен плавно затемнять страницу вместе с открытием модалки, для вложенного окна тот же backdrop должен располагаться между нижней и верхней модалкой и плавно затемнять нижнюю модалку. Фокус, клики и Escape должны принадлежать верхнему окну.

После 13 shared stack использует публичные focus/keyboard options Bootstrap 5.1.0, владельцев событий/поколений и backdrop. Dispose ожидает hidden, отменённый hide не обходится; command locks и snapshots страниц остаются у callers. Изолированные тесты не закрывают actual browser focus/animation/accessibility.
