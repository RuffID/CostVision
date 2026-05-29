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
  - [x] привязка к счёту;
  - [x] вызов refresh workflow при необходимости;
  - [x] сохранение.

- `CostVision.Application/UseCases/Receipts/Receipts/SaveReceiptsScannedUseCase.cs`
  - [x] валидный QR;
  - [x] невалидный QR;
  - [x] дубликат чека;
  - [x] передача созданных чеков в refresh workflow;
  - [x] привязка к счёту.

- `CostVision.Application/UseCases/Receipts/Receipts/GetReceiptListUseCase.cs`
  - [x] фильтрация по доступным счётам;
  - [x] корректная группировка/сортировка;
  - [x] пустой результат;
  - [x] DTO без лишних данных.

- `CostVision.Application/UseCases/Receipts/Receipts/GetReceiptWithItemsUseCase.cs`
  - [x] чек найден;
  - [x] чек не найден;
  - [x] нет доступа;
  - [x] позиции и счета загружены корректно.

- `CostVision.Application/UseCases/Receipts/Receipts/DeleteReceiptUseCase.cs`
  - [x] удаление доступного чека;
  - [x] чек не найден;
  - [x] нет доступа.

- `CostVision.Application/UseCases/Receipts/Receipts/RefreshReceiptFromApiUseCase.cs`
  - [x] успешное обновление;
  - [x] чек не найден;
  - [x] нет доступа;
  - [x] внешний провайдер вернул ошибку;
  - [x] сохранение обновлённых позиций.

- `CostVision.Application/UseCases/Receipts/Receipts/RefreshReceiptsWithoutItemsUseCase.cs`
  - [x] выбираются только чеки без позиций;
  - [x] частичные ошибки не ломают весь процесс, если это заложено логикой;
  - [x] логирование ошибок.

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
  - [x] создание движения;
  - [x] недоступный счёт;
  - [x] некорректная сумма;
  - [x] некорректная дата;
  - [x] автоматическая связь с чеком, если используется.

- `CostVision.Application/UseCases/MoneyMovements/GetMoneyMovementListUseCase.cs`
  - [x] фильтрация по пользователю/счёту;
  - [x] сортировка;
  - [x] корректная проекция DTO;
  - [x] пустой список.

- `CostVision.Application/UseCases/MoneyMovements/MoveMoneyMovementToAccountUseCase.cs`
  - [x] перенос между доступными счетами;
  - [x] нет доступа;
  - [x] движение не найдено.

- `CostVision.Application/UseCases/MoneyMovements/DeleteMoneyMovementUseCase.cs`
  - [x] удаление движения;
  - [x] движение не найдено;
  - [x] нет доступа;
  - [x] удаление связей с чеками.

- `CostVision.Application/UseCases/MoneyMovements/UpdateMoneyMovementCommentUseCase.cs`
  - [x] изменение комментария;
  - [x] пустой комментарий;
  - [x] движение не найдено;
  - [x] нет доступа.

- `CostVision.Application/UseCases/MoneyMovements/AutoLinkExactMoneyMovementReceiptsUseCase.cs`
  - [x] точное совпадение суммы/даты;
  - [x] несколько кандидатов;
  - [x] уже связанное движение;
  - [x] отсутствие кандидатов.

- `CostVision.Application/UseCases/MoneyMovements/LinkMoneyMovementReceiptUseCase.cs`
  - [x] ручная связь движения и чека;
  - [x] нет доступа к движению/чеку;
  - [x] дубль связи;
  - [x] разные счета.

- `CostVision.Application/UseCases/MoneyMovements/UnlinkMoneyMovementReceiptUseCase.cs`
  - [x] удаление связи;
  - [x] связь не найдена;
  - [x] нет доступа.

- `CostVision.Application/UseCases/MoneyMovements/GetMoneyMovementReceiptCandidatesUseCase.cs`
  - [x] кандидаты по сумме/дате;
  - [x] исключение уже связанных чеков;
  - [x] проверка доступа.

- `CostVision.Application/UseCases/MoneyMovements/GetReceiptMoneyMovementCandidatesUseCase.cs`
  - [x] кандидаты для чека;
  - [x] исключение уже связанных движений;
  - [x] проверка доступа.

- `CostVision.Application/UseCases/MoneyMovements/GetLinkedMoneyMovementReceiptsUseCase.cs`
  - [x] список связанных чеков;
  - [x] нет доступа;
  - [x] пустой список.

- `CostVision.Application/UseCases/MoneyMovements/GetLinkedReceiptMoneyMovementsUseCase.cs`
  - [x] список связанных движений;
  - [x] нет доступа;
  - [x] пустой список.

- `CostVision.Application/UseCases/MoneyMovements/GetMoneyMovementAccountsUseCase.cs`
  - [x] список счетов пользователя для движений;
  - [x] фильтр доступности.

- `CostVision.Application/UseCases/MoneyMovements/PreviewBankStatementImportUseCase.cs`
  - [x] определение банка;
  - [x] парсинг строк;
  - [x] ошибки строк не валят весь preview;
  - [x] дубликаты;
  - [x] сопоставление со счетом.

- `CostVision.Application/UseCases/MoneyMovements/ImportMoneyMovementsUseCase.cs`
  - [x] импорт валидных строк;
  - [x] пропуск невалидных строк;
  - [x] дубликаты;
  - [x] транзакционность сохранения;
  - [x] результат с количеством созданных/ошибочных строк.

- `CostVision.Application/UseCases/MoneyMovements/GetBankStatementImportBanksUseCase.cs`
  - [x] возвращаются зарегистрированные банки;
  - [x] пустой registry.

- `CostVision.Application/UseCases/MoneyMovements/MoneyMovementAccountAccessValidator.cs`
  - [x] владелец/участник имеет доступ;
  - [x] чужой пользователь не имеет доступа;
  - [x] счёт не найден.

## Приоритет 3: парсеры и чистые helper-классы

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsers/TBank/TBankPdfStatementParser.cs`
  - [x] валидные строки Т-Банка;
  - [x] строки с переносами;
  - [x] отрицательные/положительные суммы;
  - [x] мусорные строки;
  - [x] даты в ожидаемом формате.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsers/Sber/SberBankPdfStatementParser.cs`
  - [ ] валидные строки Сбера — не выполнено: текущий парсер не настроен (`IsConfigured == false`) и возвращает пустой preview без разбора строк;
  - [ ] комиссии/переводы/покупки — не выполнено: текущий парсер не содержит логики классификации операций;
  - [ ] мусорные строки — не выполнено: текущий парсер не обрабатывает содержимое строк;
  - [ ] даты и суммы — не выполнено: текущий парсер не извлекает даты и суммы.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsers/Alfa/AlfaBankPdfStatementParser.cs`
  - [ ] валидные строки Альфа-Банка — не выполнено: текущий парсер не настроен (`IsConfigured == false`) и возвращает пустой preview без разбора строк;
  - [ ] разные типы операций — не выполнено: текущий парсер не содержит логики классификации операций;
  - [ ] мусорные строки — не выполнено: текущий парсер не обрабатывает содержимое строк;
  - [ ] даты и суммы — не выполнено: текущий парсер не извлекает даты и суммы.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsing/BankStatementParserRegistry.cs`
  - [x] выбор парсера по банку;
  - [x] неизвестный банк;
  - [x] список поддерживаемых банков.

- `CostVision.Application/UseCases/MoneyMovements/BankStatementImports/Parsing/BankStatementTextNormalizer.cs`
  - [x] нормализация пробелов;
  - [x] переносы строк;
  - [x] неразрывные пробелы;
  - [x] пустой текст.

- `CostVision.Application/UseCases/MoneyMovements/Helpers/MoneyMovementMapper.cs`
  - [x] доменная модель -> DTO;
  - [x] nullable-поля;
  - [x] связанные чеки.

- `CostVision.Application/UseCases/MoneyMovements/Helpers/MoneyMovementReceiptMapper.cs`
  - [x] связь движение-чек -> DTO;
  - [x] отсутствующие вложенные данные.

- `CostVision.Application/UseCases/MoneyMovements/Helpers/ReceiptMoneyMovementMapper.cs`
  - [x] чек -> связанные движения DTO;
  - [x] пустые связи.

- `CostVision.Application/Models/Dtos/Mappers/ReceiptMapper.cs`
  - [x] чек с позициями;
  - [x] чек без позиций;
  - [x] связанные счета;
  - [x] nullable-поля из ФНС/API.

- `CostVision.Application/Models/Dtos/Mappers/ReceiptGroupMapper.cs`
  - [x] группировка чеков;
  - [x] пустой список;
  - [x] сортировка групп.

- `CostVision.Application/Models/Dtos/Mappers/JsonResultMapper.cs`
  - [x] успешный `ServiceResult`;
  - [x] ошибка валидации;
  - [x] not found/forbidden/bad request;
  - [x] generic/non-generic result.

- `CostVision.Application/Models/Responses/Results/ServiceResult.cs`
  - [x] success/failure factories;
  - [x] корректные статусы;
  - [x] payload в generic-версии.

## Приоритет 4: инфраструктура без внешних зависимостей

- `CostVision.Infrastructure/Services/Receipts/QrParser.cs`
  - [x] QR-строка ФНС с `t`, `s`, `fn`, `i`, `fp`;
  - [x] параметры в другом порядке;
  - [x] URL-encoded значения;
  - [x] отсутствующий обязательный параметр;
  - [x] мусорная строка.

- `CostVision.Infrastructure/Services/Receipts/ExternalReceiptProvider.cs`
  - [x] успешный ответ API;
  - [x] ошибка API;
  - [x] пустой ответ;
  - [x] ошибка десериализации;
  - [x] request строится из QR/manual данных корректно. Сеть мокать через `IReceiptRequest`.

- `CostVision.Infrastructure/Services/Converters/ProverkachekaReceiptMapper.cs`
  - [x] маппинг данных API в доменную модель;
  - [x] позиции;
  - [x] налоги/скидки/количество;
  - [x] nullable-поля.

- `CostVision.Infrastructure/Services/Converters/ProverkachekaDataConverter.cs`
  - [x] разные формы поля `data` в ответе API;
  - [x] null/пустой объект;
  - [x] некорректный JSON.

- `CostVision.Infrastructure/Services/Helpers/Hasher.cs`
  - [x] одинаковый пароль даёт проверяемый хеш;
  - [x] неверный пароль не проходит;
  - [x] соль/разные хеши для одного пароля, если реализовано;
  - [x] пустые значения.

- `CostVision.Infrastructure/Services/Helpers/NameNormalizedHelper.cs`
  - [x] регистр;
  - [x] лишние пробелы;
  - [x] русские символы;
  - [x] пустая строка.

- `CostVision.Infrastructure/Services/Helpers/ReceiptAccessVerificationService.cs`
  - [x] доступ владельца;
  - [x] доступ участника;
  - [x] нет доступа;
  - [x] чек/счёт не найден.

## Приоритет 5: PageModel smoke unit-тесты

PageModel не должен содержать бизнес-логику, поэтому здесь нужны только точечные тесты: вызван правильный use case, request собирается корректно, `ServiceResult` мапится в JSON/redirect/page result.

- `CostVision/Pages/Login.cshtml.cs`
  - успешный логин;
  - неверный логин/пароль.

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
