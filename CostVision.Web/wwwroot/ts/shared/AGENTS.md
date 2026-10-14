# Общие клиентские компоненты

## Ответственность

HTTP, токен, DOM, alerts, форматирование, навигация, активность, dropzone, индикатор и Bootstrap переиспользуются страницами.

## Контракты и границы

Не добавлять страницу-специфичные бизнес-правила. HTTP принимает необязательный пятый AbortSignal, сохраняет JSON/multipart/antiforgery и передаёт Accept=application/json. HttpError содержит kind/http status/url; HTML/диагностика не используются как сообщение. Envelope проверяется перед generic cast, unwrap требует data и поддерживает validator обязательных полей/списков/страниц/готовых totals. Это проверка формы, не полный runtime-schema каждого вложенного DTO. Координация не входит в HTTP-helper: requestCoordinator выдаёт ticket со снимком parameters/signal/isCurrent, invalidate отменяет прежнее чтение и меняет поколение, capture фиксирует контекст команды. Каждый экземпляр принадлежит одной независимой области; caller создаёт снимок значений и проверяет результат/ошибку/loading. Записи не получают abort этого координатора; getWriteErrorMessage обозначает network/aborted/contract/HTTP >=500 как неизвестный исход и требует обновления/сверки перед повтором. Modal stack отдаёт фокус, клики и Escape верхнему окну.

paging.ts отображает серверную страницу, count и флаги перехода через DOM API. Переход блокирует повторный click до ответа, clear скрывает прежние метаданные и меняет revision: прежние catch/finally не снимают блокировку новой навигации. Компонент не рассчитывает count/суммы, не ограничивает Items и не заменяет page-specific фильтры.

initFileDropzone возвращает disposer собственных click/keyboard/drag/drop/input listeners; прежние callers совместимы и могут игнорировать возврат.

bootstrap.ts оборачивает один actual Bootstrap 5.1.0 instance на элемент с публичными focus=false/keyboard=false. Stack сохраняет порядок принятого show, проверяет отмену после всех listeners и удаляет окно по hidden. Hide во время show и reopen во время hide ожидают lifecycle; dispose не вызывает native dispose до hidden и не обходит prevented hide. Microtasks ограждены revision/owner; общих отложенных stack timers нет. Каждый backdrop/RAF/removal принадлежит своему элементу; при смене верхнего окна создаётся fade backdrop между слоями. Исходные scroll styles восстанавливаются после последнего окна.

Top owner получает Tab/focus/click/Escape; нижние окна inert/aria-hidden, native FocusTrap не активируется. После close восстанавливается живой opener/нижний focus либо доступная root navigation. Не обращаться к private Bootstrap API. Последний dispose снимает document/window subscriptions; непостоянный pagehide запрашивает disposal, bfcache сохраняет владельцев. Отмена hide может отложить teardown до разрешённого закрытия.

helpTooltip сохраняет button API: focus открывает описание, native Enter/Space активируют click, Escape закрывает tooltip прежде модалки. Popup имеет role=tooltip, уникальный id и aria-describedby/controls/expanded; DOM безопасный. Hover допускает переход на popup. Host/rerender/removal observer/pagehide владеют subscriptions и timers; modal hidden закрывает описание. Не перемещать фокус на неинтерактивный popup.

## Проверка

Проверка типов — npm run build:ts по корневым правилам. Поведение DOM, модальных окон, камеры и запросов требует отдельных сценариев; сборка не подтверждает браузерную приёмку.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).

dateRange содержит общий включительный календарь с прежним default currentMonth. preferences сохраняет cookie encoding/path/max-age/samesite и query page semantics; malformed percent не скрывается. paging разделяет прежние catalog и centered окна, не пересчитывает серверные count/итоги. formatters требуют typeof number + finite для обязательных чисел; optional text нормализуется отдельно. requireElementById проверяет HTMLElement и явно переданный runtime constructor; generic без constructor гарантирует только базовый тип. shared/receipts владеет общими DTO/транспортом/workflow, получая page-specific coordinators/locks/callbacks без глобального store.
