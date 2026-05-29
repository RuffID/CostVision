# План unit-тестирования CostVision

Используем xUnit.net + FluentAssertions + Moq или NSubstitute.

Причина: проект уже хорошо разделён на use case, интерфейсы репозиториев и `ServiceResult`, поэтому тесты будут естественно строиться вокруг входного request, моков `IUnitOfWork`/репозиториев и проверки результата. xUnit даст меньше шаблонного кода, а FluentAssertions сделает проверки читаемыми.

Предлагаемая структура проектов:

- `CostVision.Application.UnitTests` — основной проект unit-тестов бизнес-логики.
- `CostVision.Infrastructure.UnitTests` — тесты чистых инфраструктурных сервисов, парсеров, мапперов и helper-классов без настоящей БД/сети.
- `CostVision.Web.UnitTests` — точечные тесты PageModel только там, где есть ветвление результата; основной web-поток лучше закрывать интеграционными тестами отдельно.

## Границы unit-тестов

Unit-тесты не должны ходить в реальную БД, файловую систему, сеть и внешний API проверки чеков. Всё это мокается. Для EF-конфигураций, middleware pipeline, Razor Pages routing и полного HTTP-потока нужны отдельные integration tests, не unit tests.

Если пункт плана не выполнен, пропущен или признан неприменимым, рядом с ним обязательно указывать причину: отсутствие текущей логики, перенос в integration/component/e2e tests, дублирование другим тестом или другую конкретную причину.

## Приоритет 1: бизнес-критичные use case

### Авторизация и пользователи



- `CostVision.Application/UseCases/Authorize/Authentication/AuthenticateUserUseCase.cs`

  - [x] успешный вход;

  - [x] пользователь не найден;

  - [x] пользователь заблокирован;

  - [x] неверный пароль;

  - [x] обновление `LastLoginAtUtc`/сохранение изменений.



- `CostVision.Application/UseCases/Authorize/Users/CreateUserUseCase.cs`

  - [x] валидация обязательных полей;

  - [x] конфликт логина/имени, если проверяется;

  - [x] хеширование пароля;

  - [x] назначение роли;

  - [x] возврат `ServiceResult` при ошибках репозитория/валидации.



- `CostVision.Application/UseCases/Authorize/Users/UpdateUserUseCase.cs`

  - [x] пользователь не найден;

  - [x] изменение данных без пароля;

  - [x] изменение данных с новым паролем;

  - [x] конфликт уникального логина;

  - [x] сохранение роли.



- `CostVision.Application/UseCases/Authorize/Users/ToggleUserActiveUseCase.cs`

  - [x] запрет/разрешение активации;

  - [x] пользователь не найден;

  - [x] корректная смена флага активности.



- `CostVision.Application/UseCases/Authorize/Users/MarkUserActivityUseCase.cs`

  - [x] обновление активности существующего пользователя;

  - [x] отсутствие пользователя.



- `CostVision.Application/UseCases/Authorize/Users/Helpers/UserUpsertRequestValidator.cs`

  - [x] пустые поля;

  - [x] некорректный пароль;

  - [x] граничные длины;

  - [x] валидный request.



### Счета и доступы



- `CostVision.Application/UseCases/Receipts/Accounts/CreateAccountUseCase.cs`

  - [x] создание счёта владельцем;

  - [x] нормализация цвета;

  - [x] дефолтные участники/права;

  - [x] ошибки валидации.



- `CostVision.Application/UseCases/Receipts/Accounts/UpdateAccountUseCase.cs`

  - [x] счёт не найден;

  - [x] нет доступа — текущая реализация маскирует отсутствие доступа как `404`;

  - [x] обновление имени/цвета;

  - [x] проверка сохранения.



- `CostVision.Application/UseCases/Receipts/Accounts/UpdateAccountMembersUseCase.cs`

  - [x] добавление участника;

  - [x] удаление участника;

  - [x] изменение прав;

  - [x] запрет удаления владельца/самого себя, если это предусмотрено логикой;

  - [x] пользователь/счёт не найден.



- `CostVision.Application/UseCases/Receipts/Accounts/GetUserAccountsUseCase.cs`

  - [x] пользователь видит только доступные счета;

  - [x] корректная проекция DTO;

  - [x] сортировка, если есть — не применяется к текущей реализации: явной сортировки сейчас нет.



- `CostVision.Application/UseCases/Receipts/Accounts/GetAccountShareUsersUseCase.cs`

  - [x] список доступных пользователей для шаринга;

  - [x] исключение уже добавленных участников;

  - [x] проверка прав владельца.



- `CostVision.Application/UseCases/Receipts/Accounts/ValidateReceiptCreationAccessUseCase.cs`

  - [x] можно создавать чек в счёте;

  - [x] нельзя создавать без доступа;

  - [x] счёт не найден.



- `CostVision.Application/UseCases/Receipts/Accounts/MoveReceiptToAccountUseCase.cs`

  - [x] перенос чека между счетами;

  - [x] нет доступа к исходному/целевому счёту;

  - [x] чек уже привязан;

  - [x] сохранение операции.



- `CostVision.Application/UseCases/Receipts/Accounts/RemoveReceiptFromAccountUseCase.cs`

  - [x] удаление связи чека со счётом;

  - [x] запрет удаления последней/недоступной связи, если это предусмотрено логикой;

  - [x] чек/счёт не найден.



- `CostVision.Application/UseCases/Receipts/Accounts/Helpers/AccountColorHexNormalizer.cs`

  - [x] нормальные HEX-значения;

  - [x] значения без `#`;

  - [x] пустое/некорректное значение;

  - [x] регистр символов.



- `CostVision.Application/UseCases/Receipts/Accounts/Helpers/AccountReceiptAccessValidator.cs`

  - [x] владелец имеет доступ;

  - [x] участник имеет доступ;

  - [x] чужой пользователь не имеет доступа;

  - [x] отсутствующие связи.



### Чеки



- `CostVision.Application/UseCases/Receipts/Receipts/SaveManualReceiptUseCase.cs`

  - [x] создание ручного чека;

  - пустые позиции — не применяется к текущей реализации: ручной request не содержит позиции;

  - некорректные суммы/количество — не применяется к текущей реализации: валидации сумм и количества сейчас нет;

  - [x] привязка к счёту;

  - [x] вызов refresh workflow при необходимости;

  - [x] сохранение.



- `CostVision.Application/UseCases/Receipts/Receipts/SaveReceiptsScannedUseCase.cs`

  - [x] валидный QR;

  - [x] невалидный QR;

  - [x] дубликат чека;

  - [x] чек найден во внешнем API — покрыто через refresh workflow;

  - [x] внешний API вернул ошибку — покрыто через refresh workflow;

  - [x] привязка к счёту.



- `CostVision.Application/UseCases/Receipts/Receipts/GetReceiptListUseCase.cs`

  - [x] фильтрация по доступным счётам;

  - [x] корректная группировка/сортировка — не применяется к текущей реализации: use case возвращает плоский список без сортировки;

  - [x] пустой результат;

  - DTO без лишних данных — не применяется к текущей реализации: use case возвращает доменную модель.



- `CostVision.Application/UseCases/Receipts/Receipts/GetReceiptWithItemsUseCase.cs`

  - [x] чек найден;

  - [x] чек не найден;

  - [x] нет доступа;

  - [x] позиции и счета загружены корректно.



- `CostVision.Application/UseCases/Receipts/Receipts/DeleteReceiptUseCase.cs`

  - [x] удаление доступного чека;

  - [x] чек не найден;

  - [x] нет доступа;

  - связанные сущности удаляются/сохраняются согласно текущей логике — лучше проверять integration-тестом каскадов EF.



- `CostVision.Application/UseCases/Receipts/Receipts/RefreshReceiptFromApiUseCase.cs`

  - [x] успешное обновление;

  - [x] чек не найден;

  - [x] нет доступа;

  - [x] внешний провайдер вернул ошибку;

  - [x] сохранение обновлённых позиций.



- `CostVision.Application/UseCases/Receipts/Receipts/RefreshReceiptsWithoutItemsUseCase.cs`

  - выбираются только чеки без позиций;

  - частичные ошибки не ломают весь процесс, если это заложено логикой;

  - логирование ошибок.



- `CostVision.Application/UseCases/Receipts/Receipts/Refresh/ReceiptRefreshWorkflow.cs`

  - [x] внешний чек найден и мапится в доменную модель;

  - [x] внешний чек не найден;

  - [x] позиции пересоздаются/обновляются правильно;

  - [x] продукты связываются или создаются корректно;

  - [x] ошибки провайдера возвращаются как `ServiceResult`.



### Товары



- `CostVision.Application/UseCases/Receipts/Products/GetProductListUseCase.cs`

  - [x] фильтр по названию;

  - [x] пагинация/лимит, если есть;

  - [x] нормализация названий;

  - [x] сортировка;

  - [x] пустой результат.



- `CostVision.Application/UseCases/Receipts/Products/UpdateProductAdaptiveNameUseCase.cs`

  - [x] товар найден;

  - [x] товар не найден;

  - [x] пустое адаптивное имя;

  - [x] нормализация имени;

  - [x] сохранение изменений.



## Приоритет 2: денежные движения и импорт выписок

- `CostVision.Application/UseCases/MoneyMovements/CreateMoneyMovementUseCase.cs`
  - создание движения;
  - недоступный счёт;
  - некорректная сумма/дата;
  - автоматическая связь с чеком, если используется.

- `CostVision.Application/UseCases/MoneyMovements/GetMoneyMovementListUseCase.cs`
  - фильтрация по пользователю/счёту;
  - сортировка;
  - корректная проекция DTO;
  - пустой список.

- `CostVision.Application/UseCases/MoneyMovements/MoveMoneyMovementToAccountUseCase.cs`
  - перенос между доступными счетами;
  - нет доступа;
  - движение не найдено;
  - транзакционность.

- `CostVision.Application/UseCases/MoneyMovements/DeleteMoneyMovementUseCase.cs`
  - удаление движения;
  - движение не найдено;
  - нет доступа;
  - удаление связей с чеками.

- `CostVision.Application/UseCases/MoneyMovements/UpdateMoneyMovementCommentUseCase.cs`
  - изменение комментария;
  - пустой комментарий;
  - движение не найдено;
  - нет доступа.

- `CostVision.Application/UseCases/MoneyMovements/AutoLinkExactMoneyMovementReceiptsUseCase.cs`
  - точное совпадение суммы/даты;
  - несколько кандидатов;
  - уже связанное движение;
  - отсутствие кандидатов.

- `CostVision.Application/UseCases/MoneyMovements/LinkMoneyMovementReceiptUseCase.cs`
  - ручная связь движения и чека;
  - нет доступа к движению/чеку;
  - дубль связи;
  - разные счета.

- `CostVision.Application/UseCases/MoneyMovements/UnlinkMoneyMovementReceiptUseCase.cs`
  - удаление связи;
  - связь не найдена;
  - нет доступа.

- `CostVision.Application/UseCases/MoneyMovements/GetMoneyMovementReceiptCandidatesUseCase.cs`
  - кандидаты по сумме/дате;
  - исключение уже связанных чеков;
  - проверка доступа.

- `CostVision.Application/UseCases/MoneyMovements/GetReceiptMoneyMovementCandidatesUseCase.cs`
  - кандидаты для чека;
  - исключение уже связанных движений;
  - проверка доступа.

- `CostVision.Application/UseCases/MoneyMovements/GetLinkedMoneyMovementReceiptsUseCase.cs`
  - список связанных чеков;
  - нет доступа;
  - пустой список.

- `CostVision.Application/UseCases/MoneyMovements/GetLinkedReceiptMoneyMovementsUseCase.cs`
  - список связанных движений;
  - нет доступа;
  - пустой список.

- `CostVision.Application/UseCases/MoneyMovements/GetMoneyMovementAccountsUseCase.cs`
  - список счетов пользователя для движений;
  - фильтр доступности.

- `CostVision.Application/UseCases/MoneyMovements/PreviewBankStatementImportUseCase.cs`
  - определение банка;
  - парсинг строк;
  - ошибки строк не валят весь preview;
  - дубликаты;
  - сопоставление со счетом.

- `CostVision.Application/UseCases/MoneyMovements/ImportMoneyMovementsUseCase.cs`
  - импорт валидных строк;
  - пропуск невалидных строк;
  - дубликаты;
  - транзакционность сохранения;
  - результат с количеством созданных/ошибочных строк.

- `CostVision.Application/UseCases/MoneyMovements/GetBankStatementImportBanksUseCase.cs`
  - возвращаются зарегистрированные банки;
  - пустой registry.

- `CostVision.Application/UseCases/MoneyMovements/MoneyMovementAccountAccessValidator.cs`
  - владелец/участник имеет доступ;
  - чужой пользователь не имеет доступа;
  - счёт не найден.

## Приоритет 3: парсеры и чистые helper-классы

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsers/TBank/TBankPdfStatementParser.cs`
  - валидные строки Т-Банка;
  - строки с переносами;
  - отрицательные/положительные суммы;
  - мусорные строки;
  - даты в ожидаемом формате.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsers/Sber/SberBankPdfStatementParser.cs`
  - валидные строки Сбера;
  - комиссии/переводы/покупки;
  - мусорные строки;
  - даты и суммы.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsers/Alfa/AlfaBankPdfStatementParser.cs`
  - валидные строки Альфа-Банка;
  - разные типы операций;
  - мусорные строки;
  - даты и суммы.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsing/BankStatementParserRegistry.cs`
  - выбор парсера по банку;
  - неизвестный банк;
  - список поддерживаемых банков.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsing/BankStatementTextNormalizer.cs`
  - нормализация пробелов;
  - переносы строк;
  - неразрывные пробелы;
  - пустой текст.

- `CostVision.Application/UseCases/MoneyMovements/Helpers/MoneyMovementMapper.cs`
  - доменная модель -> DTO;
  - nullable-поля;
  - связанные чеки.

- `CostVision.Application/UseCases/MoneyMovements/Helpers/MoneyMovementReceiptMapper.cs`
  - связь движение-чек -> DTO;
  - отсутствующие вложенные данные.

- `CostVision.Application/UseCases/MoneyMovements/Helpers/ReceiptMoneyMovementMapper.cs`
  - чек -> связанные движения DTO;
  - пустые связи.

- `CostVision.Application/Models/Dtos/Mappers/ReceiptMapper.cs`
  - чек с позициями;
  - чек без позиций;
  - связанные счета;
  - nullable-поля из ФНС/API.

- `CostVision.Application/Models/Dtos/Mappers/ReceiptGroupMapper.cs`
  - группировка чеков;
  - пустой список;
  - сортировка групп.

- `CostVision.Application/Models/Dtos/Mappers/JsonResultMapper.cs`
  - успешный `ServiceResult`;
  - ошибка валидации;
  - not found/forbidden/bad request;
  - generic/non-generic result.

- `CostVision.Application/Models/Responses/Results/ServiceResult.cs`
  - success/failure factories;
  - корректные статусы;
  - payload в generic-версии.

## Приоритет 4: инфраструктура без внешних зависимостей

- `CostVision.Infrastructure/Services/Receipts/QrParser.cs`
  - QR-строка ФНС с `t`, `s`, `fn`, `i`, `fp`;
  - параметры в другом порядке;
  - URL-encoded значения;
  - отсутствующий обязательный параметр;
  - мусорная строка.

- `CostVision.Infrastructure/Services/Receipts/ExternalReceiptProvider.cs`
  - успешный ответ API;
  - ошибка API;
  - пустой ответ;
  - ошибка десериализации;
  - request строится из QR/manual данных корректно. Сеть мокать через `IReceiptRequest`.

- `CostVision.Infrastructure/Services/Converters/ProverkachekaReceiptMapper.cs`
  - маппинг данных API в доменную модель;
  - позиции;
  - налоги/скидки/количество;
  - nullable-поля.

- `CostVision.Infrastructure/Services/Converters/ProverkachekaDataConverter.cs`
  - разные формы поля `data` в ответе API;
  - null/пустой объект;
  - некорректный JSON.

- `CostVision.Infrastructure/Services/Helpers/Hasher.cs`
  - одинаковый пароль даёт проверяемый хеш;
  - неверный пароль не проходит;
  - соль/разные хеши для одного пароля, если реализовано;
  - пустые значения.

- `CostVision.Infrastructure/Services/Helpers/NameNormalizedHelper.cs`
  - регистр;
  - лишние пробелы;
  - русские символы;
  - пустая строка.

- `CostVision.Infrastructure/Services/Helpers/ReceiptAccessVerificationService.cs`
  - доступ владельца;
  - доступ участника;
  - нет доступа;
  - чек/счёт не найден.

- `CostVision.Infrastructure/Services/MoneyMovements/BankStatementPdfTextExtractor.cs`
  - это ближе к integration/component test, потому что читает PDF; unit-тестировать только обработку ошибок вокруг extractor, если будет выделена чистая логика.

## Приоритет 5: PageModel smoke unit-тесты

PageModel не должен содержать бизнес-логику, поэтому здесь нужны только точечные тесты: вызван правильный use case, request собирается корректно, `ServiceResult` мапится в JSON/redirect/page result.

- `CostVision/Pages/Login.cshtml.cs`
  - успешный логин;
  - неверный логин/пароль;
  - установка cookie/claims лучше вынести в integration test, если мокать тяжело.

- `CostVision/Pages/AddReceipt.cshtml.cs`
  - ручное добавление вызывает `SaveManualReceiptUseCase`;
  - QR-добавление вызывает `SaveReceiptsScannedUseCase`;
  - ошибки возвращаются JSON через mapper.

- `CostVision/Pages/Receipts.cshtml.cs`
  - список чеков;
  - открытие чека;
  - удаление;
  - перенос между счетами;
  - refresh.

- `CostVision/Pages/MoneyMovements.cshtml.cs`
  - список движений;
  - создание/удаление/перенос;
  - связь/разрыв связи с чеком;
  - preview/import выписки.

- `CostVision/Pages/Products.cshtml.cs`
  - список продуктов;
  - обновление адаптивного имени.

- `CostVision/Pages/Settings/Users.cshtml.cs`
  - список пользователей;
  - создание/редактирование;
  - активация/деактивация.

- `CostVision/Pages/Settings/UserSettings.cshtml.cs`
  - загрузка текущего пользователя;
  - обновление настроек.

- `CostVision/Pages/UserActivity.cshtml.cs`
  - mark activity вызывается для текущего пользователя;
  - отсутствие текущего пользователя.

## Что не начинать с unit-тестов

- EF Core repository implementations: `CostVision.Infrastructure/DataBase/Repositories/*`. Их лучше проверять integration-тестами на тестовой БД или SQLite/in-memory provider, потому что ценность unit-тестов над `DbSet`-моками низкая.
- EF migrations и model configuration: integration/schema tests, не unit tests.
- `BackupService`, `DataBaseCheckUpService`, `ReceiptRefreshBackgroundService`, `ExceptionHandlingMiddleware`: для них лучше component/integration tests, потому что важны файловая система, hosted service lifecycle и HTTP pipeline.
- Razor `.cshtml` и TypeScript UI: отдельно frontend/component/e2e тесты, не C# unit tests.

## Минимальный стартовый набор

1. Создать `CostVision.Application.UnitTests` на xUnit.
2. Добавить пакеты: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FluentAssertions`, `Moq` или `NSubstitute`.
3. Первыми покрыть:
   - `AuthenticateUserUseCase`;
   - `SaveManualReceiptUseCase`;
   - `SaveReceiptsScannedUseCase`;
   - `ReceiptRefreshWorkflow`;
   - `CreateMoneyMovementUseCase`;
   - `LinkMoneyMovementReceiptUseCase`;
   - `PreviewBankStatementImportUseCase`;
   - `QrParser`;
   - парсеры выписок Т-Банк/Сбер/Альфа.
4. После этого добавить тесты для Account use case и Product use case.
5. Затем точечно покрыть PageModel, где есть ветвления и JSON-маппинг.

## Критерий готовности первого этапа

Первый этап можно считать готовым, когда есть тесты на успешный сценарий, ошибку доступа, `not found` и ошибку валидации для каждого бизнес-критичного use case из приоритета 1, а также тесты парсинга QR и банковских выписок из приоритета 3.
