# Этап 04. Целостность денежных операций

Статус: **Реализован, проверка не завершена**.
Дата начала: 13.10.2026.
Дата изолированной проверки: 13.10.2026.
Приоритет: **П1**.
Зависимости: 01, 03.

## Исходные проблемы аудита

1. **Критично: повтор новой строки импорта с заменой.** В [ImportMoneyMovementsUseCase](../../../CostVision.Application/UseCases/MoneyMovements/BankStatementImports/ImportMoneyMovementsUseCase.cs), `ValidateRows`, повтор допускается при `ReplaceDuplicate=true`. `FindDuplicateAsync` ищет в БД, а созданные в этом же пакете операции ещё не сохранены. Первая строка и повтор с флагом замены могут обе вызвать `Create`. В [MoneyMovementConfigure](../../../CostVision.Infrastructure/DataBase/ModelsConfigure/MoneyMovements/MoneyMovementConfigure.cs) нет уникального ключа импортированной строки. Проверить на SQL Server; unit mock не воспроизводит change tracker.
2. **Критично: скрытая частичная неудача.** [importModal.ts](../../../CostVision.Web/wwwroot/ts/pages/moneyMovements/importModal.ts), `handleImportSaveClick`, игнорирует результат `importMoneyMovements`, закрывает окно и обновляет список. Сервер возвращает успешный `ServiceResult` со счётчиками и line errors, поэтому пропущенные строки пользователь может не заметить.
3. `DuplicateMoneyMovementId` принимается как подсказка для замены любой найденной операции данного счёта до проверки ключа дубля. Доменный метод проверяет источник импорта, но совпадение строки с выбранным ID не проверяется. Решение о разрешённом редактировании существующей импортированной операции нужно отделить от поиска дубля.
4. [MoveMoneyMovementToAccountUseCase](../../../CostVision.Application/UseCases/MoneyMovements/Management/MoveMoneyMovementToAccountUseCase.cs) меняет счёт без загрузки и пересмотра связей с чеками. Перенос чека тоже не согласует денежные связи. Связь может перестать соответствовать принятому правилу общего счёта.
5. Массовый [AutoLinkExactMoneyMovementReceiptsUseCase](../../../CostVision.Application/UseCases/MoneyMovements/ReceiptLinks/AutoLinkExactMoneyMovementReceiptsUseCase.cs) допускает membership Viewer и не проверяет mutation-роль. По текущим исходникам зарегистрирован в DI, но вызова из PageModel нет: это дефект application-контракта, а не доказанный доступный HTTP-обход.
6. Уникальность пары связи защищена, но однозначность автоматической привязки между конкурентными запросами не защищена этим составным ключом. Один чек может получить разные пары. Алгоритм автоматического связывания повторяется в создании операции и массовом use case.

## План работ

- Вести типизированный ключ и локальный индекс импортируемых операций до сохранения; повтор в пакете должен обновить/пропустить уже принятую строку по явному правилу.
- Проверять ID дубля вместе с источником, счётом и согласованным контрактом замены. Повторный импорт и конкурентный импорт должны быть идемпотентны по принятому ключу.
- Показывать created/updated/error counts и ошибки строк; сохранять исправляемые строки в окне при частичном результате.
- Определить поведение связей при переносе: запрет несовместимого переноса либо явная атомарная отвязка. Не переносить молча чек в другой счёт вместе с операцией.
- Общий алгоритм точного сопоставления использовать с явным mutation-доступом и защитой конкуренции; сохранить разрешённые ручные множественные связи.

## Проверки и приёмка

- [x] Изолированно: повтор новой строки с заменой не создаёт вторую операцию; повтор существующей имеет ожидаемые счётчики.
- [x] Изолированно: чужой/устаревший/несовпадающий ID дубля не подменяет выбранную запись.
- [x] Контроллер с подставным DOM/fetch показывает частичные ошибки и исключает успешные строки из повторной отправки.
- [x] Изолированно: несовместимые переносы отклоняются до мутации; совместимые сохраняют связи атомарным сценарием.
- [x] Изолированно: Viewer не создаёт автоматические связи; неоднозначные кандидаты пропускаются.
- [ ] Браузер/HTTP: отображение итогов/ошибок и исправление строк, фокус/backdrop/Escape общего стека, повторные действия и Conflict.
- [ ] Конкурентный импорт и автоматическая привязка проверены на SQL Server отдельно.

## Журнал выполнения

13.10.2026: разрешена изолированная реализация только 04 при открытых проверках 01–03. Их статусы и ограничения сохранены; этапы 05–15 не начаты.

Решения пользователя: несовместимый перенос запрещён до ручной отвязки; ReplaceDuplicate допустим только при полном совпадении импортного ключа и меняет только пользовательский комментарий, сохраняя автора, исполнителя и связи.

Изменения:

- BankStatementRowKey объединяет preview, ключ локального индекса и предикат дубля импорта/переноса. Состав identity сохранён: точное OccurredAt, Amount, Type, нормализованный ImportComment. ID операции и AccountId размещения отдельны. Decimal/DateTime сравниваются как значения; string — ordinal. Сумма импорта проверяется на представление 18,2 без округления/переполнения.
- Локальный индекс содержит только принятые строки до SaveChanges. New/replace/new даёт Created=2, Updated=1, Error=0 и две операции. Счётчики относятся к принятым/отклонённым строкам, а не к числу уникальных обновлённых объектов. Последний принятый комментарий остаётся в операции.
- DuplicateMoneyMovementId должен совпадать с единственным дублем счёта, источника и полного ключа. Неверный ID не запускает fallback или Create. Неоднозначные старые дубли не заменяются произвольно. Доменный метод запрещает превращать Manual в BankStatementImport и менять identity под видом замены.
- Импорт сообщает line errors и метаданные явного обновления комментария полного дубля. importModal показывает счётчики, остаётся открытым, сохраняет непринятые/неотправленные строки и удаляет принятые clientRowId до reload списка. Ошибки сохранения переводятся из позиции пакета в номер строки PDF; ошибки parsing сохраняются. Повторный click и hide во время сохранения блокируются; смена счёта требует preview. HTTP helper, TypeScript strict/noEmitOnError, DOM API, AJAX-only и общий Bootstrap-стек сохранены.
- Оба переноса загружают связи внутри Serializable и проверяют будущую совместимость до мутации. Для операции допустим целевой счёт чека либо собственный чек без счетов; для чека — целевой и остальные сохранённые счета. Несовместимость требует ручной отвязки. Связи не удаляются, чек/операция вместе не переносятся. Импортная операция дополнительно не переносится в target с тем же банковским ключом.
- ExactMoneyMovementReceiptMatcher общий для Create и массового сценария: свободный Expense, свободный Income-чек, календарная дата, точная сумма и общий счёт; ровно один кандидат в обе стороны исходного набора. Viewer исключён предикатом роли. Неоднозначность нескольких счетов/кандидатов не разрешается порядком обхода. Ручные разные множественные связи сохранены.
- Права и координация остались в Application, PageModel не менялся. Базовые репозитории/predicate/include сохранены. Расчёты отчётов, refresh и общий архитектурный рефакторинг не выполнялись.

Модель и защита конкуренции:

- Проверены реальные EF metadata: у MoneyMovement нет уникального импортного ключа; индексы AccountId/OccurredAt и AccountId/OccurredAt/Amount/Type неуникальные. ImportComment — 1024, Amount — 18,2. PK MoneyMovementReceipt защищает лишь пару, не однозначность автоматического выбора.
- Существующая перегрузка UoW передаёт Serializable в реальный DatabaseFacade поставленной EFCoreLibrary. Все чтения дубля/свободности/совместимости и запись выбранных сценариев находятся внутри неё. SaveChanges вызывается явно; при Create первый вызов получает ID, второй сохраняет найденную связь. Прежняя перегрузка с автоматическим сохранением сохранена.
- Serializable выбран для удержания прочитанных строк и диапазонов отсутствующих дублей/связей до commit. 1205 возвращается как Conflict всего запроса без скрытого retry; денежные сценарии используют существующий ReceiptWriteConflictException как сигнал отмены явной транзакции, с собственным пользовательским сообщением. Неизвестные ошибки не превращаются в дубли. После отказа нужен новый scope: rollback не очищает change tracker.
- Это обоснованный сценарный механизм, не доказательство отсутствия гонки. Процессный lock, обычная транзакция и mock не используются как такое доказательство. Unique-пара не подменяет глобальную однозначность. SQL-проверка обязательна.
- EF-схема не менялась, миграция сейчас не требуется. Для независимой от протокола защиты любых писателей понадобятся сохраняемая банковская идентичность с уникальным размещением и отдельный согласованный контракт автоматического резервирования сторон, сохраняющий ручные множественные связи. Нельзя просто добавить unique ReceiptId или полный индекс по 1024-символьному тексту. Такое изменение модели требует отдельного решения, проверки имеющихся дублей/связей, пользовательской миграции и SQL-приёмки; сейчас оно не выполнено.

Фактические проверки:

1. До production-исправлений, после согласования FK тестового графа с назначенными ID: **23 теста, 12 упали, 11 прошли, 0 пропущено**. Воспроизведены повторный Create, неверный ID, Viewer и несовместимые переносы. Команда: `dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter FullyQualifiedName~Stage04RegressionTests --logger trx --results-directory artifacts/compile-check/test-results/stage04-before-corrected --verbosity quiet`. TRX: `artifacts/compile-check/test-results/stage04-before-corrected/Spike_PC_2026-10-13_21_09_14_net10.0.trx`.
2. Финальный весь Application-набор: **375 прошли, 0 упали, 0 пропущено**, включая **45 Stage04RegressionTests**, **32 Stage03RegressionTests**, **48 Stage02RegressionTests**, **23 FinancialDataIsolationTests**. Команда: `dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage04-final --verbosity quiet`. TRX: `artifacts/compile-check/test-results/application-stage04-final/Spike_PC_2026-10-13_21_38_40_net10.0.trx`.
3. Связанные Infrastructure-тесты: **34 прошли, 0 упали, 0 пропущено**: 3 Stage04MoneyPersistenceTests, прежние 30 связанных проверок 03/02 и MoneyMovementModelConfigurationTests. Команда: `dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~Stage04MoneyPersistenceTests|FullyQualifiedName~Stage03ReceiptPersistenceTests|FullyQualifiedName~Stage03UnitOfWorkTests|FullyQualifiedName~ReceiptModelConfigurationTests|FullyQualifiedName~MoneyMovementModelConfigurationTests|FullyQualifiedName~AuthenticationPasswordCompatibilityTests|FullyQualifiedName~HasherTests" --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage04-final --verbosity quiet`. TRX: `artifacts/compile-check/test-results/infrastructure-stage04-final/Spike_PC_2026-10-13_21_38_44_net10.0.trx`. Только EF metadata/ToQueryString, подставные DbConnection/DbTransaction, синтетические SqlException и hashing; реальная БД не использовалась.
4. Связанные Web unit tests: **26 прошли, 0 упали, 0 пропущено**. Команда: `dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:SkipFrontendBuild=true -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --filter "FullyQualifiedName~ProductsModelTests|FullyQualifiedName~StoresModelTests|FullyQualifiedName~ReceiptsModelTests|FullyQualifiedName~MoneyMovementsModelTests|FullyQualifiedName~JsonResultMapperTests|FullyQualifiedName~LoginModelTests|FullyQualifiedName~AddReceiptModelTests" --logger trx --results-directory artifacts/compile-check/test-results/web-stage04-final --verbosity quiet`. TRX: `artifacts/compile-check/test-results/web-stage04-final/Spike_PC_2026-10-13_21_38_48_net10.0.trx`. Прямые handler/mapper-вызовы без hosting, не HTTP-/браузерная приёмка.
5. Frontend: **8 прошли, 0 упали, 0 пропущено**, реальный собранный importModal с подставным DOM/fetch. Команда: `node --experimental-strip-types --test tests/Frontend/moneyMovementImport.test.mts`; финальный вывод сохранён через Tee-Object в `artifacts/compile-check/test-results/frontend-stage04-final.txt`. Node сообщил MODULE_TYPELESS_PACKAGE_JSON при загрузке существующих ES-модулей; package.json не менялся и предупреждение не подавлялось. Реальной сети/браузера нет.
6. Compile-check конкретных Domain, Application, Infrastructure, Web: `dotnet build <project>/<project>.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet`; Web дополнительно `-p:SkipFrontendBuild=true`. Каждая сборка: **0 предупреждений, 0 ошибок**. `npm run build:ts` из CostVision.Web — успешно; strict/noEmitOnError сохранены. Generated output обновлён только штатной сборкой.

7. Проверены 52 затронутых исходника/документа как строгий UTF-8: нет U+FFFD, четырёх вопросительных знаков и проверенных признаков mojibake. Ссылки изменённых Markdown и File/Project-элементы CostVision.slnx ведут на существующие цели. Исходные окончания строк сохранены; новые файлы — UTF-8 без BOM. TRX отдельно подтверждает все 45/32/48/23 регрессии 04/03/02/01.

Оставшаяся приёмка:

- SQL Server с отдельного разрешения: проверить реально применённую схему, precision и collation текста, настоящие predicate/include и материализацию связей. Разобрать старые импортные дубли и несовместимые связи, не удаляя их автоматически.
- Два независимых SQL-соединения: import/import одного ключа, замену существующего, смешанный пакет и rollback без частичных записей; import/перенос в target; create/auto, auto/auto разных счетов общего чека, manual-link/auto и оба переноса/link. Подтвердить диапазонные блокировки до commit и исход победитель/повтор либо 1205 с Conflict и повтором в новом scope. Изолированные результаты и ToQueryString этого не доказывают.
- Браузер/HTTP с отдельного разрешения: счётчики, PDF-номера ошибок, коррекция комментария, явная замена выявленного дубля, частичный повтор без принятых строк, смена счёта, отказ reload, закрытие/Escape и общий backdrop/focus. Node-заглушка не подтверждает Bootstrap, routing или antiforgery.
- Refresh и отдельная отвязка чека от счёта не объявлены защищёнными этим протоколом; расчёты отчётов остаются этапом 05. Общая координация preview/filter lifecycle остаётся этапом 10.
- SQL-приёмка 01, SQL-/браузерная приёмка 02 и SQL-/конкурентная/браузерная приёмка повторов/Conflict 03 открыты. Их исправления и статусы сохранены. Приложение, hosting, Docker, SQL, реальная БД, браузер, внешние интеграции, миграции и Git не запускались. Этапы 05–15 не начаты.

Передача: [самодостаточный промпт этапа 05](next-stage-05-prompt.md). Его подготовка не начинает реализацию 05. Без обязательной SQL-приёмки конкурентного импорта/связей этап 04 не завершён.
