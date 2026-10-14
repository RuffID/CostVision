# Этап 13. Стек модалок и доступность

Статус: **Реализован, проверка не завершена**.
Дата начала: 14.10.2026.
Дата изолированной проверки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 10, 11.

## Исходные проблемы до реализации

1. [bootstrap.ts](../../../CostVision.Web/wwwroot/ts/shared/bootstrap.ts) управляет собственным backdrop поверх скрытых Bootstrap backdrop. После `hideModalBackdrop` сразу теряется ссылка `modalBackdrop`, а удаление отложено таймером. Быстрое повторное открытие/закрытие может очистить таймер удаления старого элемента через общий `modalBackdropHideTimer`; оставшийся элемент требует проверки управляемыми таймерами.
2. Обновление стека назначает `pointerEvents`, z-index и фокус, но не координирует напрямую Bootstrap focus trap нижних окон. `show`/`shown` также могут быть отменены другими обработчиками. Нужно проверить, что стек отражает фактически открытые окна и клавиатура принадлежит верхнему.
3. `scheduleStackedModalUpdate` запускает несколько отложенных обновлений без владения поколением/отменой. В интерфейсе `BootstrapModal` нет dispose; cleanup слушателей, таймеров и экземпляров не выражен как контракт.
4. [helpTooltip.ts](../../../CostVision.Web/wwwroot/ts/shared/helpTooltip.ts) держит глобальный popup и подписки; визуальные подсказки нуждаются в проверке клавиатурного открытия/закрытия и связи с описываемым элементом.
5. [_Layout.cshtml](../../../CostVision.Web/Pages/Shared/_Layout.cshtml) задаёт `lang="en"` для русского интерфейса. Формовые поля и динамические фильтры следует последовательно проверить на `id/name/label`; не считать наличие `id` достаточным для всех форм.

Уточнение исходной записи: после 12 интерфейс уже декларировал actual dispose(). В 13 проверен и изменён shared lifecycle; прежнее отсутствие декларации не выдаётся за актуальный дефект.

## План работ

- Сделать состояние backdrop и отложенного удаления явно принадлежащим конкретному элементу/поколению. Повторное открытие не должно отменять удаление чужого старого backdrop.
- Согласовать stack с реально показанными окнами и отменёнными событиями Bootstrap. Сохранить требуемое плавное затемнение и расположение между нижним и верхним окном.
- Управлять фокусом, Escape, Tab и восстановлением фокуса при закрытии верхнего окна; предусмотреть teardown без навязывания нового modal-фреймворка.
- Исправить язык документа и доступность подписей/подсказок в затронутой разметке. Учитывать правило визуального служебного столбца магазинов.

## Проверки и приёмка

- [ ] Одна модалка, вложенная модалка и быстрое закрытие/открытие не оставляют лишних backdrop.
- [ ] Отменённый `show/hide` не повреждает стек.
- [ ] Фокус, Tab, клики и Escape относятся к верхнему окну; после закрытия фокус восстановлен.
- [ ] Таймеры и подписки имеют проверенный lifecycle.
- [ ] Изолированные тесты логики и визуальная браузерная проверка записаны отдельно; таймерный тест не доказывает плавность реальной анимации.

Подтверждённая изолированная часть (общие browser-критерии выше остаются открыты):

- [x] Production helper с fake DOM/public Bootstrap events/timers проверяет backdrop ownership, prevented show/hide, rapid same/different-element reopen и deferred dispose.
- [x] Подставной DOM проверяет top Tab/focus/click/Escape, disabled/hidden/fieldset, lower/root return focus, out-of-order shown и teardown.
- [x] Production tooltip проверен на focus/click/Escape/blur, ARIA, безопасный текст, hover/popup, rerender/removal/pagehide/timers.
- [x] Прежние actual page-entry/coordinator/import/scanning проверки 10–12 сохранены; TypeScript и Razor compile-check выполнены.
- [ ] Actual browser/скринридер/animation/accessibility процедура ниже выполнена отдельно с разрешения пользователя.

## Журнал выполнения

14.10.2026: пользователь разрешил изолированную реализацию только 13 при открытой приёмке 01–12/07A/08A. Сверяются actual Bootstrap, helper, callers и тесты; дефекты реального focus trap и анимации не воспроизводились. Этапы 14–15 не начаты.

### Проверенная поверхность и владельцы

- Подключённый `_Layout` bundle и readable vendor source — Bootstrap **5.1.0**. Source Modal.show/hide проверяет defaultPrevented после всех listeners; hide игнорируется во время show transition. Native dispose не отменяет назначенные transition callbacks. FocusTrap.activate снимает document handlers прежнего trap; отсутствие собственного handler само по себе не доказывало дефект. Vendor не менялся и не исполнялся вне браузера; реальные focus/animation defects не заявляются воспроизведёнными.
- `shared/bootstrap.ts` сохраняет Bootstrap adapter. Один owner на HTMLElement хранит actual instance, публичный wrapper, phase/revision, pending hide/reopen/disposal, opener/lastFocus и subscriptions. Создание через helper задаёт public `focus=false/keyboard=false`; private `_focustrap/_config/_isTransitioning` не используются. Декларации options сверены с actual API.
- Show принимается microtask после dispatch и проверки отмены; shown подтверждает готовность, но не поднимает позднее нижнее окно. Hide не исключает элемент из stack до hidden. Wrapper hide во время show ожидает shown, wrapper show во время hide — hidden. Native/data-dismiss events тоже проходят shared listeners. Prevented hide оставляет окно/командную блокировку; dispose ждёт разрешённого закрытия, без принудительного обхода import busy.
- Microtasks проверяют owner/revision, поздняя работа прежнего instance не управляет новым. Общие 0/50-мс stack timers удалены. Hidden снимает layer state; native dispose выполняется после hidden, последний owner удаляет shared document/window handlers. Nonpersisted pagehide запрашивает dispose, bfcache сохраняет owners; cancelled hide может отложить окончательный teardown. Receipt feedback controller 12 сохраняет свою hidden/remove последовательность, изменена только связь с заголовком.
- Backdrop принадлежит отдельному owner с element/RAF/top; при смене top новый fade backdrop располагается между слоями, уходящий владеет собственным 150-мс removal. Reopen не отменяет старое удаление. Native backdrops скрываются отдельно от custom retiring элементов. Custom backdrop декоративный, клики получает actual modal. Исходные body/fixed/sticky scroll styles восстанавливаются после final hidden, locked styles удерживаются при закрытии верхнего окна.
- Stack владеет top Tab/focus/click/Escape и inert/aria-hidden нижних окон. Native FocusTrap не активируется из-за public config. Используются реальные disabled/hidden/inert/visibility/client rects и tabIndex; после close выбирается живой opener внутри нового top, remembered/первый focusable либо корневая навигация. Изолированный DOM не доказывает фактическую работу браузера/ассистивных технологий.
- `shared/helpTooltip.ts` владеет trigger/host bindings, popup/ARIA association, hover open/close timers, active resize/scroll/pointer/keyboard listeners и removal observer. Focus/hover/native button click открывают описание, Escape закрывает tooltip прежде modal, фокус остаётся у trigger. Existing aria-describedby сохраняется. Rerender, удаление host/trigger, hidden и pagehide очищают собственные ресурсы; переход pointer на popup не закрывает его немедленно. DOM создаётся без innerHTML.
- Layout `lang=ru`, выход имеет русское доступное имя; заголовки модалок Receipts/Stores/MoneyMovements и feedback связаны через aria-labelledby. Динамический редактор комментария имеет aria-label при прежних id/name. Static audit проверяет уникальность id, modal title references, id/name и label targets четырёх actual страниц. Первый служебный столбец Stores не изменён.

### Фактические проверки

Команды из CostVision.Web и затем корня соответственно:

```powershell
npm run build:ts > ../artifacts/compile-check/test-results/build-typescript-stage13-final.txt 2>&1
node --experimental-strip-types --test tests/Frontend/*.test.mts > artifacts/compile-check/test-results/frontend-stage13-final.txt 2>&1
dotnet build CostVision.Web/CostVision.Web.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true > artifacts/compile-check/test-results/build-web-stage13-final.txt 2>&1
```

Финальные результаты: **TypeScript exit0** со strict/noEmitOnError; **137 passed / 0 failed / 0 skipped / 0 cancelled** — 124 прежних + 13 новых modal/tooltip/markup cases. Web/Razor compile-check **0 errors / 2 existing NU1510 warnings** Infrastructure DI/logging package pruning. .NET-наборы и IntegrationTests не запускались. Generated dist обновлён только build:ts. Node MODULE_TYPELESS_PACKAGE_JSON не подавлялся; diagnostic log проверки отсутствующего receipt user — ожидаемая отрицательная регрессия 12.

Первый переходный прогон дал 93 passed/31 failed: прежние fake DOM/Bootstrap не объявляли используемые public getOrCreateInstance/removeAttribute/MutationObserver. Исправлена поверхность фикстур, assertions/validators/strict не ослаблены. Затем прежние124 прошли; отдельные новые helper cases прошли, финальный полный результат указан выше. Fake Bootstrap моделирует public cancelable events/transitions, не подменяет production stack/controller и не доказывает vendor rendering/focus.

DLL SHA-256 сверены с libs/README.md: base A2D5C72E7B30EC8886D70AFBD9B30F1A424EAD4402F37C5174A58601AD77A11F; Maintenance 24B9B4DF25E519833A25C12DDD036EA9B397AA4DE2E6202B2B5D2DC66B6F3CDB; SqlServer 76C83AAAAB8522F598605A3D5E55976E71C6E84ACC51B0CA513E260E9C2C2E4B; HTTP 37B5EC623F8770CE66ADBCE0CECCB06F45368881AB07152AB5CFE94CC9C16925. Поставка и 08A не изменены. Финансовые/access/identity/ownership/import/manual-link/refresh/retry/transactions/schema/query/HTTP/coordinator/scanning/storage контракты сохранены.

Финальная статическая проверка 33 затронутых source/tests/docs/AGENTS/slnx: строгий UTF-8, U+FFFD/четыре вопросительных знака/проверенные признаки mojibake — 0 проблем; Markdown links и File/Project paths solution — 0 отсутствующих целей. BOM/существующие окончания строк сохранены; у переработанного bootstrap.ts восстановлен исходный CRLF без BOM. Разрешённые git status --short / git diff --stat / git diff --name-only показали 31 modified + 2 untracked (новый helper test и prompt14); иных незакоммиченных08A/посторонних файлов нет. Stage/commit/push не выполнялись. Evidence: artifacts/compile-check/test-results/static-stage13-final.json.

### Открытая actual browser процедура (не запускалась)

1. По отдельной просьбе проверить desktop/mobile actual страницы: отчёт, Stores, MoneyMovements (включая data-bs кнопки import/create), Products/Settings и feedback12. Для первой/вложенной модалки проверить плавный fade страницы/нижнего окна, z-index/backdrop между слоями, отсутствие native flash, сохранение scroll/позиции и отсутствие скачка fixed/sticky элементов. Учитывать reduced motion и различную длительность переходов.
2. Во время show/hide быстро закрыть/открыть тот же и другой элемент, повторить цепочку из трёх окон. Отменить show/hide поздним listener, закрыть во время show, запросить dispose во время переходов. После окончания не должно быть orphan backdrop/таймеров/listeners/живого disposed callback. Проверить native dismiss/data API, ранний click до завершения page init, delayed confirmation/revision10 и feedback disposal12. Не считать порядок fake событий браузерной гарантией.
3. Клавиатурой пройти Tab/Shift+Tab, disabled/fieldset/hidden/пустой dialog, программный/мышиный уход в нижний слой и корневую страницу. Focus/click/Escape принадлежат top; первый Escape у открытой подсказки закрывает описание. После close вернуть фокус на живой opener в lower/root; проверить удалённый/disabled opener, отсутствие lower focus loop и достижимость root navigation после final close.
4. В скринридере проверить lang=ru, названия каждого dialog/редактора, объявление tooltip по focus/Enter/Space, Escape/blur/outside click и сохранение других descriptions. Проверить hover переход в popup, resize/scroll, rerender/removal, modal hidden, pagehide/bfcache и повторную работу после возврата. Проверить hidden/inert поддержку целевых браузеров.
5. Повторить import preview/save blocked hide/double click/partial retained rows/PDF errors, captured command IDs и delayed confirmations10, feedback12 без автоматического retry POST. Проверки не меняют финансовую/recovery/storage политику. Все SQL/concurrency/migration/provider/maintenance/HTTP/auth/camera/CSP/storage/production проверки 01–12/07A/08A открыты.

App/hosting/browser/camera/device/Docker/SQL/БД/provider/network/deployment/EF tooling/миграции не запускались. Owning/parent AGENTS, business/technical docs, навигация и slnx актуализированы. Подготовлен [самодостаточный промпт 14](prompts/next-stage-14-prompt.md); подготовка не начинает 14 или 15. До actual browser приёмки статус 13 — «Реализован, проверка не завершена».
