# Хранение данных и интеграции

[ApplicationContext](../CostVision.Infrastructure/DataBase/ApplicationContext.cs) — EF Core-контекст приложения. Конфигурации моделей находятся в `CostVision.Infrastructure/DataBase/ModelsConfigure`; они описывают таблицы, ключи, индексы и связи.

Use case не работает с контекстом напрямую. Он использует [IUnitOfWork](../CostVision.Application/Abstractions/DataBase/Repositories/IUnitOfWork.cs) и небольшие репозитории, например [IReceiptRepository](../CostVision.Application/Abstractions/DataBase/Repositories/Receipts/IReceiptRepository.cs). Запрос, фильтр и нужные `Include` задаёт use case, а репозиторий остаётся тонким доступом к данным.

Когда несколько изменений должны быть сохранены вместе, use case вызывает `ExecuteInTransaction` у unit of work. Реализация [UnitOfWork](../CostVision.Infrastructure/DataBase/Repositories/UnitOfWork.cs) открывает транзакцию и сохраняет результат как одно действие.

Интеграция с сервисом проверки чеков начинается с [ExternalReceiptProvider](../CostVision.Infrastructure/Services/Receipts/ExternalReceiptProvider.cs). Он получает внешние данные, а [ProverkachekaReceiptMapper](../CostVision.Infrastructure/Services/Converters/ProverkachekaReceiptMapper.cs) переводит их в внутренние модели. Миграции в `DataBase/Migrations` — исторические сгенерированные файлы; их не следует редактировать вручную.
