# Этап 06. Надёжное обновление чеков

Статус: **Реализован, проверка не завершена**.
Дата начала: 13.10.2026.
Дата изолированной проверки: 14.10.2026.
Приоритет: **П1/П2**.
Зависимости: 03, 04.

## Исходные проблемы аудита

1. **Критично: подмена идентичности при обновлении.** [ExternalReceiptProvider](../../../CostVision.Infrastructure/Services/Receipts/ExternalReceiptProvider.cs) валидирует внешний документ и присваивает ему ID исходного чека, но не сравнивает фискальные реквизиты с запросом. [Receipt.TryRefreshFrom](../../../CostVision.Domain/Models/Receipts/Receipt.cs) затем перезаписывает накопитель, номер документа, признак, дату, сумму и вид операции. Валидный ответ про другой документ может заменить уже связанный чек. Сценарий проверен по коду, фактический неверный ответ сервиса не наблюдался.
2. [RefreshPendingReceiptsUseCase](../../../CostVision.Application/UseCases/Receipts/Receipts/RefreshPendingReceiptsUseCase.cs) загружает все ожидающие чеки одним tracked-списком и обрабатывает в одном UoW. [ReceiptRefreshWorkflow](../../../CostVision.Application/UseCases/Receipts/Receipts/Refresh/ReceiptRefreshWorkflow.cs) кэширует товары лишь внутри одного чека. Повторная обработка товара в общем контексте и крупный tracked-граф увеличивают память и зависят от поведения DLL-репозитория.
3. Неожиданное исключение одного чека прерывает весь проход. HTTP timeout/transport exception не превращаются здесь в сохранённый результат фоновой попытки; один проблемный чек способен повторно мешать обработке последующих.
4. Проверка расписания и числа попыток выполняется до внешнего вызова, без механизма владения задачей. Ручной refresh, фон и несколько экземпляров могут обновлять один документ одновременно. В EF-конфигурации нет токена конкурентного изменения.
5. Workflow создаёт/обновляет store/product до финального результата применения. В ветке фонового отказа вызывается общий `SaveChangesAsync`. Если отказ наступил после мутаций графа, нужно доказать отсутствие сохранения частичного состояния. Встроенный provider заранее валидирует много условий, поэтому это риск границы workflow, а не доказанное частичное сохранение обычного ответа.

## План работ

- Проверять совпадение фискальной идентичности до изменения tracked-графа. Отдельно определить допустимое уточнение суммы/времени, если оно действительно требуется внешним контрактом.
- Ограничить выборку pending-чеков пакетами с устойчивым порядком. Обрабатывать один документ в отдельной управляемой границе сохранения и освобождения контекста.
- Разделить подготовку проверенного внешнего результата, применение графа и сохранение retry-состояния. При отказе не сохранять изменения store/product/items вместе с отметкой ошибки.
- Согласовать классификацию временных внешних ошибок, регистрацию попытки и продолжение обработки следующих чеков. Отмену приложения не считать очередной неудачной попыткой.
- Добавить защиту конкурентного обновления с учётом реального количества экземпляров; не считать локальный lock распределённой защитой.

## Проверки и приёмка

- [x] Чужая фискальная идентичность отклоняется без изменения чека, каталога и связей — изолированные Domain/provider/workflow-регрессии.
- [x] Ошибка/timeout одного документа не блокирует следующие; отмена останавливает обработку — actual workflow, подставное хранение/HTTP и настоящие DI scopes.
- [x] Семь зарегистрированных попыток, UTC/локальная полночь и ручное восстановление сохраняют контракт — изолированно.
- [x] Частичный граф после отказа не сохраняется следующей операцией — изолированная модель хранения и новый scope; реальный SQL rollback ещё открыт.
- [ ] Два конкурентных refresh и запись новых товаров/магазинов проверены отдельно на SQL Server.
- [x] Выборка ограничена 32 записями, scopes освобождаются; пакеты 32/3 и actual DI-контексты проверены изолированно. Реальное потребление памяти/SQL-планы ещё не измерены.

## Журнал выполнения

Изолированная реализация только 06 разрешена при открытых проверках 01–05. Этапы 07–15, включая 07A, не начаты. Реальные API, фон, БД и несколько экземпляров не запускались.

### Изменения

- ExternalReceiptProvider сравнивает все поля ReceiptIdentity до назначения исходного ID и подготовки магазина/позиций. Workflow проверяет identity до каталога и валидирует полный detached-граф; Domain отклоняет чужой документ до мутации. Допусков/уточнения суммы или времени нет. ProverkachekaReceiptMapper сохраняет фактическое преобразование копеек и времени; его контракт проверен связанными тестами.
- Ручной use case читает no-tracking снимок и передаёт явный ID пользователя. После внешнего HTTP workflow в короткой Serializable-транзакции повторно читает tracked-чек с Items/Product, Store/автором и Accounts/Account/Members; проверяет текущий доступ, RowVersion и расписание. Граф/каталоги/успешное состояние сохраняются одним явным SaveChanges, без дополнительного автоматического сохранения. Автор, счета, денежные связи, порядок позиций и агрегаты 05 сохранены.
- Receipt.RowVersion настроен IsRowVersion. RefreshRevision увеличивается при каждой успешной замене графа, обеспечивая UPDATE чека с проверкой RowVersion даже при совпадении остальных реквизитов/времени. Нужна пользовательская миграция **AddReceiptRowVersion**, добавляющая SQL Server rowversion и обязательный bigint RefreshRevision с default 0 существующим и новым Receipts. Migration/designer/snapshot агент не создавал и не менял. До использования модели нужна проверка миграции/реальной схемы и существующих данных/normalized-индексов каталогов. Старые дубли не удаляются автоматически.
- Фон использует no-tracking пакеты по 32, устойчивый порядок/курсор CreatedAtUtc/Id и верхнюю границу времени начала прохода. Каждый пакет читается в освобождаемом scope; каждый документ обрабатывается в отдельном async scope. Пауза в секунду сохранена, часы подставляются через TimeProvider. Общий UoW не разделён, отчёты не пагинированы: 07/07A не начаты.
- HttpRequestException, TimeoutException и отмена транспорта при активном входном token дают ExternalService. Отмена приложения не сохраняет попытку. Неизвестный отказ логируется с исходным исключением; после disposal отказавшего scope фон отдельно сохраняет только retry-state в новом scope, с проверкой версии/расписания и без повторного provider/каталога. Если эта запись тоже отказала, есть отдельный журнал и продолжение следующих документов; третьего сохранения нет. Семь зарегистрированных попыток, Failed и ручное восстановление сохранены.
- DbUpdateConcurrencyException, 1205 и известные unique-конфликты дают Conflict. К прежним ограничениям отдельно добавлены IX_Products_NormalizedName/IX_Stores_NormalizedName_NormalizedAddress; AdaptiveName/unknown не маскируются под дубль. Нет скрытого повторения refresh или процессного lock. RowVersion защищает запись результата, но не резервирует HTTP-задачу: параллельные вызовы provider допустимы.
- Владельческие AGENTS.md, бизнес-/техническая документация и навигация обновлены. Статусы/открытая приёмка 01–05 сохранены.

### Фактические проверки

До исправления: **0 passed/7 failed/0 skipped** — воспроизведена подмена шести полей identity и обращение workflow к каталогу до отказа. [TRX](../../../artifacts/compile-check/test-results/stage06-before/Spike_PC_2026-10-13_23_07_14_net10.0.trx).

Финальные проверки 14.10.2026:

- **Application: 461 passed/0 failed/0 skipped**, включая 27 Stage06RegressionTests, 9 Stage06BatchTests, прежние 50 Stage05RegressionTests, 45 Stage04RegressionTests, 32 Stage03RegressionTests, 48 Stage02RegressionTests, 23 FinancialDataIsolationTests и остальной набор. Финальный запуск после добавления RefreshRevision и проверки конфликта каталога. [TRX](../../../artifacts/compile-check/test-results/application-stage06/Spike_PC_2026-10-14_10_52_30_net10.0.trx).
- **Связанные Infrastructure: 63 passed/0 failed/0 skipped** — RowVersion/RefreshRevision metadata, actual predicate/order/take/Guid cursor через ToQueryString без соединения, actual DI/DLL в независимых scopes без hosting, actual DLL ORDER BY/TOP(32) на подставном DbConnection, provider/mapper, прежние 01–05 и синтетические SQL-конфликты/транзакции. [TRX](../../../artifacts/compile-check/test-results/infrastructure-stage06/Spike_PC_2026-10-14_10_52_42_net10.0.trx).
- **Связанные Web: 26 passed/0 failed/0 skipped**, прямые handler/mapper-вызовы без hosting. [TRX](../../../artifacts/compile-check/test-results/web-stage06/Spike_PC_2026-10-14_10_52_57_net10.0.trx).
- **Frontend: 11 passed/0 failed/0 skipped/0 cancelled** после npm run build:ts, подставные DOM/fetch; TypeScript-исходники в 06 не менялись. Существующее MODULE_TYPELESS_PACKAGE_JSON не подавлялось. [Transcript](../../../artifacts/compile-check/test-results/frontend-stage06.txt).
- Конкретные **Domain/Application/Infrastructure/Web compile-check: 0 предупреждений/0 ошибок**; TypeScript build успешен, strict/noEmitOnError сохранены. Выводы: build-domain-stage06.txt, build-application-stage06.txt, build-infrastructure-stage06.txt, build-web-stage06.txt, build-typescript-stage06.txt в artifacts/compile-check/test-results.

Команды из корня (npm — из CostVision.Web):

```powershell
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage06 --verbosity quiet
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~Stage06RefreshPersistenceTests|FullyQualifiedName~ExternalReceiptProviderTests|FullyQualifiedName~ProverkachekaReceiptMapperTests|FullyQualifiedName~Stage05ReportQueryTests|FullyQualifiedName~Stage04MoneyPersistenceTests|FullyQualifiedName~Stage03ReceiptPersistenceTests|FullyQualifiedName~Stage03UnitOfWorkTests|FullyQualifiedName~ReceiptModelConfigurationTests|FullyQualifiedName~MoneyMovementModelConfigurationTests|FullyQualifiedName~AuthenticationPasswordCompatibilityTests|FullyQualifiedName~HasherTests" --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage06 --verbosity quiet
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:SkipFrontendBuild=true -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~ProductsModelTests|FullyQualifiedName~StoresModelTests|FullyQualifiedName~ReceiptsModelTests|FullyQualifiedName~MoneyMovementsModelTests|FullyQualifiedName~JsonResultMapperTests|FullyQualifiedName~LoginModelTests|FullyQualifiedName~AddReceiptModelTests" --logger trx --results-directory artifacts/compile-check/test-results/web-stage06 --verbosity quiet
node --experimental-strip-types --test tests/Frontend/*.test.mts | Tee-Object -FilePath artifacts/compile-check/test-results/frontend-stage06.txt
dotnet build CostVision.Domain/CostVision.Domain.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-domain-stage06.txt
dotnet build CostVision.Application/CostVision.Application.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-application-stage06.txt
dotnet build CostVision.Infrastructure/CostVision.Infrastructure.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-infrastructure-stage06.txt
dotnet build CostVision.Web/CostVision.Web.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet | Tee-Object -FilePath artifacts/compile-check/test-results/build-web-stage06.txt
```

```powershell
npm run build:ts | Tee-Object -FilePath ../artifacts/compile-check/test-results/build-typescript-stage06.txt
```

Тестовые снимки/commit, повреждённая материализация через reflection и ускоренные часы существуют только в изолированных тестах. Они не являются production-реализацией хранения, SQL rollback, EF fixup или распределённой приёмкой. Actual DI/DLL проверены без запуска service/HTTP/SQL. Промежуточная ошибка тестового доступа к internal FinancialDataAccess устранена проверкой через реальный use case; production API/friend assemblies не расширены. Промежуточный snapshot-тест отказа retry-state исправлен разделением сохранённого и tracked-снимка, без ослабления ожидания нулевой сохранённой попытки после отказа.

### Оставшаяся приёмка

- Пользователь создаёт и проверяет AddReceiptRowVersion с обоими столбцами RowVersion и RefreshRevision. Нужно проверить actual SQL rowversion/bigint/default/индексы, старые normalized-дубли, collation/precision и реальные predicate/include/DTO/отношения. До миграции обновлённая модель не совместима со старой схемой. Никакого SQL/миграции в этой задаче не выполнялось.
- На двух независимых SQL-соединениях: manual/manual, manual/background, background/background одного ReceiptId; обновление разных чеков с новыми/существующими общими Store/Product; конфликт с другим писателем каталога; UPDATE по rowversion (включая замену позиций с одинаковыми реквизитами/временем), 1205, rollback полного графа, запись retry-state в новом scope и повтор после Conflict. Проверить реальную EF fixup/замену позиций и отсутствие частичных каталога/графа после отмены/отказа commit. Mock/metadata/ToQueryString и process lock этого не доказывают.
- Реальные provider HTTP/transport/timeout/неверная identity, отмена и перезапуск приложения/hosted lifecycle/локальная полночь; HTTP-/браузерное отображение ручного восстановления/Conflict и сохранённых агрегатов 05. Внешний контракт точного времени/суммы не подтверждён живым API; допустимое уточнение требует отдельного решения пользователя.
- Открыты SQL 01, SQL-/браузерная 02, SQL-/конкурентная/браузерная/precision/collation 03; для 04 — схема/старые дубли/несовместимые связи, import/import, import/перенос, create/auto, auto/auto разных счетов, manual-link/auto, перенос/link, rollback/1205/новый scope и importModal/Conflict/Bootstrap в браузере.
- Для 05 открыты actual predicate/include/DTO/агрегаты всех отчётов/деталей/refresh, отзыв membership и согласованность последовательных чтений при изменении связей. AsSplitQuery не даёт общего snapshot. HTTP-/браузерные режимы/фильтры/доходы/расходы/копейки/пагинация/неизвестные итоги открыты. Изолированная 06 не закрывает эти проверки.
- Git, приложение, hosting, Docker, SQL, реальная БД, браузер, внешние интеграции и миграции не запускались. Этапы 07–15, включая 07A, не начаты.

Передача: [самодостаточный промпт этапа 07](next-stage-07-prompt.md). Его подготовка не разрешает реализацию 07.
