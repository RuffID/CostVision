# Архитектура

Приложение разделено так, чтобы правило предметной области не зависело от страницы, базы данных или внешнего сервиса.

- `CostVision.Domain` содержит сущности: например, [Receipt](../CostVision.Domain/Models/Receipts/Receipt.cs), [Account](../CostVision.Domain/Models/Receipts/Account.cs) и [MoneyMovement](../CostVision.Domain/Models/MoneyMovements/MoneyMovement.cs). Здесь проверяется, можно ли изменить объект.
- `CostVision.Application` описывает сценарии пользователя. Например, [SaveManualReceiptUseCase](../CostVision.Application/UseCases/Receipts/Receipts/SaveManualReceiptUseCase.cs) создаёт чек, а [ImportMoneyMovementsUseCase](../CostVision.Application/UseCases/MoneyMovements/BankStatementImports/ImportMoneyMovementsUseCase.cs) сохраняет данные банковской выписки.
- `CostVision.Infrastructure` реализует работу с PostgreSQL/EF Core, файловой системой и внешним API проверки чеков.
- `CostVision.Web` принимает HTTP-запросы и отдаёт Razor Pages и JSON. В нём нет бизнес-решений: страница вызывает use case и преобразует его результат в HTTP-ответ.

Обычный путь запроса выглядит так: страница → use case → репозитории и доменные модели → сохранение изменений. Use case возвращает [ServiceResult](../CostVision.Application/Models/Responses/Results/ServiceResult.cs), а [JsonResultMapper](../CostVision.Web/Web/Mappers/JsonResultMapper.cs) выбирает для него HTTP-статус и JSON.

Состав зависимостей собирается в [Program.cs](../CostVision.Web/Program.cs). Регистрации вынесены в расширения [ApplicationServiceCollectionExtensions](../CostVision.Application/Extensions/ApplicationServiceCollectionExtensions.cs), [InfrastructureServiceCollectionExtensions](../CostVision.Infrastructure/Extensions/InfrastructureServiceCollectionExtensions.cs) и [ServiceCollectionExtensions](../CostVision.Web/Web/Extensions/ServiceCollectionExtensions.cs).
