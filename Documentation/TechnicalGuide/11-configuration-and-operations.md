# Конфигурация и эксплуатация

## Обязательные настройки

[Program.cs](../../CostVision.Web/Program.cs) добавляет обязательный файл `Config/config.json`, используя текущий рабочий каталог. Этот файл добавляется после стандартных источников конфигурации; при одинаковых ключах порядок источников влияет на приоритет. Нельзя считать, что переменная окружения автоматически переопределит значение файла.

| Ключ | Назначение и проверка |
|---|---|
| `ConnectionStrings:MSSql` | Строка подключения; отсутствие приводит к ошибке регистрации |
| `ApiEndpoints:ProverkachekaApiUrl` | Абсолютный адрес внешнего сервиса; ValidateOnStart |
| `ProverkachekaApiToken` | Непустой токен; ValidateOnStart |
| `ForwardedHeaders:KnownProxies` | Непустой список корректных IP; ValidateOnStart |
| `Serilog` | Уровни, обогащение и sinks журнала |
| `DatabaseMaintenance:Operation` | `UpdateExisting` по умолчанию; `InitializeNew` выбирается явно только для первой установки |
| `DatabaseMaintenance:TimeoutSeconds` | Конечный бюджет 1–3600 секунд, default 60 |
| `DatabaseMaintenance:ServerBackupDirectory` | Обязательный абсолютный каталог на SQL Server, без вычисления от каталога Web |
| `DatabaseMaintenance:SingleInitializerConfirmed` | Обязательное `true` после остановки оператором других экземпляров, writes и DDL на всё обслуживание |

Здесь приведены только имена ключей. Значения секретов не входят в документацию. Порядок подстановки конфигурации и лимиты загрузки требуют пересмотра в этапе 14 плана.

## HTTP и reverse proxy

Forwarded headers обрабатываются до authentication: включены X-Forwarded-For, Proto и Host. Доверенные адреса добавляются из Options. Применение этих настроек должно проверяться в конкретной схеме размещения.

Kestrel допускает тело запроса до 1 000 000 000 байт. Это верхняя граница сервера; ограничения формы, прокси и отдельного сценария могут отличаться. Значение нельзя считать гарантированным допустимым размером банковского файла.

## Инициализация базы

[InitializeDatabaseAsync](../../CostVision.Web/Web/Extensions/WebApplicationExtensions.cs) вызывает [DatabaseMaintenanceStartup](../../CostVision.Infrastructure/Services/DataBase/DatabaseMaintenanceStartup.cs). Выбран startup-режим: до `RunAsync` хоста выполняется одна await-операция библиотечного `IDatabaseMaintenance<CostVisionContextKey>` с конечным timeout и ApplicationStopping token. Await включает библиотечный cleanup и DisposeAsync scoped ресурсов; readiness становится true только после обоих. HTTP listener и hosted refresh начинают работу затем при `app.RunAsync()`. При отказе/отмене readiness остаётся false, Program возвращается с ExitCode=1 и асинхронно освобождает WebApplication. Task.Run, внешний WaitAsync и Environment.Exit удалены; повторный startup в том же процессе запрещён.

`UpdateExistingAsync` — обновление существующей БД: inspection и binding EF target, pending, подтверждённый backup, повторный inspection target, migrations и проверка отсутствия pending. No-pending возвращает Unchanged без backup. Создание отсутствующей базы разрешает только явно выбранный `InitializeNewAsync`; ошибки подключения/аутентификации/прав не означают Missing. В частности, SQL 4060 требует master probe с VIEW ANY DATABASE и подтверждения отсутствия sys.databases row. После успешной первой установки оператор меняет Operation обратно на UpdateExisting до следующего запуска; автоматического fallback нет.

Бюджет кооперативный: отмена передаётся driver/EF и проверяется между шагами, но не доказывает остановку зависшего OS/server I/O за фиксированное время. Приложение ждёт cleanup, а не допускает host после прекращения внешнего ожидания. Отказ после dispatch backup/HEADERONLY/VERIFYONLY либо неопределённый migration/cleanup исход отравляет локальный gate; повтор не выполняется. Проверка серверного состояния и recovery принадлежат оператору. Автоматического restore/recovery SQL Server API в библиотеке нет; сценарий автоматического восстановления заблокирован, собственный SQL-обход не создан.

До запуска ConsoleLifetime `StartupCancellation` временно подписывает Ctrl+C и Unix SIGTERM, отменяет связанный token и удерживает ожидание cleanup. Подписки снимаются после возвращения startup. ApplicationStopping также включён в этот token. Реальные Windows/Unix сигналы, принудительное убийство процесса и supervisor shutdown grace period не проверены; grace period должен позволять await cleanup, а внешний kill не подтверждает остановку серверной команды.

## Резервные копии и постоянные каталоги

ServerBackupDirectory задаётся оператором для файловой системы SQL Server. Приложение не создаёт локальную папку и не проверяет этот путь через локальный Directory.Exists. Нужны права SQL service account на каталог, права maintenance user на inspection/backup/migrations, достаточное место и доступность ключей шифрования при restore. Конкретный путь, права и версия реального SQL неизвестны и являются блокерами эксплуатации.

Поддержаны EngineEdition 2/3/4 и стабильный прямой endpoint. Требуются `ConnectRetryCount=0`, отсутствие routing/failover/reconnect, MultiSubnetFailover/Failover Partner, ReadOnly intent, AttachDBFilename, повторяющей EF execution strategy и внешней transaction. Azure SQL Database/MI/Synapse и неподтверждённые topology не поддерживаются. Library gate singleton ограничивает только один root DI container: другие процессы, фоновые и HTTP writers и DDL оператор останавливает до запуска и держит остановленными до завершения обслуживания. Значение SingleInitializerConfirmed документирует эту предпосылку, но не проверяет и не останавливает процессы. Rolling deployment с активными другими экземплярами заблокирован; распределённого lease API нет.

SQL Server-модуль подтверждает COPY_ONLY/CHECKSUM backup, один соответствующий HEADERONLY set и VERIFYONLY с FILE position/CHECKSUM. Receipt не означает успешного restore; требуется реальное операторское восстановление отдельно. Локального hash для server backup не вычисляют. Обычный startup-журнал не раскрывает locator; его retention, доступ и recovery-процедура согласуются в эксплуатационной приёмке.

Data Protection хранит ключи в `keys-windows` или `keys-linux` под content root; имя приложения берётся из entry assembly. Сохранение ключей поддерживает cookie после перезапуска. Для нескольких экземпляров необходима согласованная общая конфигурация key ring.

## Журналирование

Serilog читает настройки из конфигурации, а SimpleClassNameEnricher добавляет короткое имя класса. Web middleware пишет путь и TraceId; фоновые компоненты сохраняют свои действующие логи. Maintenance/startup пишет только закрытые stage/code/outcome. [SafeDatabaseLoggerFactory](../../CostVision.Infrastructure/Services/DataBase/SafeDatabaseLoggerFactory.cs) передаёт EF events как числовой EventCode без исходных SQL, category/scope/state, formatter и exception chains. InnerException библиотеки остаётся диагностическим объектом и не направляется в обычный журнал.

Конкретные sinks, лимиты и срок хранения определяются действующим окружением. Ротация и права записи не проверялись запуском и не считаются подтверждёнными этой документацией. Журнал не должен содержать токены и полные пользовательские документы.

## Контейнер

[Dockerfile](../../CostVision.Web/Dockerfile) использует SDK 10 для сборки и aspnet 10 для итогового образа. На этапе сборки устанавливаются Node/npm, восстанавливаются пакеты, компилируется TypeScript и публикуется Web с SkipFrontendBuild.

Итоговый образ открывает порт 8080, запускает `dotnet CostVision.Web.dll` от appuser с uid 1001 и подготавливает каталоги Config, Logs и keys-linux. SQL Server в этот образ не входит.

Config и key ring требуют корректных mounts и прав. Объявленный EXPOSE не подтверждает фактическую маршрутизацию трафика. Сборка образа, запуск контейнера, восстановление backup и deployment в этой задаче не выполнялись.
