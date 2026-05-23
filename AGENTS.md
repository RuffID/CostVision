# Правила для агентов

## Описание проекта

CostVision - веб-приложение для учёта расходов по чекам и счетам.

Технический стек:

- ASP.NET Core Razor Pages в проекте `CostVision`.
- TypeScript для клиентской логики в `CostVision/wwwroot/ts`.
- Слои `Domain`, `Application`, `Infrastructure`, `Web`.
- Entity Framework Core через `ApplicationContext`.
- Доступ к данным через `IUnitOfWork` и репозитории.
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
- `CostVision` содержит Razor Pages, frontend-скрипты, middleware, авторизационные атрибуты и web-инфраструктуру.

## Репозитории и доступ к данным

- В репозиториях не добавлять специализированные методы для конкретных выборок вроде `GetByIdWith...`, `Get...With...`, `GetAccessible...`, `GetWithout...` и похожих.
- Для чтения использовать базовые методы репозитория: `GetItemByPredicateAsync`, `GetItemsByPredicateAsync` или `GetItemByIdAsync` с `predicate` и нужным `include`.
- Фильтры и графы загрузки (`Include`, `ThenInclude`, `AsSplitQuery`) описывать на стороне вызывающего кода, если это не нарушает существующий контракт слоя.
- Репозитории должны оставаться тонкими обертками над базовыми CRUD/query-абстракциями и не содержать бизнес-сценарии.

## Миграции

- Миграции Entity Framework создаёт пользователь вручную через консоль.
- Агент не должен создавать миграции сам и не должен выполнять команды добавления миграций.
- При изменении модели данных агент должен указать, что после правок пользователю нужно создать миграцию.

## Use Case и бизнес-логика

- Бизнес-сценарии размещать в use case слоя `Application`.
- PageModel не должен содержать бизнес-логику: он принимает request, вызывает use case и возвращает JSON/result.
- Проверки доступа пользователя к счетам и чекам держать в use case или специализированных application helper/service, а не в Razor Page.
- Транзакционные сценарии выполнять через `IUnitOfWork.ExecuteInTransaction`.

## Frontend

- Для новых клиентских скриптов использовать TypeScript.
- AJAX-обмен строить через общие HTTP helper из `wwwroot/ts/shared`.
- Динамический DOM собирать через DOM API, без строковой HTML-разметки.
- Страницы с AJAX-логикой должны работать без полной перезагрузки для основных действий.
