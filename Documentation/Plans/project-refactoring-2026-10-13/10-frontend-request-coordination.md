# Этап 10. Координация frontend-запросов

Статус: **Реализован, проверка не завершена**.
Дата начала: 14.10.2026.
Дата изолированной проверки: 14.10.2026.
Приоритет: **П2**.
Зависимости: 09.

## Найденные проблемы на начало этапа

1. [dashboard/index.ts](../../../CostVision.Web/wwwroot/ts/pages/dashboard/index.ts), `loadAndRenderReportAsync`, принимает каждый завершившийся ответ без проверки актуальности фильтров. [reports/receipts/index.ts](../../../CostVision.Web/wwwroot/ts/pages/reports/receipts/index.ts), `loadReceiptList`, и [moneyMovements/index.ts](../../../CostVision.Web/wwwroot/ts/pages/moneyMovements/index.ts), `reloadMovements`, имеют тот же риск. Медленный старый запрос может перезаписать новый результат.
2. Основные списки товаров/магазинов уже используют `latestLoadRequestId`, однако вложенные окна — нет. `openStorePurchasesModal`, `loadSelectedStoreReceiptsPage`, загрузка linked/candidates операций записывают ответ в текущий state после смены выбранного объекта или закрытия окна.
3. [importModal.ts](../../../CostVision.Web/wwwroot/ts/pages/moneyMovements/importModal.ts) сбрасывает state при закрытии, но поздний preview заново наполняет его. Смена банка/счёта/файла во время запроса не связана с идентичностью полученного preview.
4. Async-представления держат выбранный ID в изменяемом модульном состоянии; следующий шаг команды иногда читает уже изменённый state. Блокировка одной кнопки не решает переключение другого объекта.

## План работ

- Для каждой независимой области загрузки ввести собственное поколение запроса и снимок параметров. Принимать ответ и ошибку только для актуального поколения.
- Передавать `AbortSignal` через общий HTTP-helper; отмена дополняет проверку поколения, но не заменяет её.
- Инвалидировать поколения при закрытии/переключении модалки, изменении файла и сбросе страницы. Не отменять запись с обещанием, что она не произошла на сервере: после неизвестного исхода обновить состояние по факту.
- Команды выполнять с захваченными ID и входными значениями; обновлять UI только если его контекст всё ещё соответствует результату.
- Выделить небольшой переиспользуемый координатор только для повторяющегося поведения, без нового frontend-фреймворка.

## Проверки и приёмка

- [ ] Искусственно задержанный ответ А после ответа Б не меняет данные/ошибку/индикатор Б.
- [ ] Закрытие и повторное открытие окна для другого объекта не принимает старый ответ.
- [ ] Preview соответствует текущим файлу, банку и счёту; старые данные нельзя сохранить как новый импорт.
- [ ] Обработаны повторный клик, ошибка, отмена и неизвестный исход записывающего запроса.
- [x] Изолированные frontend-тесты используют подставной fetch и управляемые Promise, без сети и браузера.

## Журнал выполнения

14.10.2026: пользователь разрешил изолированную реализацию только 10 при открытой приёмке 01–09/07A. Прочитаны owning/parent AGENTS, планы/принятое решение 08, поставка DLL и фактические frontend/API/shared/test-модули. HTTP/envelope/shape/error 09, финансовые/access/identity/импортные/транзакционные правила и guards 07 сохранены; 11–15 не начаты.

### Реализованные области

| Область | Граница актуальности |
|---|---|
| shared/requestCoordinator | begin: новое поколение, parameters/signal/isCurrent; invalidate отменяет старое чтение; capture фиксирует контекст команды. Экземпляры независимы |
| Dashboard | Отдельные accounts/report; снимок применённых дат/источника/счетов/магазинов; старые success/error не заменяют сводку/график |
| Products | Accounts/list/purchases и naming context; search input инвалидирует ожидающий список, новое окно не получает старые покупки/ошибку |
| Stores | Accounts/list/store receipts/details и store/name/account-action/confirmation contexts; неизвестная сводка при новом магазине, старые детали не открываются |
| Receipts report | Accounts/list/query/details и confirmation; debounce/date change/pagehide инвалидируют чтение, старые error/loading не заменяют актуальную страницу |
| Receipt-money modal | Linked/candidate отдельно; снимок filter/page и revision открытия; hide/filter/reopen/pagehide исключают прежний ответ |
| MoneyMovements | Initial accounts/banks, main/linked/candidate/detail отдельно; modal revision и snapshots; pagehide очищает debounce и останавливает продолжение init |
| Import | Preview identity = File object + bankId + accountId, проверка server identity; старые строки нельзя сохранить под новыми реквизитами |
| paging | clear меняет revision; old navigation catch/finally не снимает busy новой навигации |

AbortSignal доведён от контроллеров до page API и пятого аргумента sendJsonRequest. Проверка поколения обязательна после await: отмена не гарантирует прекращения fetch/серверной обработки. Актуальный отказ отображается, устаревший и намеренная отмена не показываются как новый отказ.

Name/create/comment/refresh/move/delete/remove/link/unlink/import захватывают ID/вход до await. Повторная команда одного объекта/пары блокируется; результат применяется только соответствующему окну/списку. Reopen устанавливает состояние кнопок независимо от старого finally; изменение create/comment draft во время запроса сохраняется. Comment освобождает кнопку только в том же DOM-редакторе и не применяет старый результат к повторно открытому редактору того же ID. Delayed hidden подтверждения проверяет захваченный контекст перед восстановлением нижнего окна и не очищает новое подтверждение. Backend permissions и финансовые формулы остаются серверными, totals/count не пересчитываются по странице.

Записывающие запросы не отменяются координатором чтений. Network/aborted/contract/HTTP >=500 дают понятный неизвестный исход с требованием обновить список и сверить данные перед повтором; автоматического POST retry и обещания «не сохранено» нет. Для импорта неизвестный результат сохраняет строки и блокирует следующий save до сверки/нового preview. Строгий preview/save lock, запрет click/hide до ответа, accepted clientRowId, частичные ошибки/PDF-номера и счётчики 04 сохранены. Даже forced disposal во время POST сохраняет lock до его окончания; ответ старого окна не применяется к новому состоянию. Preview owner защищает finally нового распознавания.

### Фактическая изолированная проверка

Новый requestCoordination.test.mts: 30 cases через actual DOMContentLoaded/controller/API/coordinator/paging, управляемые Promise и подставные DOM/fetch. Проверены A-после-B, old success/error/loading/finally, abort-ignoring fetch, независимость linked/candidate, close/reopen разных и одинаковых ID, фильтры, pagehide initial accounts, captured command ID/target/name, double click, changed create/comment draft, same-ID comment reopen и неизвестный write outcome. API forwarding проверяет 14 scoped чтений; initial accounts дополнительно проверяются через actual entry points.

moneyMovementImport.test.mts: 15 cases с actual preview перед save, File/bank/account identity, server identity mismatch, unknown outcome, forced disposal/reopen, прежними import04 регрессиями строк/счётчиков/Conflict/partial/reload. Fixtures дополнены необходимыми DOM/DTO/preview данными, production validators не ослаблены.

Финальные команды (в указанном порядке):

```powershell
# Из CostVision.Web
npm run build:ts > ../artifacts/compile-check/test-results/build-typescript-stage10-final.txt 2>&1
# Из корня CostVision, после успешной сборки
node --experimental-strip-types --test tests/Frontend/*.test.mts > artifacts/compile-check/test-results/frontend-stage10-final.txt 2>&1
```

Обе команды завершились exit 0. TypeScript strict/noEmitOnError успешно собран; generated dist обновлён только build:ts.

| Контур 10 | Passed | Failed | Skipped | Cancelled | Evidence |
|---|---:|---:|---:|---:|---|
| Относящиеся frontend-регрессии (весь tests/Frontend) | 76 | 0 | 0 | 0 | artifacts/compile-check/test-results/frontend-stage10-final.txt |

Из 76 cases: 30 новых coordination, 15 import (8 прежних + 7 новых), 31 прочих HTTP/total/bounded-read регрессий. MODULE_TYPELESS_PACKAGE_JSON остался видимым, не подавлялся. До финального прогона исправлены неполные fixture и найденные same-ID/init/hidden/finally случаи; промежуточные прогоны не являются итоговой приёмкой.

Проверены строгий UTF-8, отсутствие U+FFFD/mojibake/четырёх вопросительных знаков, исходные BOM и типы окончаний строк. Смешанные CRLF/LF-файлы сохраняют смешанный формат; отчёт чеков сохраняет BOM. Файловые элементы CostVision.slnx и актуализированные навигационные ссылки разрешаются.

Бизнес-/техническая документация, owning/parent AGENTS, навигация и CostVision.slnx актуализированы. Подготовлен [самодостаточный промпт 11](prompts/next-stage-11-prompt.md); это не разрешает запуск 11.

.NET исходники/модель/схема/migrations/snapshot не менялись; новые .NET-прогоны не выполнялись. Старые результаты 09 (Application496/Infrastructure115/Web83/frontend39) — историческое evidence09, не проверка будущего 11 или новая .NET-проверка10.

### Обязательная открытая HTTP/браузерная приёмка

Запуск следующей процедуры требует отдельной прямой просьбы в разрешённом контуре. Она не выполнялась агентом; fake DOM/fetch не закрывают её.

1. На dashboard/products/stores/receipts/moneyMovements задержать чтение A, применить другие фильтры/страницу B и завершить B раньше A. Затем вернуть A с успехом и отдельно с 401/403/409/500/невалидным ответом. Проверить, что данные, график, count/totals, ошибка и loading принадлежат B; отмена A не заменяет результат B. Для поиска проверить изменение текста до следующего apply/debounce. Подтвердить реальные headers/antiforgery/envelope09, не выдавая HTML за сообщение.
2. В каждом окне purchases/store receipts/details/linked/candidates открыть объект A, задержать чтение, закрыть и открыть B, затем тот же A. Возвратить старый success/error после новых данных: окно не меняется и не открывается самопроизвольно. Изменить фильтры кандидатов и страницу независимо от linked-чтения. Проверить пустой набор, последний page и unknown totals без нуля; прежние count/page metadata не возвращаются.
3. Задержать name/move/delete/refresh/comment/link/unlink, изменить текущий UI/target, закрыть и переоткрыть другое или то же действие, дважды нажать кнопку. Сверить фактически отправленные ID/target/name с первоначальным выбором. Старый результат не закрывает новое окно, не меняет его данные/ошибку/кнопки. После завершения прежней команды same-ID форма доступна; автоматического второго POST нет. Create с изменённым draft его сохраняет.
4. Вложенное remove-confirmation: закрыть подтверждение и до delayed hidden открыть другой receipt/store/action. Старый hidden не очищает новое действие и не восстанавливает прежнее нижнее окно. Отдельно проверить обычную отмену подтверждения с возвратом правильного нижнего окна. Проверить backdrop/z-index, фокус, клики и Escape только верхнего окна на реальном Bootstrap; переделка13 не выполнена.
5. Import: подтвердить запрет preview/save overlap и hide/click до ответа; сменить file/bank/account в допустимом состоянии, затем искусственно завершить прежний preview после нового контекста. Старые rows/errors не сохраняются под новыми реквизитами. Проверить server identity, clientRowId принятых строк, частичный результат/PDF line numbers/счётчики, duplicate replacement и Conflict04. После смены любого реквизита нужен новый preview.
6. На разрешённых тестовых данных задержать/потерять ответ POST после фактической серверной записи либо вернуть непроверяемый ответ. UI должен сообщить неизвестный исход без утверждения rollback и без автоматического retry. Пользователь явно перечитывает список/связи и сверяет факт записи; для импорта следующий save заблокирован до нового preview после сверки. Повтор/замена подчиняются прежним финансовым правилам04, новые recovery-решения не вводятся.
7. Уйти со страницы во время initial/list/detail/candidate/preview и pending debounce. Старые чтения не продолжают init/обновление UI, новые запросы из старого debounce не отправляются. Возврат истории браузера (включая bfcache), реальное уничтожение документа и потеря ответа записи требуют наблюдения actual lifecycle; изолированный pagehide тест не подтверждает эти браузерные события.
8. Сохранить все открытые01–09/07A проверки: SQL materialization/predicate/include/projection/collation/precision/планы/объём/конкурентность/отзыв доступа; два соединения для записи/refresh и rollback/1205; provider/hosted/production; actual routing/model binding/cookie JSON401/403/HTML redirects/antiforgery/middleware before/after started. Пользовательская AddReceiptRowVersion (RowVersion rowversion, RefreshRevision bigint NOT NULL default0) обязательна до работы новой модели с БД; агент её не создаёт/не применяет.

Приложение/hosting/dev-server/browser/Docker/SQL/БД/provider/реальная сеть/deployment/миграции/Git и прочие проектные скрипты не запускались. Первые четыре общих критерия выше остаются открытыми до actual HTTP/browser приёмки, хотя их изолированная часть подтверждена. Итоговый статус10 — «Реализован, проверка не завершена»; 11–15 не начаты.
