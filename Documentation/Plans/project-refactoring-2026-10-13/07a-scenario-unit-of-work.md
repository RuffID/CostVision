# Этап 07A. Разделение Unit of Work по областям и сценариям

Статус: **Реализован, проверка не завершена**.
Приоритет: **П2**.
Зависимости: 01–07.
Дата добавления в план: 13.10.2026.

## Основание и цель

До 07A IUnitOfWork объединял 11 репозиториев: User, Role, Account, Receipt, ReceiptItem, Store, Product, ReceiptAccount, AccountMember, MoneyMovement и MoneyMovementReceipt. Каждый потребитель получал доступ ко всем областям, даже когда читал один объект. UnitOfWork одновременно агрегировал репозитории и управлял сохранением, транзакциями и состоянием после отказа. Оба глобальных типа удалены; текущий технический владелец — [IUnitOfWorkScope](../../../CostVision.Application/Abstractions/DataBase/Repositories/IUnitOfWorkScope.cs) / [UnitOfWorkScope](../../../CostVision.Infrastructure/DataBase/Repositories/UnitOfWorkScope.cs). [Карта потребителей](07a-consumer-map.md) обосновывает новые зависимости.

Выделить несколько узких контрактов `...UnitOfWork`, каждый из которых отвечает за определённую область и связанные сценарии записи. Не заменять один общий объект несколькими столь же широкими агрегатами: набор репозиториев обосновать фактическими потребителями и границей атомарной операции. Работа была частью этапа 08; теперь выполняется отдельным этапом перед ним.

## План работ

1. Составить карту всех потребителей `IUnitOfWork`: use case, application helpers, фоновые сценарии, DI и тестовые фикстуры. Для каждого указать используемые репозитории, проверки доступа, чтение/запись, владельца транзакции, промежуточные сохранения и необходимые ID. Зафиксировать целевые контракты до переноса потребителей.
2. Проверить следующие области как кандидаты: управление пользователями и ролями; управление счетами и участниками; создание и размещение чеков; обновление чеков и каталогов; импорт банковских операций; управление операциями и сверка с чеками. Это предварительные области, а не утверждённые имена интерфейсов. Разделить их дополнительно, если наборы зависимостей и транзакционные границы различаются. Межобластной сценарий получает только необходимые ему зависимости и одного владельца транзакции.
3. Выделить общий технический контракт, например `IUnitOfWorkScope`, для сохранения и выполнения транзакции без свойств репозиториев. Реализация в Infrastructure владеет общей сессией хранения и состоянием отказа. Все сценарные UoW и репозитории одного DI scope используют один контекст и change tracker; обёртки не создают отдельные контексты и не хранят независимые флаги отказа.
4. Перевести read-only сценарии на минимальные repository/query-зависимости без обязательного UoW. Сохранить базовые методы репозиториев, predicates и include на стороне Application. Не добавлять специализированные выборки, скрытый SQL или доступ к произвольным репозиториям через service locator.
5. Перевести записывающие сценарии и helpers на целевые `...UnitOfWork`, сохраняя проверки доступа и координацию в Application. Обновить Infrastructure и производственную DI-регистрацию. Внутри составного сценария не запускать конкурирующие или вложенные транзакции нескольких обёрток.
6. Переносить потребителей небольшими группами с регрессиями. Переходный адаптер допустим только на время переноса; итог этапа — удалённый глобальный контракт с 11 репозиториями и его регистрация, без постоянного фасада, возвращающего всю прежнюю поверхность.
7. После проверки актуализировать локальные и родительские AGENTS.md, техническую документацию и описание владельца транзакций. Не описывать запланированную структуру как реализованную.

## Контракт сохранения и отказов

Перед переносом сверить фактическую реализацию после этапов 01–07. На момент добавления этого этапа действуют следующие правила этапа 03:

- Прежняя перегрузка `ExecuteInTransaction(action, ct)` сохраняет после action автоматически. Перегрузка с `IsolationLevel` выполняет action и commit без неявного сохранения; сохранение вызывает сценарий. Сохранить эти различия и необходимые промежуточные сохранения для получения ID.
- Создание чека вручную, QR-пакет и перенос используют явную `Serializable`. Разделение UoW не должно ослаблять изоляцию, правила владения и проверку дубля в счёте.
- Только известные нарушения уникальности SQL Server 2601/2627 для индекса авторской копии чека и `PK_ReceiptAccounts` преобразуются в `ReceiptWriteConflictException`; ошибка 1205 также даёт явный конфликт. Для жертвы deadlock не выполняется повторный rollback, скрывающий исходный отказ. Прочие ошибки не становятся дубликатами.
- После отказа явной транзакции сессия запрещает дальнейшее сохранение и транзакционные сценарии; rollback не очищает change tracker. Это состояние должно распространяться на все UoW общей сессии. Для повторной попытки нужен новый scope.
- Application восстанавливает добавленные или перенесённые доменные связи при отказе. Сохранить результат каждой строки пакета и отсутствие записи при отказе проверки доступа.

Изолированная проверка этих контрактов не закрывает SQL-приёмку предыдущих этапов и не доказывает защиту от конкурентной записи.

## Границы этапа

- Изменение структуры зависимостей не меняет бизнес-правила, финансовые расчёты, HTTP-контракты, схему БД или алгоритмы импорта и обновления чеков.
- Полное удаление EF Core из Application, adapter-контракты DLL и упрощение фасада страницы остаются в [этапе 08](08-application-and-persistence-boundaries.md).
- Потребность в иной возможности библиотеки или модели фиксировать явно, не подменять копированием persistence или фиктивной реализацией. Если потребуется схема, изменение согласовать отдельно; миграции создаёт пользователь.
- Этот документ не разрешает реализацию. Открытые проверки этапов 01–07 сохраняются; начало и приёмка 07A не закрывают их автоматически.

## Проверки и приёмка

- [x] Карта потребителей обосновывает каждый целевой контракт; глобальный UoW удалён, новые контракты не дают доступ ко всем областям.
- [x] Read-only потребители имеют минимальные зависимости; доступ и координация остаются в Application, базовые методы репозиториев сохранены.
- [x] Изолированный тест использует фактическую производственную DI-регистрацию: UoW и репозитории разделяют контекст внутри scope, разные scope получают разные сессии. Hosting и подключение к БД для этого не нужны.
- [x] Тесты проверяют владельца транзакции, изоляцию, число и порядок сохранений, промежуточные ID, передачу отмены и распространение состояния отказа через разные UoW одной сессии. После отказа нет нежелательной последующей записи.
- [x] Регрессии проверяют реальные predicates, результаты строк, создаваемые чеки/связи, права доступа и финансовые результаты, а не только число вызовов mocks. Пройдены относящиеся изолированные тесты и регрессии предыдущих этапов.
- [x] Выполнен compile-check конкретных затронутых `.csproj` в `artifacts/compile-check`, по возможности с `--no-restore` и отключёнными frontend-targets. Зафиксированы команды, дата, passed/failed/skipped и ограничения.
- [ ] С отдельным разрешением на SQL Server проверены атомарность межобластных операций, rollback, ограничения и конкурентная запись чеков одного автора и разных авторов в общий счёт. Обычная транзакция, процессный lock, mocks и общая сессия сами по себе гонку не исключают.
- [x] Документация и AGENTS.md отражают проверенную структуру. До обязательной SQL-приёмки статус не выше «Реализован, проверка не завершена».

## Журнал выполнения

Дата начала реализации: 14.10.2026. Пользователь разрешил только 07A при открытых проверках 01–07. Начата карта фактических потребителей; этапы 08–15 не начаты. Исторически 13.10.2026 разделение UoW выделено из 08 в самостоятельный этап, тогда выполнялась только подготовка плана.


### Изолированная реализация и проверки 14.10.2026

- До переноса составлена карта 53 Application-потребителей, helpers, background scopes, DI и фикстур. Глобальные IUnitOfWork/UnitOfWork и их регистрация удалены; добавлены технический scope и 14 сценарных adapters по 2–4 репозитория. Read-only получает прямые минимальные зависимости; одиночные записи — repository + scope.
- Общие predicate/include/projection-контракты 07, access, identity, финансовые формулы, алгоритмы/pending/порядок импорта, сверки и refresh сохранены. Application ещё зависит от EF/DLL; MoneyMovementsPageUseCase не упрощался. 08–15 не начаты.
- Один scope владеет actual DLL context, transaction и общим отказом. Begin/action/Save/commit/отмена блокируют дальнейшие Save и обе перегрузки через все adapters; nested/competing transaction запрещена до второго Begin, перехваченный отказ Save не допускает commit. Обычная перегрузка сохраняет после action, явная — только по вызову сценария. Промежуточный ID/второе Save связи и короткий refresh сохранены.
- Stage07aUnitOfWorkTests добавляет 19 изолированных cases с actual AddApplication/AddInfrastructure/DLL: tracker/independent scopes, owner/isolation/token, auto/explicit Save, промежуточный ID через разные adapters, распространение отказа/new scope, nested/competing и swallowed failure. Fake connection/Save только наблюдают протокол; generated ID имитируется в тесте и не подтверждает SQL.
- Фикстуры и linked TestUseCaseFactory/ITestStorage перенесены на actual constructor-зависимости; полный тестовый набор репозиториев не является production API и не регистрируется в DI. IntegrationTests адаптирован и только скомпилирован; тест каталожного unique-конфликта ожидает уже утверждённый ReceiptWriteConflictException с исходной DbUpdateException/SqlException.
- Владельческие/родительские AGENTS.md, техническое руководство, карта транзакций, навигация и solution items обновлены. Бизнес-/HTTP-/UI-контракт не менялся.

Команды выполнялись из корня, кроме npm — из CostVision.Web. Конкретные проекты, без solution, restore и hosting:

```powershell
dotnet build CostVision.Domain/CostVision.Domain.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet
dotnet build CostVision.Application/CostVision.Application.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet
dotnet build CostVision.Infrastructure/CostVision.Infrastructure.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --verbosity quiet
dotnet build CostVision.Web/CostVision.Web.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet
dotnet build tests/CostVision.Infrastructure.IntegrationTests/CostVision.Infrastructure.IntegrationTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage07a --verbosity quiet
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage07a-final --verbosity quiet
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --logger trx --results-directory artifacts/compile-check/test-results/web-stage07a --verbosity quiet
npm run build:ts
node --experimental-strip-types --test tests/Frontend/*.test.mts
```

Итоговые результаты: Application **476 passed / 0 failed / 0 skipped**, Infrastructure **113/0/0**, Web **37/0/0**, frontend **16/0/0** (cancelled 0). Domain/Application/Infrastructure/Web и IntegrationTests compile-check — **0 warnings / 0 errors**; TypeScript strict/noEmitOnError успешен. Существующее MODULE_TYPELESS_PACKAGE_JSON не подавлялось.

Evidence в artifacts/compile-check:

- test-results/application-stage07a/Spike_PC_2026-10-14_15_55_34_net10.0.trx;
- test-results/infrastructure-stage07a-final/Spike_PC_2026-10-14_16_01_03_net10.0.trx;
- test-results/web-stage07a/Spike_PC_2026-10-14_16_01_37_net10.0.trx;
- test-results/frontend-stage07a.txt и test-results/build-typescript-stage07a.txt;
- build-costvision.domain-stage07a.txt, build-costvision.application-stage07a.txt, build-costvision.infrastructure-stage07a.txt, build-costvision.web-stage07a.txt, build-integration-stage07a.txt.

Промежуточные отказы адаптации тестов исправлены: явные strict-зависимости, допустимый FiscalSign и тестовый ReceiptId; бизнес-assertions не ослаблялись. Итоговые результаты выше относятся к исправленному коду.

UTF-8/BOM/окончания строк сверены со снимками исходников; U+FFFD и последовательностей из четырёх вопросительных знаков не обнаружено. Локальные ссылки обновлённых документов и пути CostVision.slnx проверены.

**Не выполнено:** SQL/реальная БД/конкурентность/constraints/rollback-fixup, HTTP/provider/hosted lifecycle, браузер, Docker и production. Изолированные результаты не закрывают проверки 01–07 либо обязательную SQL-приёмку 07A. Общая сессия не даёт snapshot нескольких чтений. Требуется пользовательская AddReceiptRowVersion с **RowVersion rowversion и RefreshRevision bigint NOT NULL default 0**; migration/designer/snapshot/модель/схема в 07A не изменялись. Старые дубли не удалялись. Подготовлен [самодостаточный промпт 08](prompts/next-stage-08-prompt.md); его подготовка не разрешает начало этапа.
