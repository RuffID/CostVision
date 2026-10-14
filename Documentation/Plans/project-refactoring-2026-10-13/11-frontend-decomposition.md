# Этап 11. Разделение frontend по обязанностям

Статус: **Реализован, проверка не завершена**.
Дата начала: 14.10.2026.
Приоритет: **П2/П3**.
Зависимости: 05, 07, 10.

## Найденные проблемы

1. Точки входа отчёта чеков и магазинов имеют по 943 строки, операций — 846, дашборда — 555 на дату обзора. В них смешаны получение DOM, состояние, фильтры, cookies, пагинация, загрузка, команды и управление несколькими окнами. Проблема в смешении обязанностей, а не в самом числе строк.
2. [reports/receipts/dateRange.ts](../../../CostVision.Web/wwwroot/ts/pages/reports/receipts/dateRange.ts) и [moneyMovements/dateRange.ts](../../../CostVision.Web/wwwroot/ts/pages/moneyMovements/dateRange.ts) повторяют расчёт календарных пресетов; похожая реализация встроена в дашборд. Cookies, query page, форматирование сумм и построение пагинации также повторяются в товарах/магазинах/отчётах.
3. Перенос, отвязка, открытие и обновление чека реализованы отдельно в отчёте и окне магазинов. Общие модули карточек и модалки денежных связей уже есть, но командный поток и синхронизация состояния ещё дублируются.
4. Типы DTO повторяются по страницам; `Chart` и экземпляр графика объявлены как `any`. [dom.ts](../../../CostVision.Web/wwwroot/ts/shared/dom.ts) generic-helper проверяет существование элемента, но произвольный `as TElement` не проверяет его тип в runtime.
5. [formatters.ts](../../../CostVision.Web/wwwroot/ts/shared/formatters.ts) принимает `unknown` и подставляет ноль для отсутствующего значения; похожие fallback есть в нормализации DTO. Так обязательное число может выглядеть как реальный нулевой итог.
6. Есть локальные плотные однострочные блоки и inline-стили в построении сложных элементов; они затрудняют чтение и повторное использование. Переформатирование всего проекта не требуется.

## План работ

- Разделять каждый крупный модуль последовательно: вход/init, state, API, view model, UI, commands и контроллеры отдельных окон. Перед разделением обеспечить координацию запросов этапа 10.
- Использовать существующие shared-модули; выносить только уже повторяющиеся даты, предпочтения, форматирование и пагинацию.
- Вынести общий поток действий с чеком без зависимости модулей магазинов от точки входа отчёта.
- Согласовать общие DTO и типы подключённых библиотек по их реальному API. Не объявлять произвольные методы ради компиляции.
- Проверять обязательные числа и DOM-типы на границе. Допустимые необязательные значения нормализовать отдельно, не скрывая нарушение контракта.
- Стилевые правки делать только в затронутых функциях; структуру страницы и визуальные договорённости `Pages/AGENTS.md` сохранить.

## Проверки и приёмка

- [x] Точки входа связывают модули, не владеют всеми потоками страницы (исходники и actual entry tests).
- [x] Render-функции не выполняют HTTP, state не читается из DOM как бизнес-истина; формы читаются controller/commands, поиск магазина dashboard хранится в state.
- [x] Общие даты/пагинация/предпочтения имеют один проверенный контракт; варианты catalog/centered сохранены.
- [x] Пропавшая обязательная сумма не превращается в нулевой финансовый результат (production formatters/API и actual dashboard entry).
- [x] TypeScript strict сохранён; штатная сборка проверяет новые границы.
- [ ] Основные действия остаются AJAX, вложенные окна используют общий стек.

## Журнал выполнения

14.10.2026: пользователь разрешил изолированную реализацию только 11 при открытой приёмке 01–10/07A. Реализация разделена последовательно по dashboard, moneyMovements, stores и reports/receipts; затем проверены общие границы. Этапы 08A и 12–15 не начаты.

### Реальные владельцы

| Область | Границы после переноса |
|---|---|
| Dashboard | index запускает controller; state создаёт accounts/report owners, ui получает типизированный DOM, render отображает переданный срез state/DTO без HTTP |
| MoneyMovements | controller/init/events + session/bindings; createCommand, commentCommands, receiptLinksController, accountActionsController; importModal сохраняет строгий preview/save lifecycle |
| Stores | controller/init/events + session/bindings; preferences, namingController, storeReceiptsController, accountActionsController; render/receiptView отображают входные данные |
| Receipt report | controller/init/events + session/bindings; filtersController, listController, receiptCommands, confirmationController, accountActionsController; render создаёт cards/loading/paging |
| Shared | dateRange/preferences/paging и receipts/types/api/workflow; ReceiptList/StoreList/StoreReceiptList/ProductList используют PagedList |

Независимые поколения/AbortSignal, snapshot параметров, pagehide/debounce, same/different-ID close/reopen, pair/ID locks, captured command inputs, changed drafts, delayed hidden confirmation и unknown write outcome10 сохранены. Controller-зависимости ограничены Pick; собственного глобального store/framework нет. Shared receipt workflow получает details coordinator/Set/capture/transport/UI/reload явно от страницы, не объединяет копии/identity/accounts, не отменяет и не повторяет изменяющий POST. Init error boundaries stores/report сохранены. DOM IDs/data-actions и привязка событий сохранены по исходникам и изолированным регрессиям; реальный порядок Bootstrap-событий/анимаций ещё не принят.

Calendar сохраняет включительные дни, Monday–Sunday, leap February/year crossing и прежний default currentMonth. Cookies сохраняют decode/encode/path/max-age/samesite, query page — positive integer/default1 и прочие URL-параметры/hash. Catalog окно до пяти кнопок и report page±2 остаются разными вариантами общего helper; server count/total не пересчитываются. Обязательные formatter числа требуют typeof number + finite; missing/null/string/NaN/Infinity не становятся0, optional text отдельно. Dashboard API проверяет каждую точку incomeSum/expenseSum/label/periodStart. AvailableAccount accessRole требует0/1/2, nullable receipt account role отдельно. DOM helper проверяет HTMLElement и переданный constructor; generic без constructor подтверждает только базовый тип. Chart типизирован по используемому API поставленного Chart.js **4.5.1**, any удалён.

Backend/Domain/EF/HTTP handlers/схема/migration/designer/snapshot и финансовые/access/identity/ownership/import/refresh/retry/transaction правила в11 не менялись. SHA-256 поставки сверены с libs/README.md: EFCoreLibrary `64A6540852CFD211D48DDE6DF09F3F0A5579C6EABC90E7C26095BFE897E5AB17`, HttpClientLibrary `37B5EC623F8770CE66ADBCE0CECCB06F45368881AB07152AB5CFE94CC9C16925`; принятое решение А08 сохранено. План08A остаётся отдельным, существующие сторонние изменения его документации сохранены.

### Изолированная проверка 11

После переноса отдельных групп выполнялись build/regressions; итоговые команды после последних изменений:

```powershell
# Из CostVision.Web
npm run build:ts > ../artifacts/compile-check/test-results/build-typescript-stage11-final.txt 2>&1
# Из корня, после успешной сборки
node --experimental-strip-types --test tests/Frontend/*.test.mts > artifacts/compile-check/test-results/frontend-stage11-final.txt 2>&1
```

Сборка exit0, strict/noEmitOnError сохранены. Frontend **91 passed / 0 failed / 0 skipped / 0 cancelled**: 76 прежних регрессий10, 14 новых production boundary cases frontendDecomposition.test.mts и 1 actual dashboard entry case; validator09 не ослаблялся. Отсутствующий periodStart прежнего dashboard fixture исправлен согласно реальному DTO. MODULE_TYPELESS_PACKAGE_JSON не подавлялся. Evidence11: `artifacts/compile-check/test-results/build-typescript-stage11-final.txt`, `frontend-stage11-final.txt`; промежуточные workflow/boundaries/review результаты не выдаются за финальные. Generated dist обновлялся только build:ts. Owning/parent AGENTS, business/technical docs и навигация обновлены; [самодостаточный промпт12](prompts/next-stage-12-prompt.md) подготовлен без реализации12.

.NET исходники не менялись; неизменённые .NET-наборы не запускались. Приложение/hosting/dev-watch/browser/Docker/SQL/БД/provider/реальная сеть/deployment/EF tooling/миграции/прочие project scripts не запускались. Fake DOM/fetch/streams/session factories не подтверждают HTTP/Bootstrap/canvas/browser/SQL/provider/production. Все открытые проверки01–10/07A сохранены, применение пользовательской AddReceiptRowVersion к реальной БД не подтверждено.

### Оставшаяся приёмка (открыта, автоматически не запускать)

1. По отдельной просьбе проверить actual маршруты/auth/antiforgery09 и серверные DTO всех четырёх страниц с реальными доходами/расходами/копейками/count и неизвестными итогами. Проверить отсутствующую сумму точки/деталей: понятная ошибка без нулевого успеха и безопасное состояние прежних данных.
2. Повторить процедуру10: задержанные A-после-B success/error, независимые filters/pages/linked/candidates, hide/close/reopen того же/другого ID; stale loading/finally, pagehide/debounce/init и bfcache. После возвращения проверить фактическую возможность загрузки, не считать её доказанной node-тестом.
3. Для create/comment/name/refresh/move/delete/remove/link/unlink проверить captured copy/target/pair, double click, changed draft и same-ID editor; delayed hidden confirmation. Потерянный POST response требует перечитать факт на сервере перед ручным повтором; автоматического retry нет.
4. Импорт04/10: file/bank/account preview identity, смена каждого реквизита, preview/save lock, partial accepted clientRowId/retained rows/PDF errors, unknown outcome с ручной сверкой/новым preview. Финансовую/recovery политику не менять.
5. Пройти календарные presets на Sunday/Monday, январе и високосном феврале, включительный один день; cookies/defaults после повторного открытия и query page с прочими параметрами/hash. Проверить реальные catalog/centered кнопки на первой/средней/последней странице, count и totals полного серверного набора.
6. Проверить actual DOM IDs/runtime constructors, Chart.js4.5.1/canvas/tick/tooltip и совпадение server точек; визуальные договорённости Pages, служебный столбец, прежний event order/AJAX без reload. Первое/вложенное окно: общий backdrop между окнами, focus/click/Escape верхнего окна. Переделку stack13 не начинать.
7. Сохраняются SQL/materialization/volume/plans/concurrency/rollback01–08/07A, реальные provider/hosted retry06 и HTTP/browser09–10. AddReceiptRowVersion/RowVersion/RefreshRevision и её применение проверяет пользователь; изолированный11 не закрывает эти пункты. После получения actual evidence отметить только подтверждённые критерии.
