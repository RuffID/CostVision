# Этап 07. Ограниченная загрузка данных

Статус: **Реализован, проверка не завершена**.
Дата начала: 14.10.2026.
Приоритет: **П2**.
Зависимости: 01, 03, 05.

## Исходные проблемы до этапа 07

1. [GetReceiptListPageUseCase](../../../CostVision.Application/UseCases/Receipts/Receipts/GetReceiptListPageUseCase.cs) сначала получает полный список чеков за период, затем фильтрует, считает и выполняет `Skip/Take` в памяти. Размер ответа ограничен, стоимость загрузки — нет.
2. [GetProductListUseCase](../../../CostVision.Application/UseCases/Receipts/Products/GetProductListUseCase.cs) загружает товары со всеми покупками, чеками, счетами и участниками. Фильтры дат/счёта применяются позднее в `IsReceiptAccessible`; исходный SQL отбирает лишь доступность и поиск товара. [GetStoreListUseCase](../../../CostVision.Application/UseCases/Receipts/Stores/GetStoreListUseCase.cs) имеет аналогичный полный граф и пагинацию в памяти.
3. [GetStoreReceiptListUseCase](../../../CostVision.Application/UseCases/Receipts/Stores/GetStoreReceiptListUseCase.cs) загружает все чеки магазина, а кандидатные счётчики считает до выбора страницы. Два метода построения словарей и подсчёт повторяются в отчёте чеков.
4. [GetMoneyMovementListUseCase](../../../CostVision.Application/UseCases/MoneyMovements/Queries/GetMoneyMovementListUseCase.cs) загружает все движения и свободные чеки периода. Оба use case кандидатов возвращают весь набор без `skip/take`, несмотря на заявление о серверной пагинации в существующей технической документации.
5. Дашборд материализует графы и считает всё в памяти. `GetProductStorePurchasesUseCase` загружает все позиции, нормализует и агрегирует их на сервере приложения. `AsSplitQuery` снижает раздувание join, но не ограничивает общий объём.
6. Сортировки часто не имеют окончательного уникального критерия. После переноса пагинации в БД равные даты/названия могут давать нестабильные границы страниц.

## План работ

- Для каждого экрана определить отдельные данные страницы, общий count, общую сумму фильтра и счётчики только отображаемых строк.
- Перенести фильтры, сортировку, агрегаты и ограничение в query-композицию по существующему базовому контракту. Не вычислять итог по странице вместо всего фильтра.
- Сначала обеспечить группировку идентичности чеков, затем пагинацию групп. Простое `Take` сырых записей изменит результат.
- Загружать минимум нужных навигаций. Для read-only выбирать no-tracking; проверить фактические defaults DLL, не предполагать их.
- Предусмотреть ограниченные страницы кандидатов, пределы периода и файлов/пакетов. Не обрезать результаты молча.
- Если существующей DLL не хватает count/projection/aggregation, зафиксировать конкретный недостающий контракт и согласовать изменение; не вводить специализированные методы репозитория или обходной SQL без разрешения.

## Проверки и приёмка

- [ ] Маленькая страница не требует материализации всей истории и memberships.
- [ ] Общая сумма и count соответствуют всему фильтру, а не текущей странице.
- [ ] Группы чеков и магазинов не распадаются на границе страниц.
- [ ] Порядок устойчив при одинаковых значениях сортировки.
- [ ] Число запросов, объём загрузки и трансляция проверены на разрешённом SQL-контуре; mock и compile-check не считаются такой проверкой.

## Журнал выполнения

14.10.2026: пользователь разрешил только этап 07 при открытых проверках 01–06. Миграцию AddReceiptRowVersion с RowVersion и RefreshRevision создаёт пользователь; агент её не создаёт. 07A и 08–15 не начаты. Начата карта actual query paths и проверка контракта поставленной EFCoreLibrary.dll; SQL-/HTTP-/браузерная приёмка не выполняется.
## Фактическая карта чтения после 07

В таблице перечислены логические обращения к репозиториям. Это не измеренное число SQL-команд: AsSplitQuery добавляет команды, а агрегаты содержат коррелированные подзапросы. Физические команды, строки, планы и время остаются предметом SQL-приёмки.

| Путь | Страница/результат | Count и суммы полного фильтра | Граф и счётчики |
|---|---|---|---|
| Receipts / GetReceiptListPageUseCase | Доступ+период; фильтр счёта/видимых операций/поиск; представитель identity, порядок DateTime DESC/фискальные поля/Id, затем Skip/Take ID | Один scalar COUNT/Income/Expense по документам после всех фильтров | Копии только выбранных документов с Store/author/видимым отчётным графом; отдельное чтение Accounts открываемых ReceiptId и COUNT кандидатов лишь для показанных строк |
| Products | Root Any доступной покупки учитывает период и доступ к выбранному счёту; scalar aggregates; стабильная сортировка с Id и Skip/Take | COUNT и сумма позиций всего фильтра отдельно от страницы | История позиций/чеков/участников не материализуется; единицы через expression-селектор ReceiptFinancialRules |
| Stores | Root Any доступного чека в периоде/счёте и поиск; представитель NormalizedName до страницы; стабильный Id | COUNT групп отдельно; distinct документы всего фильтра для дохода/расхода | Scalar children адресов только для выбранных групп, история/участники не загружаются |
| StoreReceipts | Доступ+StoreId/GroupKey; документы identity до страницы ID | Полный distinct COUNT/доход/расход | Тот же граф копий страницы и счётчики открываемых ReceiptId, что у Receipts |
| MoneyMovements | Доступ+период/счёт/Comment/ImportComment/видимые receipt filters; OccurredAt DESC/Id, Skip/Take; только scalar DTO | Отдельный COUNT/Income/Expense полного фильтра | Нет графа memberships; только для показанных операций выполняется COUNT свободных доступных копий по прежней точной календарной дате/сумме/счёту |
| Операции для чека | Исходная реальная копия и Accounts; прежние ручные фильтры; точное расстояние даты/суммы/Id до skip/take базового DLL list API | Отдельный COUNT операций всего фильтра | Граф связей/доступа лишь для операций страницы; ответ PagedListDto |
| Чеки для операции | Проверка исходной операции; прежние фильтры; canonical identity до ограничения страницы | COUNT документов, не копий | Все подходящие копии выбранных документов, порядок групп страницы сохранён; Items может быть длиннее PageSize |
| Dashboard | DISTINCT-каталог магазинов отдельно до store filter; только использованные источники; scalar identity/связи и ID/date/amount/type | Полный отфильтрованный scalar-набор передаётся точному IncomeExpenseReportCalculator/ReceiptPaymentCoverage | Нет сущностей/истории/memberships; произвольного Take нет, потому что покрытие нескольких документов требует всех выбранных рёбер |
| ProductStorePurchases | ProductId+доступ+наличие магазина; GroupBy StoreId/display name/вид единиц; агрегаты количества/цены, стабильный порядок | Весь агрегированный набор выбранного товара | История ReceiptItem/Receipt/Accounts/Members не материализуется; формат массива сохранён, результат не обрезается |

Legacy GetReceiptListUseCase больше не вызывается экраном; его полный read-контракт и прежние регрессионные потребители сохранены. Details/refresh, linked lists одного объекта, импорт, автоматический matcher и фоновый refresh не объявляются новыми paged-экранами. Их утверждённые контракты 01–06 сохранены.

## Подтверждённый контракт DLL и согласованное расширение

У поставленного IGetItemByPredicateRepository<T, AppDbContextBase> список возвращает Task<List<T>>; defaults skip=0, take=null, asNoTracking=false; include имеет тип Func<IQueryable<T>, IQueryable<T>>. Arbitrary TResult/count/aggregation в этом list API отсутствуют. Отдельный IQueryRepository<T, AppDbContextBase>.Query(bool asNoTracking) возвращает IQueryable<T>.

Пользователь явно согласовал общий GetProjectedItemsAsync<TResult>(predicate, query, ct). IProjectedReadRepository используется Receipt/ReceiptItem/Product/Store/MoneyMovement. ProjectedReadQuery применяет predicate к DLL Query(asNoTracking: true), затем переданную вызывающим сценарием проекцию и ToListAsync(ct). Базовые CRUD/include сохранены; specialized выборок, SQL вручную, reflection в production, второго контекста и разделения UoW нет.

## Реализованные границы

- Размер всех страниц 1–100, default кандидатов/операций 20; номер ограничивается 1..max(1, ceil(count/pageSize)). Пустая выдача имеет известный count=0 и страницу 1.
- Фискальные группы и группы магазинов ограничиваются до загрузки копий/адресов. Count и финансовые totals принадлежат всему фильтру. ID реальных платежей и шесть полей документа не подменены датой/суммой.
- SQL-поиск по магазину и mapper используют один приоритет представителя: редактируемая копия, затем ordinal-строковый ID. Размещения сохраняют доступ и фактический открываемый ReceiptId. Неизвестный фискальный тип не превращается в нулевую сумму.
- Оба окна кандидатов и список операций показывают явные страницы/count; обратное окно подписывает количество документов и сохраняет все подходящие копии. Поиск/receiptFilter операций перенесены на сервер; frontend не пересчитывает сводку из Items. Изменение фильтра/объекта сбрасывает страницу. Локальные проверки актуальности ответа не являются реализацией общих этапов 10–11.
- SQL-предикаты и projection содержат переводимые conditional expressions; вызовы обычных финансовых методов, Compile/Invoke и фильтрация в памяти не используются вместо query-композиции. Чистый глобальный алгоритм покрытия 05 сохранён.
- Этап не меняет схему, import identity, matcher, ручную множественность, автора, refresh/retry или транзакционные протоколы. Пользовательская AddReceiptRowVersion по-прежнему обязательна: RowVersion rowversion и RefreshRevision bigint NOT NULL default 0. Migration/designer/snapshot не созданы и не изменены.

## Изолированные проверки

14.10.2026 перед исправлениями новая Application-матрица: 2 passed / 8 failed / 0 skipped; ошибки показывали поздние root filters/нестабильные границы/page-only counters. После реализации выполняется actual predicate/projection над доменными графами. Наблюдение root-запросов обязательно непустое, а не фиктивная пустая история старого mock API.

Stage07RegressionTests: 15 cases; проверяются текущий доступ/отзыв membership/скрытый выбранный счёт, полные суммы, одинаковые сортировки, группы документов/магазинов, ограничение счётчиков показанными строками, пустая/крайняя страница, точное расстояние дат на границе года и високосного дня. Все регрессии 01–06 включены в полный Application-набор; прежние суммы/покрытие не ослаблены.

Stage07ReadQueryTests проверяет actual DLL metadata и перевод root filters, SQL агрегатов и страниц каталогов/чеков/операций/обратных кандидатов/ProductStorePurchases/dashboard через SQL Server ToQueryString без соединения. COUNT stores требует одного root predicate; page aggregates проверяют его также внутри покупок. Stage05ReportQueryTests сохраняет проверки детали/refresh и использует новые проекции dashboard/кандидатов. Stage07ProjectionExecutionTests исполняет actual adapter/DI/DLL на подставном DbConnection: typed COUNT и Guid-страница, ORDER BY/OFFSET/FETCH, token и отсутствие tracker; реальное соединение не открывается.

boundedReads.test.mts проверяет actual API/DOM navigation/контроллер окна: параметры страницы и фильтра, серверные totals/count, несколько копий документа, блокировку двойного перехода, сброс страницы и поздний ответ предыдущего чека. Старые importModal/report totals tests сохранены. Node сообщает существующее MODULE_TYPELESS_PACKAGE_JSON; предупреждение не подавлялось.

### Команды и результаты

Команды из корня проекта:

```powershell
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage07 --verbosity quiet
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage07 --verbosity quiet
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:SkipFrontendBuild=true -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/web-stage07 --verbosity quiet
node --experimental-strip-types --test tests/Frontend/*.test.mts
```

Финальный прогон 14.10.2026 после последних правок:

| Контур | Passed | Failed | Skipped | TRX в artifacts/compile-check/test-results |
|---|---:|---:|---:|---|
| Application | 476 | 0 | 0 | application-stage07/Spike_PC_2026-10-14_12_00_57_net10.0.trx |
| Infrastructure | 94 | 0 | 0 | infrastructure-stage07/Spike_PC_2026-10-14_12_01_10_net10.0.trx |
| Web | 37 | 0 | 0 | web-stage07/Spike_PC_2026-10-14_12_01_15_net10.0.trx |
| Frontend | 16 | 0 | 0 | Лог artifacts/compile-check/frontend-stage07.txt; cancelled=0 |

Web дополнительно проверяет передачу страниц/фильтров, полные серверные totals и несколько копий документа при PageSize=1 через actual PageModel без hosting.

Compile-check выполнялся отдельно для CostVision.Domain/CostVision.Application/CostVision.Infrastructure/CostVision.Web .csproj через dotnet build --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet (Web дополнительно -p:SkipFrontendBuild=true): все 0 предупреждений/ошибок. Логи artifacts/compile-check/build-{domain,application,infrastructure,web}-stage07.txt. Из CostVision.Web выполнен npm run build:ts со strict/noEmitOnError; generated output вручную не редактировался.

Проверены UTF-8 собственных исходников и документации (815 файлов): нет U+FFFD, mojibake или четырёх вопросительных знаков. Ссылки Documentation и файловые элементы CostVision.slnx разрешаются. Миграции и model snapshot сохранены без изменений.

## Обязательная оставшаяся приёмка

- Реальный SQL Server: actual DLL/provider-исполнение и материализация всех predicates/projections/include; collation фискальных строк, precision дат/decimal, текстовый поиск сумм/комментариев и названий; SQL-порядок Guid и равные значения на границах страниц.
- Число физических запросов (включая split и page-only counts), прочитанные/материализованные строки, планы и индексы при большой истории/множестве копий/адресов, стоимость коррелированных агрегатов; performance-порог не придуман и не объявлен достигнутым.
- Count/totals/page/copies при конкурентном изменении документов, связей и membership. AsSplitQuery и последовательные чтения не гарантируют общий snapshot; возможные межзапросные изменения должны быть воспроизведены.
- HTTP-/браузерная проверка экранов, обеих модалок, страниц/фильтров/пустого ответа/крайней страницы, копий, готовых totals/копеек/неизвестного состояния, привязки/отвязки, импорта и Bootstrap-стека.
- Все открытые проверки 01–06, пользовательская миграция и реальный provider/hosted/concurrency контур остаются открыты. Приложение/hosting/Docker/SQL/БД/браузер/внешняя сеть/миграции/Git не запускались.

Подготовлен [самодостаточный промпт 07A](next-stage-07a-prompt.md). Подготовка не разрешает его выполнение: 07A и 08–15 не начаты.

