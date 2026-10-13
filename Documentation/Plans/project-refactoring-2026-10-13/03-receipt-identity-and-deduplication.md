# Этап 03. Идентичность и дедупликация чеков

Статус: **Реализован, проверка не завершена**.
Дата начала: 13.10.2026.
Дата изолированной проверки: 13.10.2026.
Приоритет: **П1**.
Зависимости: 01.

## Найденные проблемы

1. **Критично: повтор нового QR-чека в одном пакете.** [SaveReceiptsScannedUseCase](../../../CostVision.Application/UseCases/Receipts/Receipts/SaveReceiptsScannedUseCase.cs) выполняет поиск в БД для каждого результата, вызывает `Create`, а `SaveChangesAsync` — только после обхода. Нет набора новых идентичностей внутри пакета. Два одинаковых новых результата могут попасть в запись вместе; уникальный индекс [ReceiptConfigure](../../../CostVision.Infrastructure/DataBase/ModelsConfigure/Receipts/ReceiptConfigure.cs) защитит БД, но общий запрос завершится ошибкой вместо обработки повторной строки. Фактический ответ SQL требуется проверить отдельно.
2. Проверки дублей ручного ввода, QR и переноса повторяют один набор полей. Между проверкой и записью возможна конкуренция. Уникальный индекс включает автора и не гарантирует единственность фискальной идентичности внутри общего счёта для разных авторов.
3. `GetIdentityKey` в [Receipt](../../../CostVision.Domain/Models/Receipts/Receipt.cs) и ключ банковской строки собираются строкой с форматированием суммы через текущую культуру. Для типизированной идентичности это лишняя зависимость и риск расхождения при вынесении ключа за границу одного вызова.
4. Загрузка существующего чека для добавления связи не включает его `Accounts`. Доменная проверка дублирования связи зависит от полноты загруженной коллекции; её нельзя считать проверкой БД.

## План работ

- Описать типизированную идентичность чека с сохранением принятого набора реквизитов. Разделить идентичность документа, уникальность копии автора и наличие документа в счёте.
- Вести локальный набор новых чеков и связей внутри QR-пакета. Определять результат каждого повтора, не полагаясь на повторный SQL-поиск до сохранения.
- Убрать повтор выражений идентичности из ручного ввода, QR, переноса и группировки отчётов.
- Переводить известный конфликт уникальности в ожидаемый результат. Существующий конфликт не маскировать успешным пустым ответом.
- Для общего счёта выбрать защиту от конкурентной записи, основанную на реальной модели и возможностях UoW. Обычная транзакция сама по себе не доказывает отсутствие гонки.

## Проверки и приёмка

- [x] Изолированно: пакет «новый чек, его повтор, другой новый чек» сохраняет два корректных документа и явно отмечает повтор.
- [x] Изолированно: повтор существующего чека и повтор новой связи имеют однозначный исход; отказ не создаёт лишний чек/связь.
- [x] Идентичность и группировка проверены в ru-RU/en-US, включая равные decimal с разным scale и строковые разделители в реквизитах.
- [ ] Два конкурентных запроса одного автора и разных авторов в общий счёт обработаны по согласованному правилу.
- [ ] Проверка SQL-ограничений выполнена отдельно на SQL Server; необходимость изменения схемы и миграции отмечена.

## Журнал выполнения

13.10.2026: разрешена изолированная реализация только этапа 03 при открытой SQL-приёмке 01 и SQL-/браузерной приёмке 02. Сначала добавляются регрессии текущих сценариев. Реляционная проверка конкурентной записи ещё не выполнена. Этапы 04–15 не начаты.

Изменения:

- ReceiptIdentity хранит прежние три фискальных реквизита, DateTime, TotalSum и OperationType. Идентичность документа не включает автора, Receipt.Id или счета. Авторские копии сохраняются отдельно, правила владения не менялись. GetIdentityKey использует InvariantCulture и G29; группировка использует типизированное значение.
- ReceiptIdentityPredicates задаёт ForDocument, ForAuthorCopy и InAccount, включая исключение переносимой копии. Ручной ввод, QR и перенос используют эти предикаты через базовые методы репозиториев. Predicate/include остаются в Application; Invoke, Compile и фильтрации в памяти внутри SQL-запроса нет.
- QR хранит локальные (Identity, AuthorId) и (Identity, AccountId), учитывая новые копии и новые связи до SaveChanges. Повтор получает ErrorMessage и входит в ErrorCount; валидные соседние строки сохраняются. Существующая авторская копия в ручном/QR-вводе загружается с Accounts перед TryAddAccount.
- Проверки дубля и запись всех трёх сценариев выполняются внутри Serializable-перегрузки ExecuteInTransaction. Сценарий явно вызывает SaveChanges; прежняя перегрузка с автоматическим SaveChanges сохранена. Финансовые расчёты, банковский импорт и refresh не исправлялись.
- Infrastructure распознаёт только SQL Server 2601/2627 с известным именем author identity index или PK_ReceiptAccounts. Application возвращает Conflict с сообщением; успешного пустого ответа нет. SQL Server 1205 имеет отдельный конкурентный исход без retry и повторного rollback уже откатившейся транзакции. Неизвестный индекс, FK, timeout и произвольные исключения не считаются дублями.
- При отказе сохранения/commit Application отменяет добавленные/перенесённые доменные связи. UoW запрещает повторное сохранение после ошибки явной транзакции: SQL rollback не очищает change tracker, для повтора нужен новый scope. Отказ проверки до мутации не вызывает Create/Delete/SaveChanges.
- Актуализированы инструкции владельцев, бизнес- и техническое описание, навигация. Подготовлен [промпт этапа 04](next-stage-04-prompt.md); сам этап 04 не начат.

Модель и выбранная защита:

- По текущим configuration и snapshot уникальный индекс Receipt включает CreatedByUserId и весь принятый набор identity. PK_ReceiptAccounts — (ReceiptId, AccountId). Эти ограничения защищают копию одного автора и одну конкретную связь, но не документ в общем счёте для разных авторов. Изолированная EF metadata-проверка подтверждает эту форму модели, не фактически применённую схему SQL.
- Прежний IUnitOfWork не позволял задавать изоляцию. Добавлена минимальная перегрузка, использующая реальный DatabaseFacade из поставленной EFCoreLibrary.dll. Сборка и тест через подставное DbConnection подтверждают наличие API и передачу Serializable, без SQL-соединения.
- Serializable выбран для удержания диапазонов проверки идентичности/Accounts до commit. При конкуренции возможен deadlock, который возвращается как ожидаемый отказ всего запроса. Это обоснование механизма, а не доказательство отсутствия гонки на SQL Server; отдельно нужны реальные планы запросов/блокировки и две независимые транзакции.
- Domain/EF-схема не менялась, пользовательская миграция сейчас не требуется. Отдельной уникальности (AccountId, ReceiptIdentity) в модели по-прежнему нет. Для защиты всех возможных писателей независимо от application-протокола понадобится общая модель идентичности документа и уникального размещения в счёте с сохранением копий автора либо согласованное хранение identity в размещении. Это отдельное изменение модели, пользовательская миграция и проверка существующих дублей; оно не подменено индексом по ReceiptId, процессным lock или фиктивной реализацией.

Фактические проверки:

1. До исправлений 17 новых регрессий: **8 упали, 9 прошли, 0 пропущено**. Воспроизведены повторное Create в пакете, неявный повтор собственной копии, отсутствие Accounts при загрузке и зависимость строкового ключа от культуры. TRX: `artifacts/compile-check/test-results/stage03-before/Spike_PC_2026-10-13_15_33_25_net10.0.trx`.
2. Финальный весь Application-набор: **330 прошли, 0 упали, 0 пропущено**, включая **32 Stage03RegressionTests**, **48 Stage02RegressionTests**, **23 FinancialDataIsolationTests**. Команда: `dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage03-final --verbosity quiet`. TRX: `artifacts/compile-check/test-results/application-stage03-final/Spike_PC_2026-10-13_15_53_57_net10.0.trx`.
3. Связанные Infrastructure-тесты: **30 прошли, 0 упали, 0 пропущено**: 10 Stage03ReceiptPersistenceTests, 8 Stage03UnitOfWorkTests, 5 прежних ReceiptModelConfigurationTests и 7 hashing/совместимости паролей этапа 02. Команда: `dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~Stage03ReceiptPersistenceTests|FullyQualifiedName~Stage03UnitOfWorkTests|FullyQualifiedName~ReceiptModelConfigurationTests|FullyQualifiedName~AuthenticationPasswordCompatibilityTests|FullyQualifiedName~HasherTests" --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage03-final --verbosity quiet`. TRX: `artifacts/compile-check/test-results/infrastructure-stage03-final/Spike_PC_2026-10-13_15_54_01_net10.0.trx`. Только EF metadata/ToQueryString, подставное DbConnection, синтетические SqlException и hashing; реальная БД/сеть не использовались. Reflection в тесте создаёт синтетическую ошибку актуального SqlClient, а не добавляет отсутствующую возможность UoW.
4. Связанные Web-тесты: **26 прошли, 0 упали, 0 пропущено**, включая прежние 23 этапа 02 и 3 AddReceiptModelTests. Команда: `dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:SkipFrontendBuild=true -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~ProductsModelTests|FullyQualifiedName~StoresModelTests|FullyQualifiedName~ReceiptsModelTests|FullyQualifiedName~MoneyMovementsModelTests|FullyQualifiedName~JsonResultMapperTests|FullyQualifiedName~LoginModelTests|FullyQualifiedName~AddReceiptModelTests" --logger trx --results-directory artifacts/compile-check/test-results/web-stage03-final --verbosity quiet`. TRX: `artifacts/compile-check/test-results/web-stage03-final/Spike_PC_2026-10-13_15_54_06_net10.0.trx`. Прямые вызовы handler/mapper без hosting; Conflict мапится в 409 существующим JsonResultMapper.
5. Compile-check отдельных Domain, Application, Infrastructure и Web: `dotnet build <project>/<project>.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet`; для Web дополнительно `-p:SkipFrontendBuild=true`. Каждая сборка: **0 ошибок, 0 предупреждений**. TypeScript не менялся, frontend build не запускался; прежний compile-check этапа 02 не выдаётся за новую проверку.
6. После окончательного оформления AAA и проверок результата повторно выполнены все **32 Stage03RegressionTests: 32 прошли, 0 упали, 0 пропущено**. TRX: `artifacts/compile-check/test-results/stage03-final-style/Spike_PC_2026-10-13_16_00_00_net10.0.trx`. Проверены 41 затронутый исходник/документ как UTF-8, отсутствие U+FFFD, четырёх вопросительных знаков и проверенных признаков mojibake; локальные ссылки и File-элементы CostVision.slnx ведут на существующие файлы. Сохранены исходные BOM и окончания строк изменённых файлов; generated-файлы вручную не менялись.

Оставшаяся приёмка:

- SQL Server: подтвердить реально применённый author identity index/PK, перевод и материализацию Accounts, колляцию фискальных строк и precision 18,2; проверить реальные 2601/2627 и rollback без частичной записи.
- На двух независимых SQL-соединениях проверить одного автора и разных авторов в общий счёт: одновременно новые копии, уже существующие отдельные копии, добавление одной связи, смешанные ручной/QR/перенос запросы и пакет с повтором. Проверить Serializable и диапазонные блокировки до commit, победителя/повтор либо 1205 с понятным Conflict и повтором в новом scope. Обе копии одного документа в одном счёте после конкурентной записи недопустимы. Mock/ToQueryString/обычная транзакция этого не доказывают.
- Браузер/HTTP с отдельного разрешения: отображение ErrorCount/ошибки повторной строки и Conflict без потери исправляемых данных. Изолированные PageModel-тесты не подтверждают routing, antiforgery или UI.
- Конкурентный refresh, способный менять identity, остаётся риском этапа 06. Координация денежных связей при переносе и импорт остаются в этапе 04; расчёты отчётов — в этапе 05. Эти пути не считаются защищёнными реализацией 03 автоматически.
- SQL-приёмка 01 и SQL-/браузерная приёмка 02 открыты, их статусы и исправления сохранены. Приложение, hosting, Docker, SQL, реальная БД, браузер, внешние интеграции, миграции и Git не запускались. Этапы 04–15 не начаты. Статус 03 — «Реализован, проверка не завершена» до обязательных SQL-ограничений и конкурентной записи на SQL Server.
