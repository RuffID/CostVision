# Техническое руководство CostVision

Руководство описывает устройство проекта по исходникам с актуализацией 09–10 на 14.10.2026. [Бизнес-раздел](../Business/README.md) объясняет правила продукта, а [план рефакторинга](../Plans/project-refactoring-2026-10-13/README.md) фиксирует обнаруженные дефекты и будущие изменения. Описание кода не является подтверждением запуска, прохождения тестов или проверки рабочей базы.

## Навигация

| Раздел | Что в нём описано |
|---|---|
| [01. Обзор системы](01-system-overview.md) | Назначение, технологии, проекты и основные потоки |
| [02. Архитектура](02-architecture.md) | Слои, зависимости, регистрация сервисов и транзакционные границы |
| [03. Авторизация и счета](03-authorization-and-accounts.md) | Вход, роли, участники и различия проверок доступа |
| [04. Чеки и каталоги](04-receipts-and-catalogs.md) | Фискальная идентичность, создание, обновление, товары и магазины |
| [05. Операции и отчётность](05-money-movements-and-reporting.md) | Импорт, сверка и расчёт доходов и расходов |
| [06. Web и HTTP](06-web-layer.md) | Страницы, обработчики, текущий пользователь и ответы |
| [07. Тестирование](07-testing-and-maintenance.md) | Контуры проверки, ограничения доказательств и сопровождение |
| [08. Клиентский интерфейс](08-frontend.md) | Модули TypeScript, запросы, сканирование и модальные окна |
| [09. Хранение данных](09-data-and-persistence.md) | Модель EF, индексы, репозитории и сохранение |
| [10. Интеграции](10-integrations.md) | Внешний сервис чеков, PDF и локальные библиотеки |
| [11. Эксплуатация](11-configuration-and-operations.md) | Конфигурация, запуск, backup, журналирование и Docker |
| [12. Карта исходников](12-source-map-and-maintenance.md) | Где искать код и как поддерживать локальные инструкции |

## Точки входа

- [Program.cs](../../CostVision.Web/Program.cs) — конфигурация и HTTP-конвейер.
- [Регистрация Application](../../CostVision.Application/Extensions/ApplicationServiceCollectionExtensions.cs) — сценарии и парсеры.
- [Регистрация Infrastructure](../../CostVision.Infrastructure/Extensions/InfrastructureServiceCollectionExtensions.cs) — EF, внешние сервисы и фоновые задачи.
- [Регистрация Web](../../CostVision.Web/Web/Extensions/ServiceCollectionExtensions.cs) — страницы, cookie, политики и Data Protection.
- [ApplicationContext](../../CostVision.Infrastructure/DataBase/ApplicationContext.cs) — модель хранения.
- [IUnitOfWorkScope](../../CostVision.Application/Abstractions/DataBase/Repositories/IUnitOfWorkScope.cs) — общий scope сохранения/транзакции без репозиториев; [карта 07A](../Plans/project-refactoring-2026-10-13/07a-consumer-map.md) — узкие UoW и владельцы сценариев.
- [JsonResultMapper](../../CostVision.Web/Web/Mappers/JsonResultMapper.cs) — преобразование результата в HTTP.
- [HTTP-контракт09](../Plans/project-refactoring-2026-10-13/09-web-and-http-contracts.md) — binding/форма запроса, календарные границы, JSON401/403, отмена и неожиданные ошибки; [координация10](../Plans/project-refactoring-2026-10-13/10-frontend-request-coordination.md) — поколения/отмена чтений и контекст команд; [промпт11](../Plans/project-refactoring-2026-10-13/prompts/next-stage-11-prompt.md) — передача для отдельного начала декомпозиции.
- [Принятая EF/DLL-граница08](../Plans/project-refactoring-2026-10-13/08-boundary-decision.md) и [поставка библиотек](../../libs/README.md) — решение А, actual версии/хеши/контракты и неизвестные сведения.

## Термины

Счёт объединяет покупки, операции и участников. Чек хранит состав покупки и фискальные реквизиты; денежная операция отражает доход или расход по одному счёту. Сверка связывает эти записи. Адаптивное название меняет отображение товара или магазина; в текущей модели оно общее, а не персональное.
