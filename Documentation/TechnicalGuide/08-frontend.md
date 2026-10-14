# Клиентский интерфейс

## Структура и сборка

Исходники находятся в `CostVision.Web/wwwroot/ts`. Каталог `pages` разделён по страницам, `shared` содержит общие компоненты, `types` — объявления глобальных внешних API.

[tsconfig.json](../../CostVision.Web/tsconfig.json) задаёт ES2020, ES-модули, DOM, strict и noEmitOnError. Результат компиляции и карты исходников попадают в `wwwroot/dist`. Generated-файлы не являются местом ручной разработки.

[package.json](../../CostVision.Web/package.json) содержит `build:ts` и `watch:ts`. MSBuild Web-проекта перед Build запускает `npm ci`, если compiler отсутствует, затем `npm run build:ts`. Свойство `SkipFrontendBuild=true` пропускает этот шаг. Оно допустимо только когда требуемые ассеты уже подготовлены. Автоматическая сборка документации эти команды не запускает.

## Ответственность модулей

### Сторонние библиотеки

Готовые браузерные библиотеки хранятся в `wwwroot/lib`, каждая в отдельной папке. Razor подключает их перед модулем TypeScript, который использует их глобальный API. Они не генерируются нашей TS-сборкой.

| Библиотека | Файл | Назначение |
|---|---|---|
| Chart.js | `lib/chartjs/chart.umd.min.js` | График доходов и расходов на Index |
| html5-qrcode | `lib/html5-qrcode/html5-qrcode.min.js` | Сканирование QR камерой и из файлов на AddReceipt |
| exif-js | `lib/exif-js/exif-js.js` | Чтение даты фотографии |
| jsQR | `lib/jsqr/jsQR.js` | Распознавание QR в изображениях |

Объявления в `ts/types/globals.d.ts` описывают их API для TypeScript, но не загружают библиотеки. При переносе библиотеки нужно обновить Razor-подключения; при обновлении версии — проверить используемый API. Собственные исходники остаются в `ts`, результат сборки — в `dist`.

### Модули страниц

| Модуль страницы | Поток |
|---|---|
| `dashboard` | Период, счета, магазины, источники доходов/расходов и график |
| `moneyMovements` | Фильтры операций, таблица, действия, PDF-импорт и сверка |
| `receipt` | Камера, файлы, декодирование QR, черновик и ручная форма |
| `reports/receipts` | Отчёт по чекам, фильтры счетов, карточки и модальные действия |
| `products`, `stores` | Каталоги, таблицы, названия и связанные покупки |
| `settings/users` | Пользователи и роли |
| `settings/userSettings` | Счета и участники |

В этапе 11 entry четырёх страниц dashboard/stores/reports/receipts/moneyMovements оставлены точками запуска. Controller связывает init/events и независимые сценарии; фабрика state создаёт собственную page session, bindings/ui хранит DOM/modal refs, render отображает входные данные без HTTP. Отдельные контроллеры имени, списка, деталей, подтверждения и account actions получают ограниченные Pick-зависимости. Координаторы, snapshots и locks этапа 10 остаются у прежних UI-областей; глобального store нет.

Повторённый календарь вынесен в shared/dateRange с включительными днями, неделей понедельник–воскресенье и прежним default currentMonth. shared/preferences сохраняет cookies/query page; shared/paging сохраняет различные окна кнопок catalog и centered, server count/flags/total не пересчитываются. shared/receipts содержит единственный ReceiptDto/items/accounts, транспорт open/refresh/move/remove/delete и общий workflow отчёта/магазинов. Страница передаёт конкретный ID копии, details coordinator, locks, capture контекста и callbacks UI/reload; workflow не расширяет права и не повторяет POST.

Обязательные числа в formatters требуют конечный number, пропавшее значение не становится нулём. Dashboard API дополнительно проверяет суммы/label/periodStart каждой точки. Отсутствующая accessRole доступного счёта не становится Owner; nullable роль связи чека обрабатывается отдельно. requireElementById проверяет HTMLElement и явно переданный constructor: переносимые кнопки/select/form/table section/canvas проверяются runtime. Generic без constructor подтверждает только базовый HTMLElement. Декларации Chart ограничены используемым API поставленного Chart.js 4.5.1, без any; реальный canvas/Chart ещё не принят в браузере.

## HTTP и отображение

[http.ts](../../CostVision.Web/wwwroot/ts/shared/http.ts) отправляет fetch, сериализует объекты в JSON и оставляет FormData/Blob в исходном формате. Необязательный пятый AbortSignal передаётся fetch; прежние четыре аргумента совместимы. По умолчанию Accept=application/json. JSON/multipart и RequestVerificationToken сохранены.

HttpError остаётся Error для существующего UI, содержит kind (http/contract/network/aborted), status и url. Неуспешный HTTP сохраняет сообщение только корректного failure envelope; HTML, proxy text и служебная диагностика заменяются общим понятным сообщением. Старый HTML-редирект логина отклоняется с сообщением сессии. Пустой JSON, неверный success/message и отсутствующий/null data отклоняются; false как полезные данные и корректные пустые массивы допустимы. Metadata ответа сохраняется для ошибок unwrap. Локальная ручная проверка unwrap без HTTP-ответа имеет status=null/url="".

API-потребители проверяют обязательные списки, поля результатов, страницы/флаги/count и конечные числовые серверные totals через validator в unwrapServiceResult. Детали/refresh проверяют обязательные id/dateTime/operationType/totalSum/items/accounts; импорт — rows/errors либо счётчики/errors; ручной чек — допустимый outcome/receipt/message. Это проверка необходимой формы, не полная runtime-schema всех вложенных DTO. Ошибка не превращается в фиктивный ноль или пустой успех; формулы/итоги на клиент не перенесены.

Динамические элементы создаются DOM API. Общие formatters, alerts и индикаторы загрузки помогают поддерживать одинаковое отображение сумм, дат и ошибок. Основные действия выполняются без полной перезагрузки.

AbortSignal в helper дополняет проверку актуальности. [requestCoordinator.ts](../../CostVision.Web/wwwroot/ts/shared/requestCoordinator.ts) выдаёт ticket с parameters/signal/isCurrent: begin инвалидирует прежнее поколение одной области и отменяет её чтение; capture фиксирует контекст команды. Снимки параметров создаёт вызывающий контроллер. Ответ, ошибка и loading принимаются только актуальным поколением; намеренная отмена и старый отказ не становятся новой ошибкой UI. Даже fetch, игнорирующий abort, должен пройти isCurrent.

| Область | Независимые чтения и контексты |
|---|---|
| Dashboard | Начальные счета и отчёт со снимком дат/источника/счетов/магазинов |
| Products | Счета, основной список, покупки товара; контекст имени |
| Stores | Счета, список, чеки магазина, детали; контексты магазина/имени/account action |
| Reports/Receipts | Счета, список со снимком query, детали; confirmation и отдельное окно денежных связей |
| MoneyMovements | Начальные accounts/banks, основной список, linked/candidates/details с ревизией окна |
| Import | Preview со снимком File object/bankId/accountId и контекст подтверждённой записи |

Hide, открытие другого/того же объекта, смена относящихся к области фильтров и pagehide инвалидируют чтения. Pending debounce очищается при pagehide; старая начальная загрузка не запускает следующий запрос. Shared paging меняет revision при clear, поэтому old catch/finally не меняет новую навигацию. Delayed hidden подтверждения проверяет захваченный контекст перед восстановлением нижнего окна; Общий Bootstrap stack 13 сохраняет эти caller revisions/locks.

Команды захватывают ID/входные значения до await; повторный POST одного объекта/пары блокируется. Успех/error применяется только совпадающему текущему контексту. Переоткрытая форма сама устанавливает доступность кнопок; снятие busy для всё ещё того же действия не переносит старые данные в новое окно. Create не очищает изменённый пользователем draft; comment не заменяет изменённый текст и освобождает кнопку только того же DOM-редактора. Повторно открытый редактор того же ID не получает прежний result/finally. После актуального переноса/удаления/привязки перечитывается серверный список, без клиентского пересчёта общих totals.

Записи не передают отмену чтения: abort не доказывает rollback. Network/aborted/contract и HTTP >=500 обозначаются как неизвестный исход с требованием обновить список и сверить фактические данные перед повтором. Автоматического повторного POST нет; финансовая/recovery политика сервера не менялась. Этап 10 реализован изолированно, HTTP/браузерная приёмка открыта. Декомпозиция 11 и QR/storage 12 реализованы изолированно; стек/доступность 13 реализованы изолированно. Их browser/HTTP-приёмка остаётся открытой.

## Окно импорта операций

importModal показывает created/updated/error counts последней попытки и сохраняет окно открытым. Из состояния удаляются только принятые clientRowId отправленного снимка; непринятые и исключённые до отправки строки остаются. Это выполняется до reload списка, поэтому его отказ не возвращает успешные строки в следующую отправку. Ошибки пакета отображаются с sourceLineNumber PDF; ошибки распознавания сохраняются отдельно от последней попытки записи. Проверенный сервером дубль получает переключатель явного обновления комментария.

Повторный click во время сохранения отклоняется, hide.bs.modal предотвращается, управляющие элементы блокируются. Применение результата не вызывает page reload, использует общий HTTP helper, DOM API и прежний Bootstrap-стек. Preview принимается только при совпадении File object, bankId и accountId снимка; изменение любого из них требует нового preview. Ответ сервера с чужой identity отклоняется явно. Forced disposal инвалидирует preview/context; старый preview/finally не возвращает строки и не снимает busy нового запроса.

Preview и Save в одном importModal не пересекаются; окно не закрывается до ответа. Это исключает восстановление уже принятых строк поздним preview. Строгий lock сохраняется даже при принудительном сбросе окна во время POST: новый preview/save не начинается до его завершения, старый результат не применяется к новому контексту. После неизвестного исхода save строки сохраняются, повтор заблокирован до ручной сверки по списку и нового preview.

## Добавление чека

`receipt/index`/`initialization` создают одну session/controller. Controller владеет captures account/files/token/manual draft, busy, coordinators, overlay/feedback и lifecycle. `fileScanWorkflow`/`fileDecoding` только декодируют, `cameraController` сериализует start/stop/switch/pause/resume/dispose, `submission`/API выполняют транспорт, `rendering`/`cameraUi` отображают данные, `draftPersistence` хранит персональный черновик. Глобального изменяемого пакета и циклических page imports нет.

Один busy lock покрывает files/paste/drop/camera/manual/retry без очереди пакетов. Ответ/error/finally принадлежат поколению; запоздалые callback/error камеры не обновляют новый запуск. Known QR failure позволяет явно повторить captured packet/account. Malformed counters/rows отклоняются до render; успех соседей и rejected parsed rows сохраняются. Network/contract/HTTP >=500 блокируют запись до сверки списка и нового открытия страницы, POST не получает abort и не повторяется автоматически. Ручной успех очищает только неизменённый draft.

CurrentUser.Id приходит из Razor и авторизованного no-store BrowserUser handler. Проверка выполняется до restore, на resume/перед действием и после file decoding. Mismatch/401/403 очищает видимый контекст. GET/POST gap остаётся; сервер проверяет авторизацию/права каждой записи. Ключи `qrScan.user.<lowercase GUID>.manualReceiptDraft`/`.selectedAccountId` без TTL/logout deletion; legacy `qrScan.manualReceiptDraft`/`qrScan.selectedAccountId` не читаются, не мигрируют и не удаляются. Ошибка storage видна. Camera ID остаётся прежней нечувствительной preference.

Новых лимитов файлов/байтов/пикселей/времени по решению пользователя нет. Сохранены html5-qrcode/jsQR crop/scale/threshold/inversion и EXIF/lastModified fallback. EXIF actual readFromBinaryFile получает отменяемый FileReader. Собственные Image/object URL/listeners освобождаются при success/error/cancel. Actual scanFileV2 создаёт два URL, не освобождаемых public clear(); fileScanner загружает тот же vendor в отдельном same-origin iframe и учитывает URL API его realm. File переносится в realm для vendor instanceof; dispose/cancel освобождает URL и frame без изменения vendor/private API или URL основной страницы. Actual frame/CSP/canvas остаются открытой приёмкой.

Hidden/pagehide инвалидируют decoding/чтения и ставят stop камеры в очередь. Pending POST продолжает выполняться; уход во время записи требует сверки на resume/bfcache. Возвращение проверяет identity/accounts, не запускает камеру автоматически. Nonpersisted pagehide снимает subscriptions/dropzone/manual bindings и освобождает overlay/status/retry/frame/camera/feedback; Bootstrap hidden transition сохраняется перед dispose/remove. Синхронная canvas/jsQR работа отменяется между вызовами, не посередине текущего вызова. Реальные tracks/vendor память/Bootstrap transition/storage не подтверждаются fake DOM.

Финальная изолированная проверка 12: strict TypeScript build exit 0, весь frontend **124 passed / 0 failed / 0 skipped / 0 cancelled**, AddReceiptModelTests **5/0/0**, Web compile-check **0 errors / 2 existing NU1510 warnings**. Evidence и конкретная оставшаяся browser/camera/storage/CSP/user-switch процедура — [журнал 12](../Plans/project-refactoring-2026-10-13/12-qr-scanning-and-browser-storage.md). Браузер/камера/hosting/SQL/provider не запускались; неизменённые .NET-наборы не запускались.

## Модальные окна

[bootstrap.ts](../../CostVision.Web/wwwroot/ts/shared/bootstrap.ts) оборачивает Bootstrap Modal API и хранит стек открытых окон. Он управляет z-index, общим backdrop, кликами, фокусом и Escape верхнего окна.

Поставка Bootstrap 5.1.0 имеет публичные focus/keyboard options; adapter задаёт оба false и управляет top focus/Tab/Escape без private API. Нижние окна получают inert/aria-hidden, после close восстанавливается живой opener/нижний focus либо root navigation. Принятый show задаёт порядок; shown не поднимает запоздавшее нижнее окно. Cancelled show/hide проверяются после всех listeners. Hide во время show, reopen во время hide и dispose ожидают actual shown/hidden. Prevented hide не обходится, в том числе при import busy; native dispose вызывается после hidden. Последний owner снимает shared subscriptions, nonpersisted pagehide запрашивает teardown, bfcache сохраняет owners.

Каждый backdrop владеет RAF и своим 150-мс removal; новый show не отменяет старое удаление. При смене top создаётся fade-элемент между слоями, прежний отдельно удаляется. Microtasks ограждены owner/revision; общих delayed stack updates нет. Body/fixed/sticky scroll styles сохраняются до первого show, locked styles — на время стека, исходные восстанавливаются после final hidden. Это код и isolated event/timer evidence, а не доказанная плавность actual browser animation.

helpTooltip открывает безопасный DOM с role=tooltip по focus/hover/native button click, связывает уникальный popup с aria-describedby/controls/expanded и сохраняет прежние descriptions. Escape сначала закрывает описание, фокус остаётся на trigger. Rerender/removal observer/modal hidden/pagehide снимают свои listeners/timers/popup; hover допускает переход на popup. Layout имеет lang=ru; заголовки модалок связаны aria-labelledby, комментарий операции имеет доступное имя. Первый служебный столбец Stores сохранён.

Проверки 13 и конкретная открытая процедура actual browser/скринридера/анимации находятся в [журнале 13](../Plans/project-refactoring-2026-10-13/13-modal-stack-and-accessibility.md). Старые проверки SQL/auth/camera/storage/provider не закрыты.

## Проверки

MoneyMovements показывает страницу и готовые серверные count/incomeSum/expenseSum полного фильтра. Поиск и receiptFilter отправляются серверу, изменение фильтров сбрасывает страницу; неизвестные итоги обозначены явно. Оба окна кандидатов используют shared/paging.ts с явными count/страницами и флагами перехода, без клиентского Take. Для чеков count относится к документам, а Items сохраняет их подходящие копии. Независимые поколения и AbortSignal этапа 10 сохранены после декомпозиции 11. boundedReads.test.mts проверяет API/навигацию и реальный контроллер кандидатов с подставным DOM/fetch.

frontendDecomposition.test.mts проверяет production календарь/preferences/paging, formatters/DOM/API/роли, независимые page sessions и receipt workflow; requestCoordination.test.mts также проверяет actual dashboard entry при отсутствующей сумме точки. Финальная изолированная проверка 11: strict build успешен, frontend 91 passed / 0 failed / 0 skipped / 0 cancelled. Evidence, точные команды и оставшаяся процедура — [журнал 11](../Plans/project-refactoring-2026-10-13/11-frontend-decomposition.md). .NET/hosting/browser/SQL/provider-проверки 11 не запускались; прежняя приёмка остаётся открытой.

Сводки чеков и магазинов отображают серверные расход/доход полного набора до пагинации: TotalSum/TotalSpent и IncomeSum. Клиент не складывает номиналы текущей страницы. До загрузки итог неизвестен; окно нового магазина сбрасывает предыдущую сводку. Таблица магазинов содержит отдельную колонку дохода, суммы сохраняют копейки. reportTotals.test.mts проверяет реальные compiled renderers с подставным DOM; HTTP и браузерная приёмка остаются открытыми.

Compile-check выявляет ошибки типов. tests/Frontend/moneyMovementImport.test.mts вызывает настоящий собранный контроллер с подставными DOM и fetch: счётчики/номера ошибок, частичный повтор, Conflict, повторный click, запрет закрытия, смена счёта и отказ reload. Браузер не запускается; Bootstrap/backdrop/focus, routing/antiforgery и реальная сеть этим не подтверждаются. requestCoordination.test.mts вызывает реальные DOMContentLoaded/controller/API с управляемыми Promise: A-после-B, старые error/loading/finally, close/reopen разных и одинаковых ID, смена фильтров, captured command IDs/target/name, pagehide, double click и неизвестный исход. Подставные DOM/fetch/streams не подтверждают HTTP/routing/Bootstrap и реальную конкурентность; конкретная открытая процедура — [журнал этапа 10](../Plans/project-refactoring-2026-10-13/10-frontend-request-coordination.md).
