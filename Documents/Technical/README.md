# Техническая документация CostVision

Документация описывает реализованное состояние проекта по исходному коду на 24 августа 2026 года. Бизнес-поведение без деталей реализации вынесено в [Technical requirements.md](../Technical%20requirements.md).

## Навигация

1. [Обзор системы](01-system-overview.md) — назначение, состав решения, технологии и границы.
2. [Архитектура и зависимости](02-architecture.md) — слои, DI, путь запроса, результаты и транзакции.
3. [Авторизация, пользователи и счета](03-authorization-and-accounts.md) — cookie-вход, роли и модель совместного доступа.
4. [Чеки, товары и магазины](04-receipts-and-catalogs.md) — создание, дедупликация, внешний refresh и каталоги.
5. [Денежные операции и отчётность](05-money-movements-and-reporting.md) — ручной ввод, PDF-импорт, сверка и dashboard.
6. [Web, данные, интеграции и эксплуатация](06-web-data-and-operations.md) — Razor Pages, TypeScript, SQL Server, конфигурация, startup и Docker.
7. [Тестирование и сопровождение](07-testing-and-maintenance.md) — тестовые контуры, регрессионные зоны и правила обновления документов.

## Основные точки входа

- [Program.cs](../../CostVision.Web/Program.cs) — сборка и запуск приложения.
- [регистрация Web](../../CostVision.Web/Web/Extensions/ServiceCollectionExtensions.cs).
- [регистрация Application](../../CostVision.Application/Extensions/ApplicationServiceCollectionExtensions.cs).
- [регистрация Infrastructure](../../CostVision.Infrastructure/Extensions/InfrastructureServiceCollectionExtensions.cs).
- [ApplicationContext](../../CostVision.Infrastructure/DataBase/ApplicationContext.cs) — EF Core-модель.
- [IUnitOfWork](../../CostVision.Application/Abstractions/DataBase/Repositories/IUnitOfWork.cs) — граница доступа к данным и транзакций.
- [JsonResultMapper](../../CostVision.Web/Web/Mappers/JsonResultMapper.cs) — отображение application-результатов в HTTP.

## Термины

- **Счёт (Account)** — бюджет или кошелёк, объединяющий чеки, денежные операции и участников.
- **Владелец** — создатель счёта и неизменяемый участник с полными правами.
- **Viewer / Editor** — роли участника счёта: просмотр или редактирование.
- **Чек** — фискальный документ с собственником, реквизитами, позициями и привязками к счетам.
- **Денежная операция** — доход или расход по одному счёту, созданный вручную либо импортированный.
- **Сверка** — связь `MoneyMovementReceipt` между банковской/ручной операцией и чеком.
- **Адаптивное название** — локальное отображаемое имя товара или магазина без изменения исходных данных чека.
