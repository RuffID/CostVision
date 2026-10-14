# Этап 08. Границы Application и сохранения

Статус: **Реализован, проверка не завершена**.
Дата начала: 14.10.2026.
Дата изолированной проверки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 01–07, 07A.

## Найденные проблемы

1. До 07A глобальный IUnitOfWork объединял 11 репозиториев. В [07A](07a-scenario-unit-of-work.md) он удалён: введены 14 узких adapters, прямые зависимости чтения и единый технический scope. Перед началом 08 сверить [карту потребителей](07a-consumer-map.md) и результаты; обязательная SQL-приёмка 07A открыта.
2. Application зависит от EF Core/Relational и `EFCoreLibrary.dll`. [AppDbContextBase](../../../CostVision.Application/Abstractions/DataBase/AppDbContextBase.cs) наследует `DbContext`; интерфейсы репозиториев включают context type, `IQueryable`, `include` и tracking-параметры. Это фактическая граница проекта, а не случайный отдельный `using`.
3. Domain использует `IEntity` из внешней DLL. Её независимость от EF нельзя утверждать без проверки контракта DLL. Источник, версия и совместимость двух DLL из `libs` не описаны рядом с поставкой.
4. До 08 MoneyMovementsPageUseCase объединял 14 use case, преимущественно переадресовывая вызовы страницы. PreviewBankStatementImportPageRequest также именовался по web-экрану внутри Application. Фасад/интерфейс/регистрация удалены; [MoneyMovementsModel](../../../CostVision.Web/Pages/MoneyMovements.cshtml.cs) вызывает конкретные сценарии, нейтральный request принадлежит preview.
5. После 07A прежняя перегрузка [UnitOfWorkScope](../../../CostVision.Infrastructure/DataBase/Repositories/UnitOfWorkScope.cs) сохраняет в конце транзакции автоматически, а перегрузка с изоляцией требует явного сохранения сценария. После отказа явной транзакции сессия запрещает повторную запись; rollback не восстанавливает change tracker. 07A сохранил эту семантику и общий fail-fast через все adapters, включая обычную перегрузку. Изменения границ EF в 08 также не должны терять промежуточные ID и гарантии отказа.

## План работ

- Использовать результат 07A: узкие сценарные UoW, минимальные зависимости чтения и единую scoped-сессию. Не повторять их разделение в этом этапе.
- Явно описать владельца транзакции, промежуточного сохранения и поведения после ошибки. Не переиспользовать повреждённую сессию в следующем сценарии.
- Упростить фасад страницы по реальной пользе: оставить координацию сценария, убрать механический набор прокси либо разделить его по обязанностям.
- Решение о полном удалении EF из Application принять отдельно до массового изменения контрактов. Действующий `AGENTS.md` разрешает композицию `predicate/include` вызывающим кодом и запрещает специализированные query-методы: не нарушать это правило под видом следования общему архитектурному скиллу.
- Проверить принятую DLL и её документацию перед проектированием adapter-контрактов. Не копировать её persistence, не придумывать отсутствующие методы и не менять library API незаметно.
- При изменении устойчивых границ обновить владеющий `AGENTS.md` после проверки, а не объявлять новую архитектуру уже существующей.

## Проверки и приёмка

- [x] Read-only сценарии имеют минимальные зависимости; writers сохраняют явную границу 07A. Сценарии 08 не расширяют UoW.
- [x] Actual AddApplication/AddInfrastructure разрешают новую страницу и сценарии; прежние DI/tracker/scopes-регрессии проходят без хоста/БД.
- [x] Сохранены auto/explicit Save, промежуточный ID, shared failure/new scope и запрет nested/competing/swallowed failure; actual scope/DLL с подставным connection/Save, не SQL-приёмка.
- [x] Новых специализированных query-методов, SQL, production reflection, persistence-копий, runner или второго контекста нет.
- [x] Пользователь принял вариант А: EF/DLL/query-граница сохраняется явно; Stage08BoundaryTests проверяет фактическое правило. Вариант Б и полное удаление EF не приняты.
- [ ] Реальная SQL/provider/HTTP/browser/hosted/production-приёмка и открытые проверки 01–07A. Изолированные результаты выше её не закрывают.

## Журнал выполнения

14.10.2026 пользователь разрешил только 08 при открытой приёмке 01–07A. До реализации новой persistence-границы подготовлено [конкретное предложение](08-boundary-decision.md), пользователь отдельно выбрал «А: сохранить текущую явную EF/DLL-границу в 08». Действующий predicate/include/projection-контракт 07 сохранён; полное удаление EF/IQueryable/context/include и вариант Б не выполнялись. Общий backend-скилл не отменяет решение проекта. Исторически 13.10.2026 разделение UoW вынесено в 07A; подготовка промпта 08 после него не означала начало реализации.

### Изменения и карта границ

- Application/Domain/csproj, AppDbContextBase, тонкие DLL-репозитории и projection adapter остаются в принятой структуре. Domain IEntity не содержит DbContext, но вся EFCoreLibrary имеет EF-ссылку. [Описание поставки](../../../libs/README.md) фиксирует источник в дереве, версии/хеши, actual API/metadata и неизвестные upstream/лицензию/source commit.
- Page-фасад с 14 прокси удалён вместе с интерфейсом и регистрацией. PageModel теперь явно получает 14 конкретных сценариев; это удаление лишнего слоя, а не сокращение числа обязанностей HTTP-страницы. Она передаёт request/user/token и мапит результат; бизнес-координаторы и UoW не перенесены в Web. Все 14 handlers/payload/binding/paging/totals/status сохранены.
- PreviewBankStatementImportRequest заменяет page-имя; ExecuteAsync принимает request/userId/ct. Прежняя проверка отсутствующего/пустого файла перенесена в preview до registry/storage/extraction. Для валидного файла порядок parser/формат/доступ/PDF/дубли сохранён. Application не освобождает stream; Web освобождает его через await using при результате или исключении. Это не новая общая проверка null/binding из 09.
- [Карта владельцев 07A](07a-consumer-map.md) сохраняется: read-only получает прямые repositories, один scoped UnitOfWorkScope владеет actual DLL context/transaction/failure. Обычная перегрузка auto Save после action; IsolationLevel-перегрузка только explicit Save. Создание операции сохраняет ID до matcher, затем связь при наличии; refresh provider до короткой transaction/одного Save, retry-state после disposal в новом scope. Репозитории/PageModel/preview не становятся дополнительным владельцем транзакции.
- Domain/EF-модель/схема/migration/designer/snapshot, access/identity/авторство/ручная множественность/финансовые формулы/импорт/refresh/retry/изоляция и frontend не менялись. Owning/parent AGENTS, technical guide, навигация и solution items актуализированы. Публичные бизнес-правила не менялись, их описания сохранены.

### Изолированные проверки

Stage08PreviewBoundaryTests: 7 cases — file guard до I/O, actual AccountId/UserId predicates на доменных memberships, Owner/Editor/Viewer/no-access, результат с line errors, token/stream и отсутствие закрытия caller-owned stream. Прежние preview-тесты и культурный ключ 04 адаптированы к request без ослабления ожиданий. Новых double-данных вместо финансового результата нет.

MoneyMovementsModelTests проверяет все handlers с strict конкретными dependencies, full totals/страницы/копии, missing/empty multipart request, disposal stream после результата/исключения, прежние 400/409/envelopes и actual layer DI с ValidateScopes/ValidateOnBuild без hosting/SQL. Stage08BoundaryTests (2 cases) закрепляет принятое правило А и actual metadata IEntity/DbContext/CRUD/projection/HTTP Post/token. Reflection только в тесте. Для inspection HTTP DLL тестовому csproj добавлена прямая ссылка на принятую поставку, production references не менялись.

Полные изолированные наборы включают регрессии 01–07 и все 19 Stage07aUnitOfWorkTests: tracker/independent scopes, auto/explicit Save/ID, общий отказ, fresh scope, обе transaction overloads, nested/competing/swallowed failure. Fake connection/Save/синтетические SqlException не доказывают real SQL/fixup/rollback/constraints или гонки.

| Контур | Passed | Failed | Skipped | Финальный TRX в artifacts/compile-check/test-results |
|---|---:|---:|---:|---|
| Application | 483 | 0 | 0 | application-stage08/Spike_PC_2026-10-14_16_57_36_net10.0.trx |
| Infrastructure | 115 | 0 | 0 | infrastructure-stage08-final/Spike_PC_2026-10-14_16_57_42_net10.0.trx |
| Web | 43 | 0 | 0 | web-stage08/Spike_PC_2026-10-14_16_57_47_net10.0.trx |

Конкретные Domain/Application/Infrastructure/Web и только compile-check IntegrationTests: **0 warnings/0 errors**. Промежуточные compile-ошибки тестовой адаптации (прямая ссылка HTTP DLL, Id DTO вместо BankId) и nullable warnings metadata-output исправлены; финальный Infrastructure запуск без предупреждений. Production assertions/strict не ослаблялись.

Команды из корня, без restore/solution/hosting:

```powershell
dotnet build CostVision.Domain/CostVision.Domain.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet
dotnet build CostVision.Application/CostVision.Application.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet
dotnet build CostVision.Infrastructure/CostVision.Infrastructure.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet
dotnet build CostVision.Web/CostVision.Web.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet
dotnet build tests/CostVision.Infrastructure.IntegrationTests/CostVision.Infrastructure.IntegrationTests.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage08 --verbosity quiet
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage08-final --verbosity quiet
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --logger trx --results-directory artifacts/compile-check/test-results/web-stage08 --verbosity quiet
```

Frontend/TypeScript не менялись; npm/node в 08 не запускались. Старые 16 frontend cases 07A не выдаются за новые результаты. Реальные приложение/hosting/Docker/SQL/БД/browser/provider/внешняя сеть/production, миграции и Git не запускались.

Все три набора повторно прошли после финальной проверки оформления AAA/using и корректной owner-membership тестового графа; финальные TRX указаны выше, предупреждений при финальном compile/test нет. 41 существующий/новый изменённый файл проверен строгим UTF-8: U+FFFD, последовательностей из четырёх вопросительных знаков и проверенных признаков mojibake нет. Локальные Markdown-ссылки и File/Project-пути CostVision.slnx ведут на существующие файлы. Ручные правки выполнены через apply_patch с сохранением исходных BOM/окончаний строк; generated-файлы не правились.

### Оставшаяся приёмка

Сохранены все открытые проверки 01–07A: actual predicates/includes/projections/materialization/DTO, membership revoke, collation/precision/текст/порядок Guid/объём и планы; реальные constraints/старые дубли/несовместимые связи; конкуренция ручного/QR/перенос чеков одного/разных авторов; import/import/import-transfer, create-auto/auto-auto разных счетов/manual-link-auto/transfer-link; refresh/общий каталог/RowVersion/rollback/1205/fixup/повтор в новом scope. Несколько read/count/totals/page/split не гарантируют snapshot. Metadata, mocks и обычная transaction не заменяют два SQL-соединения.

Обязательная пользовательская AddReceiptRowVersion остаётся **RowVersion rowversion + RefreshRevision bigint NOT NULL default 0**; агент migration/designer/snapshot не создаёт и не меняет. Прежде чем принимать SQL-результат, нужно проверить реально применённую схему и существующие данные. Provider HTTP/timeout/identity/hosted lifecycle/перезапуск, HTTP/auth/antiforgery и browser-экраны/importModal/Conflict/Bootstrap/копейки/неизвестные итоги остаются открытыми.

Для 08 отдельно открыты реальная HTTP/multipart приёмка новой прямой зависимости страницы и compatibility принятой DLL с фактическим provider. Полное удаление EF не является непройденной реализацией принятого А: оно не принято. Статус08 — «Реализован, проверка не завершена» по изолированному результату. Подготовлен [самодостаточный промпт09](next-stage-09-prompt.md); **09–15 не начаты**.
