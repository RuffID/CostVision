# Этап 05. Согласованные суммы отчётов

Статус: **Реализован, проверка не завершена**.
Дата начала: 13.10.2026.
Приоритет: **П1**.
Зависимости: 01, 03, 04.

## Найденные проблемы

1. **Критично: повторное суммирование одного документа.** [GetDashboardIncomeExpenseReportUseCase](../../../CostVision.Application/UseCases/Dashboard/GetDashboardIncomeExpenseReportUseCase.cs), `AddReceiptAmounts` и комбинированный расчёт, суммирует записи без группировки фискальной идентичности. [GetReceiptListUseCase](../../../CostVision.Application/UseCases/Receipts/Receipts/GetReceiptListUseCase.cs) группирует такие записи через `GetIdentityKey`. Две копии одного документа разных авторов, доступные пользователю через разные счета, дают разные итоги списка чеков и дашборда.
2. **Критично: потеря расхода при фильтре.** `HasExpenseMoneyMovementInPeriod` исключает чек по любой связанной расходной операции в периоде, даже если её счёт не выбран или операция недоступна и потому не попала в `moneyMovements`. Чек выбранного счёта может исчезнуть из суммы без добавления соответствующей операции.
3. `ReceiptMapper` формирует счётчики денежных связей, а детали/refresh не загружают их в том же объёме, что список. Нулевое значение может означать «не загружено», а не «связей нет»; после обновления карточки показатели могут стать противоречивыми.
4. Расчёт доступных кандидатов повторяется в отчётах чеков и магазинов. Признаки занятости, типы операций и параметры поиска не везде совпадают с экраном кандидатов.
5. Все виды фискальных операций суммируются положительно. Это факт реализации; корректный знак возвратов и других типов в расходном отчёте нужно определить как бизнес-правило, а не менять произвольно при рефакторинге. Преобразование количества и определение весового товара повторяются в двух use case.

## План работ

- Выделить чистый расчётный компонент с явными входными данными, уже ограниченными доступом и фильтрами.
- Согласовать единицу суммирования: фискальный документ для отчёта чеков; реальные денежные операции для движения средств. Не схлопывать разные банковские платежи только из-за совпадения суммы и даты.
- В комбинированном режиме исключать документ только при наличии учтённой операции по согласованному правилу. Разобрать связь нескольких копий документа, частичную оплату и связь с несколькими платежами.
- Согласовать DTO списка, деталей и refresh: отдельно загружать нужные агрегаты либо не затирать неизвестные данные фиктивным нулём.
- Вынести общие правила кандидатных счётчиков, денежных знаков и нормализации единиц без дублирования.

## Проверки и приёмка

- [x] Две доступные копии одного документа не удваивают расход по чекам — изолированные predicates/расчёт.
- [x] При выборе счёта А связь с исключённой операцией счёта Б не убирает расход А — изолированно.
- [x] Все режимы источника, пустые периоды, месяцы, store filters и граничные даты проверены изолированно.
- [x] Несколько платежей, частичное покрытие и возвраты имеют явно утверждённые ожидаемые суммы.
- [x] Список, детали и refresh дают согласованные агрегаты на загруженных тестовых графах; ToQueryString подтверждает перевод, не материализацию.
- [x] Добавлены отдельные тесты дашборда с конкретными суммами.
- [ ] Реальная SQL-материализация predicate/include/DTO, collation/precision, отзыв membership и согласованность нескольких чтений при конкурентных изменениях.
- [ ] HTTP-/браузерное отображение режимов, возвратов, итогов всех страниц, деталей/refresh и модальных переходов.

## Журнал выполнения

Изолированная реализация 05 разрешена пользователем при открытых проверках 01–04; их статусы и ограничения сохраняются. Дата изолированных проверок: 13.10.2026. Этапы 06–15, включая 07A, не начаты.

### Утверждённые правила

Решения получены отдельными вопросами до изменения формул. «Чеки» на главной означает только Receipt, включая доходные документы; банковская зарплата не входит. «Денежные операции» означает только MoneyMovement. «Чеки и операции» включает движения и непокрытые части документов. Список Receipts всегда содержит только чеки, переключатель главной его не меняет.

ReceiptOperationType.Income/RefundExpense — расход клиента; RefundIncome/Expense — доход. Суммы положительные, покупка 1000 и возврат 300 дают расход 1000/доход 300. Пользователь также утвердил раздельные доход/расход списка чеков и магазинов вместо 1300 номиналов. Граммы/1000, тонны×1000, кг и другие единицы без изменения.

Комбинированный режим: чек 1000 + платёж 400 → расход 1000; 400+600 → 1000; 700+700 → 1400. Два разных документа по 1000 с общим платежом 1000 → 2000. Возврат 300 со связанным зачислением 300 → доход 300. Каждый рубль платежа покрывает один документ по связи того же направления, сначала ранние документы; непокрытая часть остаётся на дате чека, платежи целиком на своих датах. Понедельник1000/вторник400 → 600/400. Переплата остаётся на дате платежа. Магазины сохраняют прежний контракт фильтра только расходной части.

### Изменения

- IncomeExpenseReportCalculator получает подготовленные входы; ReceiptPaymentCoverage распределяет decimal-покрытие по реальным связям с обратными рёбрами для перераспределения без потери приоритета ранних документов. Нет мутации связей или подмены matcher. Dashboard distinct по ReceiptIdentity и ID платежа; скрытые/исключённые операции не покрывают документ, закрытые чеки не раскрывают магазины.
- ReceiptFinancialRules общий для направлений и единиц. ReceiptMoneyMovementAggregates считает distinct доступные операции по копиям; document projection деталей/refresh дополнительно загружает необходимый граф. Отсутствие ожидаемого набора — явная ошибка, не фиктивный ноль. Refresh отдельно проверяет текущий доступ до workflow; identity, provider, retry/lifecycle не исправлялись.
- ReceiptMoneyMovementCandidateRules и отчётный helper используют фактический контракт начального окна: день ±1, сумма ±1, свободные движения обоих направлений, доступ/Accounts открываемой копии. Viewer читает кандидатов, запись Owner/Editor. Ручные множественные связи и автоматический matcher 04 сохранены.
- Сводки чеков/магазинов разделяют расход (TotalSum/TotalSpent) и IncomeSum до пагинации; магазин/группа/общий набор distinct по identity. UI отображает серверные суммы с копейками и неизвестное значение до загрузки. DTO OperationType синхронизирован в TS. Общие HTTP helper, DOM API, AJAX-only и Bootstrap-стек сохранены.
- Владельческие AGENTS и бизнес-/технические описания актуализированы. Схема не менялась; миграция не требуется.

### Фактические проверки

До исправлений первые 9 регрессий: 0 passed/9 failed/0 skipped, [TRX](../../../artifacts/compile-check/test-results/stage05-before/Spike_PC_2026-10-13_22_21_04_net10.0.trx). После утверждения бизнеса, до замены формул: 8 passed/13 failed/0 skipped из 21, [TRX](../../../artifacts/compile-check/test-results/stage05-business-before/Spike_PC_2026-10-13_22_31_00_net10.0.trx). Предварительная проверка include через обычный LINQ-граф была некорректной: EF Include там не сохраняет expression; она заменена отдельной проверкой фактического SQL-перевода. Две старые mock-настройки деталей/refresh дополнены новым чтением с выполнением настоящего predicate.

Последний полный Application: **425 passed/0 failed/0 skipped**, включая 50 Stage05RegressionTests, 45 Stage04RegressionTests, 32 Stage03RegressionTests, 48 Stage02RegressionTests, 23 FinancialDataIsolationTests и прежний набор. [TRX](../../../artifacts/compile-check/test-results/application-stage05/Spike_PC_2026-10-13_22_52_14_net10.0.trx).

Связанные Infrastructure: **37 passed/0 failed/0 skipped**, включая 3 Stage05ReportQueryTests и прежние 34. Использованы EF metadata/ToQueryString без соединения, подставные транзакции, синтетические SqlException и hashing. [TRX](../../../artifacts/compile-check/test-results/infrastructure-stage05/Spike_PC_2026-10-13_22_43_20_net10.0.trx).

Связанные Web: **26 passed/0 failed/0 skipped**, прямые handler-тесты без hosting. [TRX](../../../artifacts/compile-check/test-results/web-stage05/Spike_PC_2026-10-13_22_47_51_net10.0.trx).

Frontend: **11 passed/0 failed/0 skipped/0 cancelled** — прежние 8 import-контроллера и 3 новых реальных renderers с подставным DOM/fetch. [Transcript](../../../artifacts/compile-check/test-results/frontend-stage05.txt). Node сообщил существующее MODULE_TYPELESS_PACKAGE_JSON; предупреждение не подавлялось.

Команды из корня проекта:

```powershell
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage05 --verbosity quiet
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~Stage05ReportQueryTests|FullyQualifiedName~Stage04MoneyPersistenceTests|FullyQualifiedName~Stage03ReceiptPersistenceTests|FullyQualifiedName~Stage03UnitOfWorkTests|FullyQualifiedName~ReceiptModelConfigurationTests|FullyQualifiedName~MoneyMovementModelConfigurationTests|FullyQualifiedName~AuthenticationPasswordCompatibilityTests|FullyQualifiedName~HasherTests" --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage05 --verbosity quiet
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:SkipFrontendBuild=true -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~ProductsModelTests|FullyQualifiedName~StoresModelTests|FullyQualifiedName~ReceiptsModelTests|FullyQualifiedName~MoneyMovementsModelTests|FullyQualifiedName~JsonResultMapperTests|FullyQualifiedName~LoginModelTests|FullyQualifiedName~AddReceiptModelTests" --logger trx --results-directory artifacts/compile-check/test-results/web-stage05 --verbosity quiet
node --experimental-strip-types --test tests/Frontend/*.test.mts | Tee-Object -FilePath artifacts/compile-check/test-results/frontend-stage05.txt
```

`npm run build:ts` из CostVision.Web успешен со strict/noEmitOnError; generated output обновлён только компилятором. Конкретные compile-check **Domain/Application/Infrastructure/Web: 0 предупреждений/ошибок**:

```powershell
dotnet build CostVision.Domain/CostVision.Domain.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-domain-stage05.txt
dotnet build CostVision.Application/CostVision.Application.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-application-stage05.txt
dotnet build CostVision.Infrastructure/CostVision.Infrastructure.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-infrastructure-stage05.txt
dotnet build CostVision.Web/CostVision.Web.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-web-stage05.txt
```

### Оставшаяся приёмка и границы

Не запускались Git, приложение, hosting, Docker, SQL, реальная БД, браузер, внешние интеграции или миграции. Изолированные результаты не являются HTTP-, SQL-, браузерной или production-приёмкой.

Для 05 нужны SQL-проверки реальных predicate/include/DTO всех отчётов, деталей/refresh, distinct по копиям, collation/precision, отзыв membership и согласованность нескольких чтений при изменении связей. AsSplitQuery/последовательные запросы не гарантируют общий snapshot. Отдельно нужны браузерные/HTTP-сценарии переключателей, фильтров, раздельных итогов/копеек/пагинации, неизвестного состояния и смены магазина, отказов доступа и refresh.

01–04 остаются «Реализован, проверка не завершена»: SQL доступа/отзыва 01, SQL-/браузерная 02, SQL-/конкурентная/колляция/precision/браузерная 03. Для 04 открыты фактическая схема и старые дубли/несовместимые связи; на двух SQL-соединениях import/import, import/перенос, create/auto, auto/auto разных счетов, manual-link/auto, перенос/link, rollback/1205 и повтор в новом scope. Процессный lock/mock не заменяют эти проверки. Браузерная приёмка importModal/Conflict/Bootstrap также открыта. Refresh и отдельная отвязка чека от счёта не защищены этим протоколом автоматически.

Подготовлен [самодостаточный промпт этапа 06](prompts/next-stage-06-prompt.md); это не начало 06 и не разрешение закрывать предыдущую приёмку.
