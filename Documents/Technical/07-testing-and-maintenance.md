# Тестирование и сопровождение

## Тестовые проекты

| Проект | Назначение | Характерные зоны |
|---|---|---|
| `CostVision.Application.UnitTests` | Domain-инварианты, DTO mapping и use case на mock UoW | пользователи, счета, чеки, refresh, операции, import, reconciliation |
| `CostVision.Infrastructure.UnitTests` | configurations и чистые infrastructure-компоненты | hashing, QR, converters, external provider |
| `CostVision.Infrastructure.IntegrationTests` | SQL Server и составные компоненты | migrations, CRUD, constraints, cascade, backup, startup, PDF, auth HTTP |
| `CostVision.Web.UnitTests` | PageModel и web adapters | handlers, JSON mapping, middleware, forwarded headers |

Все проекты используют .NET 10 и xUnit v3. Unit tests используют Moq. Integration tests поднимают SQL Server через Testcontainers и требуют доступный Docker engine; часть component tests использует реальные файловые/процессные границы только внутри тестового окружения.

## Сильные зоны текущего покрытия

- создание, редактирование, вход и активность пользователя;
- password validation и hashing;
- роли счёта, участники и защита владельца;
- создание, перенос, удаление и доступ к чекам;
- QR parsing, внешний mapping и полный refresh workflow;
- retry policy и hosted background service;
- ручные операции, PDF parser’ы всех трёх банков и импорт дублей;
- ручная и автоматическая сверка;
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

## Как выбирать уровень проверки

- Чистое правило сущности — быстрый unit test без EF.
- Application orchestration — unit test с mock repositories, проверяющий результат и отсутствие лишних writes.
- LINQ expression, index, cascade, transaction или migration — SQL Server integration test; EF InMemory не заменяет реляционную проверку.
- Page handler или error mapping — Web unit test; authentication/cookie boundary — integration test с TestServer.
- Изменение bank PDF parser — отдельные samples для успешных строк, комиссий/возвратов, многострочного текста и неизвестного формата.
- Изменение TypeScript — `npm run build:ts`; при существенном UI-потоке также ручная проверка модалок и AJAX без reload.

Собирать и тестировать следует затронутые `.csproj`, а не solution целиком. Для compile-check проекта рекомендуется отдельный `BaseOutputPath`/`BaseIntermediateOutputPath` под `artifacts/compile-check` и `--no-restore`, если зависимости уже восстановлены.

## Изменения модели данных

Entity configuration меняется вместе с Domain-моделью и integration tests. EF migration создаёт пользователь вручную; migration и model snapshot не редактируются вручную. После добавления migration необходимо проверить путь upgrade на SQL Server и работу backup-before-migrate.

## Сопровождение документации

При изменении поведения обновляются оба уровня, если изменение видно пользователю:

- простое продуктовое правило — [бизнес-требования](../Technical%20requirements.md);
- слои, DI или error contract — `02-architecture.md`;
- роли и доступ — `03-authorization-and-accounts.md`;
- чек, внешний API, retry или каталог — `04-receipts-and-catalogs.md`;
- операция, bank parser, reconciliation или dashboard — `05-money-movements-and-reporting.md`;
- handler, TypeScript, EF, config, startup или Docker — `06-web-data-and-operations.md`;
- новый тестовый контур или правило проверки — этот документ.

Ссылки должны вести на исходные файлы, а не на generated migrations, `wwwroot/dist`, `bin` или `obj`. Документация описывает реализованное поведение; планы и предполагаемые функции должны быть явно помечены как будущие.

## Известные технические ограничения

- В solution нет отдельного Domain test project: чистые модели сейчас проверяются из `Application.UnitTests`.
- Нет архитектурного теста, автоматически запрещающего нежелательные project dependencies.
- Swagger metadata пока использует общее название `My API`.
- Фоновый refresh и его расписание находятся в одном web-процессе; распределённого scheduler/lock нет.
- При запуске нескольких web-экземпляров нужен общий Data Protection key ring, а фоновые задания требуют отдельной координации.
- Startup автоматически применяет migrations и поэтому требует у runtime SQL-user прав на backup и изменение схемы.
- Категории расходов покрыты Domain/EF-моделью, но не имеют пользовательского use case и страницы.
