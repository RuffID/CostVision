# Этап 09. Запросы и ошибки web/HTTP

Статус: **Реализован, проверка не завершена**.
Дата начала: 14.10.2026.
Дата изолированной проверки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 01, 02, 04.

## Исходные проблемы до этапа 09

1. PageModel финансовых страниц передают входные DTO без явной общей проверки невалидного binding/`ModelState`. Use case обращаются к `Results.Count`, `Rows.Count` и `Receipt` без защиты от явно присланных null и null-элементов. Инициализатор DTO не запрещает JSON заменить значение на null.
2. Date range и допуски проверяют отрицательность, но не верхние границы. `DateTo.Date.AddDays(1)` и большой `TimeWindowHours` могут бросить исключение вместо ожидаемого validation-ответа. Разные методы сравнивают полную дату либо `.Date`.
3. [ExceptionHandlingMiddleware](../../../CostVision.Web/Web/Middleware/ExceptionHandlingMiddleware.cs) меняет статус и пишет JSON без `Response.HasStarted`. Любой `OperationCanceledException` превращается в 499, даже если пользователь не отменил запрос. В production параллельно установлен `UseExceptionHandler`, поэтому контракт зависит от того, какой обработчик первым перехватил ошибку.
4. [http.ts](../../../CostVision.Web/wwwroot/ts/shared/http.ts) не принимает `AbortSignal`, доверяет generic cast и при ошибке возвращает сырой текст. Cookie challenge может перенаправить AJAX на HTML логина, после чего клиент выдаст ошибку JSON вместо понятной истёкшей сессии.
5. Общий HTTP-helper теряет тип ошибки/статус в обычном `Error`. Ошибки страниц показываются по-разному: alert, console, общий блок и модальная ошибка. При пустом/неверном ответе некоторые потребители подставляют пустые значения.

## План работ

- Ввести единую проверку формы транспортного запроса до бизнес-сценария: null, коллекции, типы, допустимые размеры и диапазоны. PageModel остаётся адаптером; финансовые проверки остаются в Application.
- Согласовать трактовку календарного периода и исключительные границы дат. Ограничения должны давать понятный отказ, а не переполнение или тихую нормализацию ошибочных значений.
- Определить одного владельца неожиданных ошибок; различать отмену запроса и внутреннюю отмену/timeout. После начала ответа не пытаться сформировать второй ответ.
- Для AJAX согласовать 401/403 и HTML-навигацию при истёкшей cookie; сохранить antiforgery.
- Расширить общий HTTP-контракт сигналом отмены и структурированными ошибками. Проверять обязательную форму ответа на границе, не отключая TypeScript strict.

## Проверки и приёмка

- [x] Изолированные actual handlers/сценарии отвергают binding/null/коллекции/крайние даты без бизнес-записи.
- [x] Изолированные actual cookie options возвращают JSON401/403; helper отвергает legacy HTML-редирект.
- [x] Actual middleware с подставным context различает RequestAborted, внутреннюю отмену/timeout и HasStarted, сохраняет400/413.
- [x] Изолированные helper/API отклоняют HTML/служебную диагностику, пустой/malformed envelope и обязательные данные; старые frontend-регрессии проходят.
- [x] Сохранены HTTP-коды `JsonResultMapper`, заголовок antiforgery и fail-fast для неизвестных контрактов в изолированных проверках.
- [ ] Реальные HTTP body/query/multipart/binding/даты и отсутствие записи проверены через разрешённый хост.
- [ ] Cookie expiry/policy, HTML-навигация и antiforgery проверены через фактический HTTP-конвейер.
- [ ] Actual UI показывает безопасные ошибки и сохраняет totals/import/Conflict в браузере.
- [ ] HTTP/auth граница проверена отдельным разрешённым hosting-тестом, если он нужен; unit tests адаптеров отмечены отдельно.

## Журнал выполнения

14.10.2026: пользователь разрешил изолированную реализацию только 09 при открытой приёмке 01–08. Начато чтение текущих исходников и контрактов; hosting/SQL/browser/provider не разрешены. Этапы 10–15 не начаты.

### Реализованный контракт

14.10.2026: завершена изолированная реализация09. Статус — «Реализован, проверка не завершена»: реальные HTTP/auth/browser проверки не запускались. Этапы10–15 не начаты.

- 57 AJAX handlers страниц AddReceipt, Receipts, MoneyMovements, Index, Products, Stores, Settings/Users и Settings/UserSettings проверяют ModelState/binding до сценария. DTO handlers дополнительно вызывают PageRequestValidationExtensions/RequestShapeValidation: null body, обязательный Receipt, Results/Rows/Members/RoleIds/AccountIds/StoreNames и null-элементы дают Validation400. Бизнес-права и финансовая валидация остаются Application; прямые QR/manual/import/preview и date-query сценарии имеют собственные проверки необходимых входов. Ошибочная форма не заменяется пустым успехом.
- RequestDateValidation сравнивает календарные даты, отклоняет DateTo=31.12.9999 до вычисления следующего дня и проверяет TimeWindowHours по доступному диапазону конкретной загруженной даты до умножения/AddMinutes. Кандидаты с UseTimeWindow не вычисляют неиспользуемую календарную границу. Числовой допуск суммы не умножается и нового произвольного лимита не получил; существующий paging clamp1–100 сохранён. Терминальная дата расхода/импортной строки отклоняется до записи/поиска следующего дня. Для ручной операции проверяется тип actual доменной factory, включая отрицательную сумму без явного Type. Preview отвергает FileLength<=0 до I/O; stream остаётся caller-owned. Calculator не вычисляет следующую дату после последнего дневного/месячного bucket. ReceiptReportCandidateCounts возвращает ServiceResult и явно отказывает, если исходное окно день±1 нельзя представить; неизвестный count не заменяется нулём.
- CookieHttpContract подключён actual AddCookie: JSON Accept, AJAX header либо JSON Content-Type получают401/403 с success=false/message без Location. HTML сохраняет redirect. Cookie name, /login, sliding expiration и14 дней сохранены; antiforgery не отключён. JsonResultMapper не изменён: Validation400, Unauthorized401, Forbidden403, NotFound404, Conflict409, ExternalService502, неизвестный ServiceErrorType вызывает исключение, которое middleware возвращает как500.
- ExceptionHandlingMiddleware — единственный владелец неожиданной HTTP-ошибки; параллельный UseExceptionHandler удалён. При клиентском RequestAborted до начала —499 без тела, после начала отмена не переписывает ответ. Внутренняя OperationCanceledException —500; TimeoutException —504; BadHttpRequestException сохраняет статус400/413 с безопасным сообщением. Иные ошибки после HasStarted логируются и пробрасываются хосту без второго ответа. До начала очищаются прежние headers/body; ошибка после уже отменённого запроса не записывает JSON. JSON/AJAX получает envelope, HTML-навигация — общий HTML с ошибочным статусом. Исходное exception/path/TraceId логируются, диагностика клиенту не раскрывается. Ожидаемые provider/retry ExternalService502 и SQL Conflict409 сохранены.
- shared/http.ts принимает необязательный пятый AbortSignal с совместимостью прежних вызовов. HttpError наследует Error и хранит kind(http/contract/network/aborted), status/url. Fetch и чтение body различают отмену/сетевой отказ. Обязательные success/message, непустой JSON и data проверяются; false data и пустой корректный массив допустимы. Неправильный200 failure, отсутствующие/null обязательные данные и malformed JSON дают contract error. HTML/proxy diagnostics заменяются безопасным сообщением; legacy login redirect сообщает об истёкшей сессии.
- 10 API-потребителей проверяют обязательные списки/поля, pages/count/flags/конечные серверные totals, детали/refresh, outcomes ручного чека и импортные rows/errors/counters. Проверка необходимой формы не является полной runtime-schema всех вложенных DTO. Клиент не вычисляет финансовые totals и не подставляет фиктивный пустой успех. Общие поколения запросов/disposal/координатор и декомпозиция10–11 не внедрялись.
- Актуализированы owning/parent AGENTS, бизнес-/техническое описание, навигация/solution и [самодостаточный промпт10](prompts/next-stage-10-prompt.md). Generated frontend обновлён только разрешённой TS-сборкой. Сохранены принятый вариант А08 и все открытые проверки01–08/07A; схема, миграции и финансовые правила не менялись.

### Изолированные проверки, 14.10.2026

Новые Stage09TransportTests вызывают actual handlers с invalid ModelState/null и strict-зависимостями (без вызова бизнес-сценария); reflection используется только в тестовом перечислении. Stage09RegressionTests вызывают actual сценарии крайних дат/окна и ошибочной формы; Stage09HttpTests проверяют actual cookie options/middleware с подставным HttpContext, включая HasStarted и BadHttpRequest400/413. httpContracts.test.mts вызывает compiled helper/API с подставными fetch/streams. Прямой handler, DI, metadata и fake HTTP context не подтверждают HTTP routing/auth/antiforgery либо SQL.

| Контур | Passed | Failed | Skipped | Evidence относительно artifacts/compile-check/test-results |
|---|---:|---:|---:|---|
| Application.UnitTests, включая регрессии01–08/07A | 496 | 0 | 0 | application-stage09-final-r2/Spike_PC_2026-10-14_17_40_19_net10.0.trx |
| Infrastructure.UnitTests, включая actual DLL/DI/scope | 115 | 0 | 0 | infrastructure-stage09/Spike_PC_2026-10-14_17_23_09_net10.0.trx |
| Web.UnitTests, финальный повтор после проверки400/413 | 83 | 0 | 0 | web-stage09-final-r2/Spike_PC_2026-10-14_17_34_25_net10.0.trx |
| Frontend, все tests/Frontend/*.test.mts | 39 | 0 | 0 | frontend-stage09-final.txt; cancelled=0 |

TRX всех трёх .NET-наборов: aborted=0, error=0, timeout=0. Domain/Application/Infrastructure/Web и compile-only IntegrationTests отдельно собраны: **0 warnings/0 errors** каждый. TypeScript strict/noEmitOnError успешен. Исходное Node MODULE_TYPELESS_PACKAGE_JSON не подавлялось. Логи сборок: build-{domain,infrastructure,typescript}-stage09-final.txt и build-{application,web,integration}-stage09-final-r2.txt. Application повторён после регрессии расхода без Type; прежний Application495 и Web81 не являются финальными цифрами. Промежуточные ошибки добавления тестов исправлены: dashboard проверяется через actual use case, frontend account export соответствует исходнику; итоговая таблица относится к финальным успешным запускам.

Read-only проверка собственных исходников/документов строгим UTF-8 decoder не выявила U+FFFD, последовательностей из четырёх вопросительных знаков или проверенных признаков mojibake. Проверены128 относительных ссылок в актуализированных документах и все File/Project targets CostVision.slnx: отсутствующих путей нет. Ручные правки выполнены через apply_patch; generated frontend не редактировался вручную.

Команды проверок (cwd — корень проекта, кроме явно указанной TS-сборки):

```powershell
dotnet build CostVision.Domain/CostVision.Domain.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/
dotnet build CostVision.Application/CostVision.Application.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/
dotnet build CostVision.Infrastructure/CostVision.Infrastructure.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/
dotnet build CostVision.Web/CostVision.Web.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true
dotnet build tests/CostVision.Infrastructure.IntegrationTests/CostVision.Infrastructure.IntegrationTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true
dotnet test tests/CostVision.Application.UnitTests/CostVision.Application.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/application-stage09-final-r2
dotnet test tests/CostVision.Infrastructure.UnitTests/CostVision.Infrastructure.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ --logger trx --results-directory artifacts/compile-check/test-results/infrastructure-stage09
dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --logger trx --results-directory artifacts/compile-check/test-results/web-stage09-final-r2
# cwd: CostVision.Web
npm run build:ts
# cwd: корень проекта
node --experimental-strip-types --test tests/Frontend/*.test.mts
```

### Открытая HTTP/auth/browser-приёмка

Ни приложение, hosting, browser, SQL/БД, Docker, provider, реальные сетевые запросы, deployment, миграции, Git либо прочие проектные скрипты не запускались. Hosting и реальная приёмка требуют отдельной предусмотренной правилами просьбы; это не мешает фиксации разрешённой изолированной реализации09.

После отдельного разрешения выполнить следующий контур и записать actual URL/handler/status/envelope/отсутствие записи, а не результат прямого вызова метода:

1. Через реальные GET/query, POST JSON и multipart маршруты с корректным antiforgery передать malformed JSON/GUID/decimal/date, null body/Results/Rows/Members/Receipt и null-элементы; ожидать400 без бизнес-записи. Проверить календарный период одного дня, терминальную дату и большое окно через фактические endpoints. Для file guard проверить отсутствующий/пустой PDF; не менять существующие лимиты транспорта.
2. На защищённом маршруте без/с истёкшей cookie проверить Accept JSON, AJAX header, JSON body:401 envelope без Location/HTML. С вошедшим пользователем без policy —403. HTML-навигация должна сохранить redirect/ReturnUrl и прежнюю cookie. Отдельно проверить missing/invalid/valid antiforgery token; framework400 может иметь не-JSON body, helper должен показать безопасное сообщение.
3. В разрешённом изолированном hosting fixture без реальных сервисов вызвать внутреннюю отмену/timeout/unknown exception до начала, BadHttpRequest400/413, клиентскую отмену и ошибку после начала ответа. Проверить500/504/400/413, отсутствие второго body/headers после начала, логи path/TraceId, отсутствие stack trace/служебного текста. Клиентский499 может быть недоступен уже отключившемуся клиенту; сверять серверные evidence и отсутствие повторной записи. Fixture/хост в09 не запускались.
4. В браузере проверить истёкшую сессию,403/400/409/502/504, пустой/неправильный ответ и сетевой отказ в actual UI: сообщения без HTML, никакого ложного success/нулевого total, сохранены счётчики/ошибки импорта, details/refresh и раздельные итоги. Прежний Bootstrap/backdrop/focus и lifecycle-проверки не закрывать автоматически.

Обязательная пользовательская AddReceiptRowVersion сохраняется: RowVersion rowversion и RefreshRevision bigint NOT NULL default0. Реальная схема, SQL/materialization/two-session races/rollback/collation/precision, provider/hosted lifecycle и все открытые проверки01–08/07A остаются открытыми. Отмена HTTP не доказывает, что уже начавшаяся серверная запись не произошла.

