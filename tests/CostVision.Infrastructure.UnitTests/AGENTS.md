# Изолированные инфраструктурные проверки

## Ответственность

Проект проверяет EF metadata, hashing, QR, converters и provider с подставным транспортом.

## Контракты и границы

После 07A фикстуры передают actual use case минимальные constructor-зависимости через TestUseCaseFactory. ITestStorage остаётся только набором test mocks; технические Save/transaction callbacks общие, production-контракта со всеми repositories нет.

Сохранять изоляцию от рабочей БД, внешней сети и процессов. Свойства реального SQL и внешнего API этими тестами не подтверждаются.

Stage08BoundaryTests инспектирует actual EFCoreLibrary/HttpClientLibrary contracts; тестовый проект прямо ссылается на обе принятые DLL из libs. Это не дополнительная production-зависимость или замена persistence/HTTP. Версии/хеши и неизвестный upstream описаны в libs/README.md.

## Проверка

Stage14ConfigurationTests проверяет actual layer DI/ValidateOnStart, HTTP(S)/finite timeout/PDF limits и immutable maintenance snapshot без открытого connection. Stage14PdfTests использует настоящий PdfPig и memory streams: точные byte/page boundaries, non-seekable limit+1, malformed PDF, cancellation/unknown I/O и caller ownership. Это не приёмка произвольного PDF/decompression, SQL/HTTP/OS/production.

Проверять тестовые утверждения и границы их изоляции; запуск выбирать по правилам родительского AGENTS.md. Не ослаблять ожидания и не сообщать о прохождении без выполнения.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../Documentation/Plans/project-refactoring-2026-10-13/README.md).
