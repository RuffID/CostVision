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

Domain хранит сущности и их инварианты без HTTP-сценариев. При этом проект ссылается на `libs/EFCoreLibrary.dll`: отсутствие ссылок на другие проекты не означает полной независимости от инфраструктурной библиотеки.

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
- контракты `IUnitOfWorkScope`, узких сценарных UoW, репозиториев, внешнего receipt provider, QR parser, PDF extractor и password hasher.

Регистрации перечислены в [ApplicationServiceCollectionExtensions](../../CostVision.Application/Extensions/ApplicationServiceCollectionExtensions.cs). Application зависит от EF Core: [AppDbContextBase](../../CostVision.Application/Abstractions/DataBase/AppDbContextBase.cs) наследует `DbContext`, а контракты репозиториев связаны с этим контекстом через `EFCoreLibrary`. SQL Server provider и конкретный `ApplicationContext` находятся в Infrastructure. В 08 пользователь явно сохранил эту EF/DLL-границу (вариант А), включая predicate/include/tracking и generic projection 07. [Решение и непринятые варианты](../Plans/project-refactoring-2026-10-13/08-boundary-decision.md) отличают её от полного удаления EF. [Поставка DLL](../../libs/README.md) описывает metadata, версии и неизвестный upstream.

MoneyMovementsPageUseCase и его интерфейс удалены: они механически проксировали 14 сценариев. MoneyMovementsModel получает эти конкретные зависимости напрямую; координаторы импорта/создания/сверки и владельцы транзакций остаются в Application. PreviewBankStatementImportRequest именуется по операции и проверяется самим preview-сценарием, HTTP multipart/handlers сохранены.

## Слой Infrastructure

Infrastructure реализует application-порты:

- [ApplicationContext](../../CostVision.Infrastructure/DataBase/ApplicationContext.cs) и entity configurations;
- [UnitOfWorkScope](../../CostVision.Infrastructure/DataBase/Repositories/UnitOfWorkScope.cs), сценарные adapters и тонкие типизированные репозитории;
- [ExternalReceiptProvider](../../CostVision.Infrastructure/Services/Receipts/ExternalReceiptProvider.cs), HTTP-запрос и преобразователи ответа;
- [QrParser](../../CostVision.Infrastructure/Services/Receipts/QrParser.cs);
- PDF text extractor и три bank statement parser в Application;
- password hashing;
- backup, migration check и ежедневный receipt refresh.

Все EF-репозитории работают в scoped `ApplicationContext`, поэтому один use case использует общий change tracker и одну транзакционную границу.

## Слой Web

PageModel получает текущего пользователя из request context, вызывает use case и возвращает HTML либо JSON. Проверки финансового доступа преимущественно находятся в Application. Аудит выявил пропуски в отдельных выборках и изменениях каталогов; скрытая кнопка или закрытая страница не заменяют проверку в сценарии.

AJAX-ответы формируются через [JsonResultMapper](../../CostVision.Web/Web/Mappers/JsonResultMapper.cs). Клиентский helper разворачивает единый контракт результата и показывает ожидаемые ошибки без перезагрузки страницы.

## ServiceResult и ошибки

[ServiceResult](../../CostVision.Application/Models/Responses/Results/ServiceResult.cs) и `ServiceResult<T>` различают успешный результат и ожидаемую ошибку. Типы: validation, unauthorized, forbidden, not found, conflict и external service. Mapper выбирает соответствующий HTTP status и JSON.

Неожиданное нарушение инварианта выбрасывается как exception и попадает в глобальный middleware. Оно не маскируется успешным результатом или пустым набором.

## Транзакционные границы

После 07A глобальный UoW с 11 репозиториями удалён. Read-only и helpers получают необходимые репозитории напрямую; запись использует 14 узких сценарных UoW либо один репозиторий и технический [IUnitOfWorkScope](../../CostVision.Application/Abstractions/DataBase/Repositories/IUnitOfWorkScope.cs). Наборы зависимостей и владельцы каждой транзакции указаны в [карте потребителей 07A](../Plans/project-refactoring-2026-10-13/07a-consumer-map.md).

Один scoped UnitOfWorkScope управляет транзакцией, сохранением и состоянием отказа общего ApplicationContext. ScenarioUnitOfWork только делегирует технические операции; отдельные adapters не создают контексты или флаги отказа. Разные DI scope независимы. Обычная перегрузка ExecuteInTransaction сохраняет после action; явная с IsolationLevel не сохраняет автоматически. Вложенная/конкурирующая транзакция отвергается до второго Begin. Отказ сохранения или транзакции блокирует обе перегрузки и Save через все adapters, включая перехваченный внутри action отказ; повтор требует нового scope.

Serializable сохраняет чтения/проверки и запись сценариев создания/переноса чеков, импорта, создания операций, привязки и сверки. Создание операции сохраняет её для ID, затем связь при наличии. Provider refresh вызывается до короткой Serializable-транзакции; граф чека и каталоги сохраняются одним явным Save. Retry-state записывается в новом scope после освобождения отказавшего. Изолированные DI/transaction-тесты не подтверждают SQL-атомарность или конкурентность.

## Время и идентичность

- Аудит создания, обновления, входа и refresh хранится в UTC.
- Пользовательские даты покупок и операций сохраняются как получены и группируются по календарной дате.
- Фискальная идентичность включает реквизиты, дату, сумму и вид операции; уникальный индекс дополнительно включает создателя.
- Импортированная операция сравнивается по моменту, сумме, типу и исходному тексту выписки внутри счёта.
