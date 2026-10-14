# Доверенные reverse proxy

## Ответственность

ForwardedHeadersSettings задаёт секцию и список адресов KnownProxies.

UploadSettings сохраняет общий request body default 1 000 000 000 байт. DataProtectionSettings задаёт ApplicationName и KeyDirectory (относительный путь разрешается от content root, пустой — прежние keys-windows/keys-linux). SwaggerSettings.Enabled default=true сохраняет прежнюю экспозицию. PDF-лимиты принадлежат отдельным Infrastructure options и не распространяются на QR/camera/storage.

## Контракты и границы

Согласовать с validation и ForwardedHeadersOptions. Непустой список и корректный IP не доказывают правильность production-топологии.

## Проверка

Подходящие изолированные проверки находятся в tests/CostVision.Web.UnitTests. HTTP-хост, браузер и production pipeline проверяются отдельным разрешённым контуром.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).
