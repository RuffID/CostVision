# Этап 12. Сканирование и данные браузера

Статус: **Реализован, проверка не завершена**.
Дата начала: 14.10.2026.
Дата изолированной проверки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 03, 09, 10.

## Найденные проблемы

Ниже исходные основания этапа до реализации; актуальные владельцы и ограничения указаны в журнале.

1. Модули [receipt/initialization.ts](../../../CostVision.Web/wwwroot/ts/pages/receipt/initialization.ts), `submission.ts`, `fileScanWorkflow.ts`, `cameraController.ts`, `rendering.ts` имеют взаимные импорты и общий изменяемый `receiptPageState`. Многие файлы повторяют один длинный блок импортов, включая неиспользуемые зависимости. Формальное разделение файлов не создало независимых обязанностей.
2. `qrScanScanFilesAndSubmit` очищает глобальные результаты и заполняет их по мере асинхронного чтения. Другой запуск через paste, drag/drop или камеру может очистить тот же state. Вызовы отправки иногда не ожидаются; общий overlay снимает любой завершившийся запрос.
3. [submission.ts](../../../CostVision.Web/wwwroot/ts/pages/receipt/submission.ts) при ошибке AJAX пишет в console и возобновляет камеру, но не показывает явный отказ сохранения. `qrScanApplyServerResponse` подставляет нулевые счётчики и пустой список при нарушении обязательной формы ответа.
4. [draftPersistence.ts](../../../CostVision.Web/wwwroot/ts/pages/receipt/draftPersistence.ts) и [constants.ts](../../../CostVision.Web/wwwroot/ts/pages/receipt/constants.ts) используют один ключ черновика на origin. При смене пользователя в том же браузере могут восстановиться реквизиты прежнего чека. Выбранный счёт также хранится общим ключом.
5. Камера имеет асинхронные start/stop/switch без единого сериализованного жизненного цикла. В просмотренном модуле нет обработчика ухода/скрытия страницы; поведение освобождения ресурса оставлено браузеру и библиотеке. Это требует проверки, а не заявления о доказанном оставшемся включённом устройстве.
6. Попытки разных декодеров и EXIF допускают осмысленные fallback, но нет явного общего лимита количества файлов, размеров изображения и длительности обработки. Их нельзя путать с fallback, скрывающим неверный серверный контракт.

## План работ

- Выделить независимые декодирование, камеру, отправку, отображение и хранение черновика; оркестрация находится в одном controller/init.
- Каждый запуск обрабатывает свой неизменяемый пакет и результат. Ввести управление занятостью/отменой и принадлежностью overlay.
- Ошибку сохранения явно показывать пользователю. Проверять серверный ответ до отрисовки; не изображать успешные нули.
- Привязать чувствительный черновик и выбранный счёт к пользователю, определить очистку при выходе и срок хранения. Не читать данные прежнего пользователя автоматически; миграцию старого общего ключа решить явно.
- Сериализовать start/stop/switch камеры и освобождать подписки/таймеры/сканер по lifecycle. Политику лимитов согласовать: пользователь выбрал отсутствие новых ограничений, busy и lifecycle-отмену.

## Проверки и приёмка

- [ ] Два запуска сканирования не перемешивают результаты и не снимают чужой overlay.
- [ ] Ошибка HTTP/сохранения видна; известный отказ допускает явный повтор исходного пакета, неизвестный исход требует сверки до новой записи. Реальная дедупликация/конкурентность проверяется отдельно в 03/04.
- [ ] Переключение пользователя не раскрывает прежний черновик и счёт.
- [ ] Быстрые смены камеры и stop во время start дают устойчивый результат.
- [ ] Object URL, таймеры и подписки освобождаются; валидные стратегии декодирования сохранены.
- [ ] Работа реальной камеры проверена отдельно в браузере с разрешения пользователя.

## Журнал выполнения

14.10.2026: выполнена изолированная реализация только 12 при открытой приёмке 01–11/07A/08A. Пользователь подтвердил: legacy общие ключи не читать/не переносить/не удалять; новые персональные данные без TTL и удаления при logout, восстановление тому же пользователю; новых лимитов файлов/байтов/пикселей/времени нет, применяется занятость и lifecycle-отмена. Существующая реализация 08A сохранена; 13–15 не начаты.

### Фактические владельцы

- `receipt/index`/`initialization` создают один `controller` и независимую `state` session. Controller связывает DOM events, captures account/files/token/manual draft, shared coordinators, busy/overlay, identity, явный retry, feedback и lifecycle. Глобальный изменяемый пакет и циклические runtime page imports удалены.
- `fileScanWorkflow`/`fileDecoding` обрабатывают локальный пакет последовательно без HTTP/UI: html5-qrcode, прежние jsQR crop/scale/threshold/inversion стратегии, EXIF Original/Digitized/DateTime и lastModified fallback. Отмена проверяется после await и между стратегиями; синхронная canvas/jsQR работа не прерывается внутри текущего вызова и новых лимитов нет.
- `fileScanner` создаёт отдельный same-origin iframe и загружает тот же vendor script по actual Razor src. Изолированный URL API realm учитывает два URL, создаваемых actual scanFileV2; public clear() vendor только очищает DOM. Adapter переносит File в vendor realm, освобождает URL после попытки/отмены и удаляет frame при dispose. Vendor/private API и URL API основной страницы не изменены. Собственные Image URL/FileReader/listeners освобождаются по lifecycle; actual iframe/CSP/canvas/vendor остаются открытой приёмкой.
- `cameraController` сериализует start/stop/switch/pause/resume/dispose, проверяет revision callbacks и ошибок; stop допускает actual PAUSED. Устаревший успешный start останавливается, поздний failure не меняет статус нового устройства. `cameraUi`/`rendering` отображают входные данные, не отправляют запросы и не читают page state. Mobile feedback отображает все строки, включая отказ соседей.
- `submission`/API отправляют captured payload и проверяют ServiceResult. QR требует обязательные неотрицательные safe-integer counters, соответствующее число строк и согласованность actual counters. Успешная строка содержит parsed sum/known operationType; rejected parsed rows могут иметь null sum/unsupported type. Неверная форма не становится пустым успехом.
- `draftPersistence`/`constants` владеют `qrScan.user.<lowercase GUID>.manualReceiptDraft` и `.selectedAccountId`. Owner — непустой CurrentUser.Id, не display name. Старые `qrScan.manualReceiptDraft`/`qrScan.selectedAccountId` остаются нетронутыми. Storage retention не удаляет данные при logout; успешный manual удаляет только неизменённый draft. Camera ID остаётся прежней нечувствительной device preference.

Razor передаёт CurrentUser.Id в `data-current-user-id`. Единственная необходимая backend-правка — авторизованный AddReceipt `OnGetBrowserUser`, ValidateBinding → Cache-Control no-store → стандартный ServiceResult<Guid>/JsonResultMapper без Application-вызова. Controller проверяет актуальную identity до восстановления/на resume/перед действием, для файлов ещё после decoding. Обнаруженная смена пользователя/401/403 очищает видимые реквизиты, счёт и результаты, сохраняя персональный storage. Проверка GET и POST не атомарна и не является абсолютной защитой от смены cookie между запросами; актуальные права и авторизацию каждой записи проверяет сервер.

### Занятость, ошибки и lifecycle

Один lock охватывает files/paste/drop/camera/manual/retry, очередь пакетов не вводится. Вход неизменяемо захватывается, второй запуск получает видимое сообщение. Overlay/finally принадлежит конкретному запуску. Известный QR-отказ сохраняет пакет и даёт кнопку явного повтора с исходным счётом; успешный ответ с ошибками отдельных строк не повторяется целиком. Network/contract/HTTP >=500 обозначают неизвестный исход и блокируют запись в session до сверки серверного списка и нового открытия добавления. POST не получает read AbortSignal и не повторяется автоматически. Финансовые правила дедупликации/права/refresh/import/UoW не менялись.

Visibility hidden/pagehide инвалидируют accounts/runs/camera reads и decoding, сериализуют stop камеры и скрывают overlay/feedback. Pending POST продолжает выполняться; resume/bfcache после ухода во время записи требует сверки и не применяет старый результат. Возвращение перепроверяет identity/accounts, камера автоматически не перезапускается. Nonpersisted pagehide снимает owned events/dropzone/manual listeners, удаляет overlay/status/retry, освобождает file frame/camera/feedback instance. Bootstrap hidden transition сохраняет событие shared stack до dispose/remove; его реальные анимации и focus trap не подтверждены. Shared 11 изменён только совместимым возвратом dropzone disposer и декларацией actual Modal.dispose; алгоритм стека 13 не менялся.

### Изолированные проверки

Команды выполнены без приложения/hosting/browser/camera/Docker/реальной сети/БД. Каталог evidence: `artifacts/compile-check/test-results/`.

| Проверка | Команда | Результат / evidence |
|---|---|---|
| TypeScript, из CostVision.Web | `npm run build:ts` | exit 0, strict/noEmitOnError; `build-typescript-stage12-final.txt`; generated dist обновлён только компилятором |
| Весь frontend | `node --experimental-strip-types --test tests/Frontend/*.test.mts` | **124 passed / 0 failed / 0 skipped / 0 cancelled**, exit 0; `frontend-stage12-final.txt` (91 прежний + 33 receipt cases); MODULE_TYPELESS_PACKAGE_JSON не подавлялся |
| Web compile-check | `dotnet build CostVision.Web/CostVision.Web.csproj -c Debug --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --verbosity quiet` | exit 0, 0 errors / 2 existing NU1510 warnings Infrastructure DI/logging refs; `build-web-stage12-final.txt` |
| Только AddReceipt handlers | `dotnet test tests/CostVision.Web.UnitTests/CostVision.Web.UnitTests.csproj --no-restore -p:BaseOutputPath=D:/Media/User/source/repos/CostVision/artifacts/compile-check/ -p:SkipFrontendBuild=true --filter FullyQualifiedName~AddReceiptModelTests --logger trx --results-directory artifacts/compile-check/test-results/web-stage12 --verbosity quiet` | **5 passed / 0 failed / 0 skipped**, exit 0; `web-stage12.txt`, `web-stage12/Spike_PC_2026-10-14_21_40_30_net10.0.trx`; те же 2 warnings |

- [x] Подставные production entry/controller/API подтверждают captured packet/account, busy, явный отказ/retry, strict response и unknown outcome без автоматического POST.
- [x] Подставные identity/storage подтверждают разные user keys, legacy untouched, retention после новой session/logout, fail-closed restore, changed draft.
- [x] Управляемые Promise/fake scanner подтверждают rapid switch, stop during start, stale callback/failure, lifecycle/bfcache и cleanup URL/frame/Image/FileReader/subscriptions.
- [x] html5-qrcode/jsQR и EXIF fallback сохранены на изолированных fixtures; 09–11 frontend regressions прошли вместе с 12.

Это не actual camera/media-track/CSP/Bootstrap/storage/HTTP acceptance. Неизменённые Application/Infrastructure .NET-наборы и полный Web-набор в 12 не запускались; прежние результаты не объявлены свежими. IntegrationTests/EF tooling/SQL/provider/hosting не запускались, схема/миграции не менялись. Пользовательская AddReceiptRowVersion уже существует в исходниках; применение и реальная схема не проверены.

### Оставшаяся процедура приёмки (только с отдельным разрешением)

1. На поддерживаемых desktop/mobile браузерах проверить фактический AddReceipt entry и same-origin iframe: загрузка html5-qrcode при production CSP/frame/script policy, File realm, html5-qrcode/jsQR/EXIF и изображения разной ориентации. В DevTools проверить освобождение обоих vendor URL, own Image URL/frame/handlers после success/failure/cancel; isolated URL accounting не доказывает vendor memory/media cleanup. Без новых лимитов отдельно зафиксировать время/память больших наборов, без молчаливого обрезания.
2. На разрешённых устройствах camera permission/denial/no-device, start → stop во время start, rapid switch, callback старого устройства, PAUSED → stop, repeated hide/return/pagehide/bfcache. Подтвердить фактическое выключение tracks/индикатора, отсутствие автоматического перезапуска на resume и старых ошибок/результатов.
3. Одновременные paste/drop/file/camera/manual/retry, смена счёта/полей во время обработки, отказ записи/Conflict/partial rows и ошибочный response. Overlay принадлежит запуску; known failure явно повторяет captured packet/account, неизвестный outcome требует сверки, POST не отменяется как rollback и не повторяется автоматически. Серверную дедупликацию и гонки продолжить по 03/04, не выводить их из UI-lock.
4. Пользователи A/B на одном origin: legacy общие keys, персональные drafts/accounts, logout/login, новая вкладка, смена cookie при старой странице, hidden/resume/bfcache, late identity/accounts и 401/403, storage denial/corruption. Подтвердить отсутствие автоматического чужого восстановления, untouched legacy и retention A для возврата A. GET/POST gap остаётся; серверная auth/antiforgery acceptance проводится отдельно по 09.
5. Mobile feedback с несколькими строками/ошибками соседей: actual Bootstrap show/hide/transition/dispose, backdrop/focus/Escape, закрытие/уход во время transition. Визуальные дефекты фиксировать для 13; не объявлять стек исправленным в 12.
6. Повторить открытые 09–11 HTTP/DOM/events/AJAX/calendar/paging/commands/import проверки и сохранить все SQL/concurrency/migration/provider/maintenance/production acceptance 01–08A. Результаты записать отдельно, затем пересмотреть критерии и статус 12.

Статическая финальная проверка: 21 compiled receipt-модуль без циклов локальных runtime imports; нет receiptPageState/innerHTML/direct fetch. Все 49 незакоммиченных файлов проверены как UTF-8 без U+FFFD/mojibake/четырёх вопросительных знаков и смешанных окончаний строк; в частично изменённых CRLF-исходниках types/globals/AddReceipt добавленные строки сохранены как CRLF. XML slnx читается, все его targets и проверенные ссылки текущей навигации/технических документов существуют. Это не runtime/browser acceptance.

Owning/parent AGENTS, business/technical docs, navigation и slnx актуализированы. Передача: [самодостаточный промпт 13](prompts/next-stage-13-prompt.md); подготовка не начинает 13.
