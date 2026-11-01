# Проверки настройки reverse proxy

## Ответственность

Тесты проверяют binding, валидацию IP и регистрацию ForwardedHeadersOptions.

HealthEndpointTests проверяет default AddUptimeHarborHealth и DLL mapping: GET /health, anonymous metadata, имя из подставного IHostEnvironment.ApplicationName, no-store и неизменное время процесса. Вызывает RequestDelegate в памяти без хоста, listener, БД или запуска процессов; не подтверждает полную production-регистрацию, авторизацию pipeline и внешний URL.

StartupCancellationTests проверяет связь caller token и моделируемого startup сигнала с отменой, disposal и запрет обращения к освобождённому CTS. Web предоставляет friend-доступ только тестовому проекту; реальный OS signal/host/процесс не запускается.

## Контракты и границы

WebConfigurationProviderTests проверяет настоящий config.json в отдельном тестовом каталоге, environment/command-line precedence и runtime options без создания key ring. StartupOptionsValidationTests вызывает actual service-only startup boundary: все options валидируются до fake maintenance, logs содержат только code, readiness ждёт async cleanup. Эти тесты не создают/не запускают WebApplication, listener, SQL или процессы.

Параметра SingleInitializerConfirmed в production-настройках нет. Отказ maintenance-настроек проверяется пустым ServerBackupDirectory.

Проверять пустые и неверные адреса без ослабления fail-fast. Это не проверка реальной сетевой топологии.

## Проверка

Проверять тестовые утверждения и границы их изоляции; запуск выбирать по правилам родительского AGENTS.md. Не ослаблять ожидания и не сообщать о прохождении без выполнения.

## Сопровождение

Этот файл отвечает за собственные исходники непосредственно в данной папке; для дочерних папок действуют их локальные инструкции и общие правила родителей. После изменения исходников в этой папке сверить и актуализировать здесь ответственность, контракты, зависимости и способ проверки в рамках той же задачи. При переносе файлов обновить описания прежней и новой папок. Не добавлять фиктивный журнал или неподтверждённый результат проверки.

Общая карта и порядок сопровождения: [техническое руководство](../../../../Documentation/TechnicalGuide/12-source-map-and-maintenance.md). Открытые изменения: [план рефакторинга](../../../../Documentation/Plans/project-refactoring-2026-10-13/README.md).
