# Архитектура и зависимости

## Composition root и HTTP-конвейер

[Program.cs](../../CostVision.Web/Program.cs) загружает обязательный `Config/config.json`, настраивает Serilog, регистрирует сервисы трёх слоёв и ограничение тела запроса в 1 ГБ. В Development включены `ValidateScopes` и `ValidateOnBuild` для раннего обнаружения ошибок DI.

После `builder.Build()` конвейер выполняется в следующем порядке:

1. forwarded headers;
2. [ExceptionHandlingMiddleware](../../CostVision.Web/Web/Middleware/ExceptionHandlingMiddleware.cs);
3. проверка и обновление базы;
4. production error handler;
5. Swagger;
6. static files и routing;
7. authentication и authorization;
8. Razor Pages и controllers.

Инициализация БД происходит до начала обработки запросов. Ошибка соединения, backup или migration останавливает запуск.

## Слой Domain

Domain хранит допустимое состояние независимо от EF Core и HTTP:

- [User](../../CostVision.Domain/Models/Authorization/User.cs) нормализует профиль, роли, активность и время входа;
- [Account](../../CostVision.Domain/Models/Receipts/Account.cs) владеет участниками, архивным состоянием и правилами ролей;
- [Receipt](../../CostVision.Domain/Models/Receipts/Receipt.cs) контролирует фискальную идентичность, счета, refresh-состояние и замену состава;
- [ReceiptItem](../../CostVision.Domain/Models/Receipts/ReceiptItem.cs) проверяет денежные значения, количество и принадлежность чеку;
- [MoneyMovement](../../CostVision.Domain/Models/MoneyMovements/MoneyMovement.cs) различает ручной ввод и банковский импорт;
- [MoneyMovementReceipt](../../CostVision.Domain/Models/MoneyMovements/MoneyMovementReceipt.cs) представляет сверку операции и чека;
- [Product](../../CostVision.Domain/Models/Receipts/Product.cs), [Store](../../CostVision.Domain/Models/Receipts/Store.cs) и [ExpenseCategory](../../CostVision.Domain/Models/Receipts/ExpenseCategory.cs) защищают каталожные инварианты.

Сущности изменяются через `Try...`-методы. Ожидаемо неверные входные данные возвращают описание ошибки до мутации; коллекции наружу доступны только для чтения.

## Слой Application

Application организован по use case, а не по HTTP-страницам. Здесь находятся:

- сценарии авторизации и пользователей;
- счета и доступ к чекам;
- получение, refresh, просмотр и удаление чеков;
- каталоги товаров и магазинов;
- денежные операции, PDF-импорт и сверка;
- dashboard;
- контракты `IUnitOfWork`, репозиториев, внешнего receipt provider, QR parser, PDF extractor и password hasher.

Регистрации перечислены в [ApplicationServiceCollectionExtensions](../../CostVision.Application/Extensions/ApplicationServiceCollectionExtensions.cs). Application ссылается на EF Core для выражений запросов и `Include`, но не на конкретный `DbContext` или SQL Server provider.

## Слой Infrastructure

Infrastructure реализует application-порты:

- [ApplicationContext](../../CostVision.Infrastructure/DataBase/ApplicationContext.cs) и entity configurations;
- [UnitOfWork](../../CostVision.Infrastructure/DataBase/Repositories/UnitOfWork.cs) и тонкие типизированные репозитории;
- [ExternalReceiptProvider](../../CostVision.Infrastructure/Services/Receipts/ExternalReceiptProvider.cs), HTTP-запрос и преобразователи ответа;
- [QrParser](../../CostVision.Infrastructure/Services/Receipts/QrParser.cs);
- PDF text extractor и три bank statement parser в Application;
- password hashing;
- backup, migration check и ежедневный receipt refresh.

Все EF-репозитории работают в scoped `ApplicationContext`, поэтому один use case использует общий change tracker и одну транзакционную границу.

## Слой Web

PageModel получает текущего пользователя из request context, вызывает use case и возвращает HTML либо JSON. Бизнес-доступ повторно проверяется в Application: скрытая кнопка или закрытая страница не считаются достаточной защитой.

AJAX-ответы формируются через [JsonResultMapper](../../CostVision.Web/Web/Mappers/JsonResultMapper.cs). Клиентский helper разворачивает единый контракт результата и показывает ожидаемые ошибки без перезагрузки страницы.

## ServiceResult и ошибки

[ServiceResult](../../CostVision.Application/Models/Responses/Results/ServiceResult.cs) и `ServiceResult<T>` различают успешный результат и ожидаемую ошибку. Типы: validation, unauthorized, forbidden, not found, conflict и external service. Mapper выбирает соответствующий HTTP status и JSON.

Неожиданное нарушение инварианта выбрасывается как exception и попадает в глобальный middleware. Оно не маскируется успешным результатом или пустым набором.

## Транзакционные границы

[IUnitOfWork.ExecuteInTransaction](../../CostVision.Application/Abstractions/DataBase/Repositories/IUnitOfWork.cs) применяется, когда сценарий меняет несколько записей: состав участников, перемещение/создание операций, импорт, массовая точная сверка и связи. Реализация начинает EF transaction, выполняет action и сохраняет изменения как одно действие.

Внешний HTTP-вызов refresh выполняется до локального `SaveChanges`; обновление магазина, товаров, позиций и чека сохраняется согласованно. Фоновые ошибки записывают состояние retry отдельным успешным локальным сохранением.

## Время и идентичность

- Аудит создания, обновления, входа и refresh хранится в UTC.
- Пользовательские даты покупок и операций сохраняются как получены и группируются по календарной дате.
- Фискальная идентичность включает реквизиты, дату, сумму и вид операции; уникальный индекс дополнительно включает создателя.
- Импортированная операция сравнивается по моменту, сумме, типу и исходному тексту выписки внутри счёта.
