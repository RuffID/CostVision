# Этап 02. Каталоги, пользователи и архивные счета

Статус: **Реализован, проверка не завершена**.
Дата начала: 13.10.2026.
Дата изолированной проверки: 13.10.2026.
Приоритет: **П1/П2**.
Зависимости: 01.

## Исходные проблемы аудита

1. **Критично: изменение чужого каталога по идентификатору.** [UpdateProductAdaptiveNameUseCase](../../../CostVision.Application/UseCases/Receipts/Products/UpdateProductAdaptiveNameUseCase.cs) и [UpdateStoreAdaptiveNameUseCase](../../../CostVision.Application/UseCases/Receipts/Stores/UpdateStoreAdaptiveNameUseCase.cs) не получают пользователя. Их PageModel передают только request. Любой аутентифицированный пользователь может изменить существующий объект без доказательства доступа к его покупкам.
2. `AdaptiveName` хранится в общей сущности товара/магазина, а интерфейс говорит «Ваше наименование». Изменение отражается у всех пользователей. Уникальность названия товара также глобальная. Это несогласованность области настройки; персонализация требует отдельного продуктового решения и схемы хранения.
3. [UserUpsertRequestValidator](../../../CostVision.Application/UseCases/Authorize/Users/Helpers/UserUpsertRequestValidator.cs) проверяет наличие непустых role ID, но `CreateUserUseCase`/`UpdateUserUseCase` не проверяют существование ролей перед записью. Некорректный ID доходит до внешнего ключа вместо ожидаемой ошибки валидации.
4. Создание и смена пароля хешируют `Password.Trim()`, а [AuthenticateUserUseCase](../../../CostVision.Application/UseCases/Authorize/Authentication/AuthenticateUserUseCase.cs) проверяет исходный пароль. Нормализация логина при сохранении и входе тоже различается. Ввод с пробелами может успешно сохраняться и затем не проходить вход в том же виде.
5. [ValidateReceiptCreationAccessUseCase](../../../CostVision.Application/UseCases/Receipts/Accounts/ValidateReceiptCreationAccessUseCase.cs) и `MoneyMovementAccountAccessValidator` проверяют membership/роль, но не архив. Архив исключён из выбора UI, однако прямой запрос создания или импорта всё ещё может пройти.
6. Общие методы чтения счетов дублируются; `GetMoneyMovementAccountsUseCase` подставляет Owner при отсутствии membership, а `GetUserAccountsUseCase` обращается к словарю ролей без проверки несогласованного состояния.

## План работ

- Сначала добавить пользовательский контекст и проверку доступности изменяемого каталога. Это исправление не требует автоматически создавать персональные названия.
- Сохранить общую модель AdaptiveName и явно описать совместное название. Персональные названия, новая схема и перенос значений исключены из этого этапа по решению пользователя.
- Проверять существование назначаемых ролей. Объединить правила нормализации профиля и пароля, сохраняя совместимость с уже созданными хешами.
- Запретить новые записи в архивный счёт на сервере. Возможность править исторические записи решать отдельно, не расширяя автоматически запрет.
- Согласовать чтение счетов и fail-fast при нарушении инварианта owner membership.

## Проверки и приёмка

- [x] Изолированные проверки реальных предикатов: недоступный товар/магазин нельзя изменить или очистить, отказ не меняет имя и не сохраняется; Admin не обходит доступ.
- [x] Общая область AdaptiveName согласована и отражена в документации, Razor-формах, заголовках и подсказках TypeScript; проверены исходники и сборка.
- [x] Неизвестный/пустой ID роли даёт Validation; профиль, hash и набор ролей не меняются частично.
- [x] Изолированно проверен вход с действующим форматом PBKDF2, создание/смена с настоящим Hasher, пробелы по краям, запрет внутренних пробелов нового пароля и пустое обновление без смены hash.
- [x] Прямые вызовы настоящих сценариев ручного/QR-чека, операции и импорта отклоняют архив; ReplaceDuplicate также отклоняется до транзакции и изменения.
- [x] Оба списка счетов используют область доступа этапа 01 и явную проверку owner membership. Отзыв участия скрывает счёт; история архива читается при явном запросе, исторический комментарий остаётся редактируемым.
- [ ] SQL Server: перевод предикатов каталога, ролей, логина и счетов; материализация Account/Members; сохранение/отказ и целостность реальных связей.
- [ ] Браузер: заголовки/подсказки общего названия, изменение/очистка и отображение результата у двух пользователей, вход с пробелами и ошибки прямых запросов.

SQL- и браузерные критерии обязательны для окончательной приёмки. Изолированные тесты, compile-check и статическая проверка разметки их не заменяют. SQL-приёмка этапа 01 остаётся открытой независимо от реализации 02.

## Журнал выполнения

13.10.2026: пользователь разрешил изолированную реализацию этапа 02 при открытой SQL-приёмке этапа 01. Общая модель AdaptiveName сохраняется; персонализация, новая схема и перенос значений не входят в этап. Этапы 03–15 не начаты.

Изменения:

- UpdateProduct/StoreAdaptiveName получают CurrentUser.Id из PageModel. Базовый запрос Receipt проверяет конкретный ProductId/StoreId и WithReceiptReadAccess этапа 01 перед мутацией. Глобальная уникальность имени товара сохранена.
- UI вместо «Ваше наименование» обозначает общее имя и влияние на других пользователей. Исправлены поиск, форма, заголовок и подсказка; generated JavaScript обновлён только штатным npm run build:ts.
- CreateUser/UpdateUser до Hash/Create/TryUpdate сверяют весь набор назначаемых ролей через базовый Role predicate. Guid.Empty отклоняется, повтор известного ID схлопывается. Прежние успешные тесты задают существующие роли явно.
- Сохранена прежняя нормализация нового пароля через Trim; вход применяет тот же Trim ровно перед одним Verify. Логин при сохранении и входе обрезается по краям, поиск не учитывает регистр. Действующие hash не перезаписываются; пустое обновление сохраняет прежний пароль.
- ValidateReceiptCreationAccess загружает Account и отклоняет архив. Создание операции и сохранение импорта используют requireActive=true общего валидатора до транзакции. Предпросмотр и исторические изменения не получают этот запрет автоматически.
- Оба списка счетов выбирают доступные Account с Members по правилам этапа 01. Общий GetRequiredAccountRole отклоняет отсутствие, неверную роль, дублирование owner membership и стороннего Owner через InvalidOperationException. Запасного Owner и раннего пустого списка владельца нет.
- Добавлены 48 сценариев Stage02RegressionTests, 3 проверки AuthenticationPasswordCompatibilityTests с настоящим Hasher и проверка StoresModel. Актуализированы инструкции владельцев, бизнес-описание, техническое руководство и навигация. Domain/EF-модель не менялась; миграция не требуется.

Фактические проверки:

1. До исправлений новые регрессии: **13 упали, 0 прошли, 0 пропущено**. Воспроизведены изменения чужих каталогов, неизвестные роли, вход с пробелами, отсутствие запрета архива и скрытая ошибка owner membership. Финальный baseline TRX: artifacts/compile-check/test-results/stage02-before/Spike_PC_2026-10-13_14_41_23_net10.0.trx.
2. После исправлений весь Application-набор: **298 прошли, 0 упали, 0 пропущено**, включая **48 Stage02RegressionTests** и все **23 FinancialDataIsolationTests этапа 01**. Команда: `dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application --verbosity quiet`. Финальный TRX: artifacts/compile-check/test-results/application/Spike_PC_2026-10-13_15_07_02_net10.0.trx.
3. Связанные Web unit tests: **23 прошли, 0 упали, 0 пропущено**. Команда: `dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:SkipFrontendBuild=true -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~ProductsModelTests|FullyQualifiedName~StoresModelTests|FullyQualifiedName~ReceiptsModelTests|FullyQualifiedName~MoneyMovementsModelTests|FullyQualifiedName~JsonResultMapperTests|FullyQualifiedName~LoginModelTests" --logger trx --results-directory artifacts/compile-check/test-results/web-stage02 --verbosity quiet`. Это прямые вызовы handler/mapper без hosting.
4. Изолированные проверки AuthenticationPasswordCompatibilityTests/HasherTests: **7 прошли, 0 упали, 0 пропущено**. Команда: `dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~AuthenticationPasswordCompatibilityTests|FullyQualifiedName~HasherTests" --logger trx --results-directory artifacts/compile-check/test-results/password-compatibility --verbosity quiet`. Только hashing и сценарии с подставным хранением, без БД или сети.
5. Compile-check: `dotnet build CostVision.Application/CostVision.Application.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/` и `dotnet build CostVision.Web/CostVision.Web.csproj -c Debug --no-restore -p:SkipFrontendBuild=true -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/` — обе сборки **0 ошибок, 0 предупреждений**. Web собран с зависимостями Domain/Application/Infrastructure, без запуска.
6. Frontend compile-check: `npm run build:ts` в CostVision.Web — успешно, strict и noEmitOnError сохранены. Разметка и тексты проверены статически; браузер не запускался.
7. Проверены 67 затронутых исходников, локальных инструкций и документов как UTF-8: U+FFFD, четырёх вопросительных знаков и проверенных признаков mojibake нет; отсутствующих целей локальных ссылок в изменённых документах нет. TRX отдельно подтверждает 48 Passed для этапа 02 и 23 Passed для FinancialDataIsolationTests этапа 01. В исходном коде приложения Hash вызывается только при создании/смене пароля с Trim; Hasher и параметры PBKDF2 не изменялись.

Оставшаяся приёмка:

- С отдельного разрешения выполнить SQL Server проверки обоих этапов: реальные предикаты/include, поведение сохранения/отказа, collation логина и роль счёта. Изолированный граф не подтверждает SQL, конкурентное архивирование, удаление роли или отзыв membership между проверкой и записью.
- С отдельного разрешения выполнить браузерные сценарии общего названия у двух пользователей и новых отказов. Прямые PageModel-тесты не подтверждают routing, cookie, antiforgery и production DI.
- Приложение, hosting, Docker, реальные БД, SQL, внешние интеграции, браузер и миграции не запускались; Git-операции не выполнялись.
- Статус этапа 01 сохранён, его оставшаяся SQL-приёмка не закрыта. Этапы 03–15 не начинались; известная дедупликация импорта/QR и общий архитектурный рефакторинг не входят в эту реализацию.
