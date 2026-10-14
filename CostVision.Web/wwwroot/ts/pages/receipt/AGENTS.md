# Добавление и сканирование чеков

## Ответственность

index/initialization создают одну независимую page session; controller владеет событиями, занятостью, поколениями чтений, захваченным пакетом/счётом и lifecycle. fileScanWorkflow/fileDecoding декодируют локальные файлы, cameraController сериализует vendor start/stop/switch/pause/dispose, submission/API выполняют транспорт, rendering/cameraUi отображают входные данные, draftPersistence владеет персональными ключами.

## Контракты и границы

Нет общего изменяемого пакета и циклических page imports. Files/paste/drop/camera/manual/retry используют общий busy lock без очереди пакетов; каждый запуск захватывает вход и проверяет ticket после await. Pagehide/visibility hidden инвалидируют decoding/чтения и ставят stop камеры в очередь. Поздние camera callbacks/errors не принадлежат новому устройству. POST не отменяется и не повторяется автоматически; неизвестный исход блокирует запись до сверки серверного списка и нового открытия страницы. Известный отказ QR позволяет явно повторить исходный пакет с исходным счётом; успех соседних строк и ошибки дублей сохраняются.

CurrentUser.Id приходит из Razor и авторизованного no-store BrowserUser handler: проверка до восстановления, при возвращении и перед отправкой, для файлов также после decoding. GET/POST не атомарны; права проверяет сервер. При обнаруженной смене пользователя/401/403 очищается видимый чувствительный контекст. Storage использует qrScan.user.<GUID>.manualReceiptDraft/selectedAccountId без TTL/удаления при logout; legacy общие ключи не читать, не мигрировать и не удалять. Успех ручного ввода очищает только неизменённый черновик. Camera ID — прежняя нечувствительная device preference.

По решению пользователя новых лимитов количества/байтов/пикселей/времени нет. Сохранены html5-qrcode/jsQR canvas-стратегии и EXIF/lastModified fallback. fileScanner изолирует vendor в same-origin iframe, отслеживает оба object URL в отдельном realm и удаляет frame при отмене/dispose; URL API основной страницы и vendor-код не менять. EXIF читается actual readFromBinaryFile через отменяемый FileReader. Собственные Image URL/listeners и dropzone subscriptions имеют cleanup. Реальный iframe/CSP/canvas/vendor lifecycle требует браузерной приёмки.

## Проверка

Feedback modal связан через aria-labelledby с qr-camera-modal-title. Его controller сохраняет hidden-последовательность 12; shared adapter 13 дополнительно откладывает native dispose до окончания переходов, без изменения scanning/storage/busy правил. Real feedback/focus/animation/camera приёмка открыта.

Проверка типов — npm run build:ts по корневым правилам. Поведение DOM, модальных окон, камеры и запросов требует отдельных сценариев; сборка не подтверждает браузерную приёмку.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).

API разворачивает проверенный ServiceResult; QR проверяет обязательные safe-integer counters, все строки/parsed и согласованность счётчиков с actual сервером. Ошибки строк могут содержать rejected operationType/null sum, успешные — обязательные данные. Отказ не заменяется нулевым успехом. receiptScanning.test.mts вызывает actual entry/controller/API/storage/camera/file adapter с подставным DOM/fetch/vendor; это не реальная камера, Bootstrap или HTTP-приёмка. Точные результаты и процедура находятся в плане 12.
