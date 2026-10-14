# Конфигурация и эксплуатация

## Обязательные настройки

[ConfigurationExtensions](../../CostVision.Web/Web/Extensions/ConfigurationExtensions.cs), вызываемый Program, добавляет обязательный `Config/config.json` по абсолютному пути от `builder.Environment.ContentRootPath`, сохраняя base path остальных providers. Приоритет от меньшего к большему: стандартные providers builder (appsettings/environment-specific JSON, development user secrets, стандартные environment/command line), затем config.json, затем повторно environment без префикса, затем command line. Для вложенных ключей environment использует `__`. Файл не перезагружается; maintenance options целиком захватываются при DI-регистрации и требуют перезапуска.

Content root выбирает ASP.NET Core при создании builder; явный host content-root через deployment configuration/command line должен указывать на каталог приложения. Без явного выбора стандартный builder обычно использует начальный cwd: переход к content-root пути не определяет за оператора каталог production. После создания builder поиск config.json не зависит от дальнейшего изменения cwd. Наличие секрета в environment/command line не разрешает обход отсутствующего обязательного файла. Изолированные проверки используют отдельный root и тестовые значения; реальные настройки не читались/не публиковались.

| Ключ | Назначение и проверка |
|---|---|
| `ConnectionStrings:MSSql` | Непустая строка подключения; отсутствие/пробелы приводят к ошибке регистрации |
| `ApiEndpoints:ProverkachekaApiUrl` | Абсолютный HTTP(S) адрес внешнего сервиса; ValidateOnStart |
| `ProverkachekaApiToken` | Непустой токен; ValidateOnStart |
| `ForwardedHeaders:KnownProxies` | Непустой список корректных IP; ValidateOnStart |
| `Serilog` | Уровни, обогащение и sinks журнала |
| `DatabaseMaintenance:Operation` | `UpdateExisting` по умолчанию; `InitializeNew` выбирается явно только для первой установки |
| `DatabaseMaintenance:TimeoutSeconds` | Конечный бюджет 1–3600 секунд, default 60 |
| `DatabaseMaintenance:ServerBackupDirectory` | Обязательный абсолютный каталог на SQL Server, без вычисления от каталога Web |
| `DatabaseMaintenance:SingleInitializerConfirmed` | Обязательное `true` после остановки оператором других экземпляров, writes и DDL на всё обслуживание |
| `ExternalRequests:TimeoutSeconds` | Default180; конечное положительное значение, представимое в TimeSpan и HttpClient.Timeout (до int.MaxValue миллисекунд) |
| `BankStatementPdf:MaxBytes` | Положительное число, согласованный default20 МиБ = 20 971 520 байт; только банковские выписки |
| `BankStatementPdf:MaxPages` | Положительное число, согласованный default200; только банковские выписки |
| `Uploads:MaxRequestBodyBytes` | Положительное число; прежний общий Kestrel default1 000 000 000 байт |
| `DataProtection:KeyDirectory` | Пустой default выбирает прежние keys-windows/keys-linux от content root; относительный override также от content root, абсолютный сохраняется |
| `DataProtection:ApplicationName` | Непустой discriminator, defaultCostVision.Web соответствует AssemblyName; одинаковый для совместимых экземпляров |
| `Swagger:Enabled` | Defaulttrue сохраняет прежние все среды; явный false отключает Swagger JSON/UI |

Значения секретов не входят в документацию. Прежде чем вызывать maintenance, Web явно выполняет `IStartupValidator.Validate()` всех зарегистрированных модулей; одного отложенного ValidateOnStart при RunAsync недостаточно. Неверные options не допускают SQL/HTTP/hosted startup. Registration/composition/options failures дают закрытый startup code без exception chains, connection string и server paths. Реальные значения SingleInitializer/каталога/прав не подставлялись для успешной проверки.

## HTTP и reverse proxy

Forwarded headers обрабатываются до authentication: включены X-Forwarded-For, Proto и Host. Доверенные адреса добавляются из Options. Применение этих настроек должно проверяться в конкретной схеме размещения.

Kestrel допускает тело запроса до 1 000 000 000 байт. Это верхняя граница сервера; ограничения формы, прокси и отдельного сценария могут отличаться. Значение нельзя считать гарантированным допустимым размером банковского файла.

Прежние framework multipart/form defaults и proxy limits сохраняются; размер PDF проверяется по фактическому оставшемуся потоку, а не только по FileLength. Seekable oversize отклоняется до чтения; non-seekable буферизуется максимум до лимита+1 до PdfPig. Число страниц проверяется до извлечения текста. Документ не обрезается до первых допустимых страниц. Отказы лимитов/известного формата/защиты становятся безопасным Validation400 в preview; неизвестный I/O/ошибка и отмена не маскируются. Caller stream освобождает Web. Синхронные PdfPig open/page/decompression нельзя прервать внутри вызова; byte/page caps не гарантируют максимальные CPU/память/время разбора. Новых decoder/time limits для QR/camera/storage нет.

## Инициализация базы

[InitializeDatabaseAsync](../../CostVision.Web/Web/Extensions/WebApplicationExtensions.cs) вызывает [DatabaseMaintenanceStartup](../../CostVision.Infrastructure/Services/DataBase/DatabaseMaintenanceStartup.cs). Выбран startup-режим: до `RunAsync` хоста выполняется одна await-операция библиотечного `IDatabaseMaintenance<CostVisionContextKey>` с конечным timeout и ApplicationStopping token. Await включает библиотечный cleanup и DisposeAsync scoped ресурсов; readiness становится true только после обоих. HTTP listener и hosted refresh начинают работу затем при `app.RunAsync()`. При отказе/отмене readiness остаётся false, Program возвращается с ExitCode=1 и асинхронно освобождает WebApplication. Task.Run, внешний WaitAsync и Environment.Exit удалены; повторный startup в том же процессе запрещён.

`UpdateExistingAsync` — обновление существующей БД: inspection и binding EF target, pending, подтверждённый backup, повторный inspection target, migrations и проверка отсутствия pending. No-pending возвращает Unchanged без backup. Создание отсутствующей базы разрешает только явно выбранный `InitializeNewAsync`; ошибки подключения/аутентификации/прав не означают Missing. В частности, SQL 4060 требует master probe с VIEW ANY DATABASE и подтверждения отсутствия sys.databases row. После успешной первой установки оператор меняет Operation обратно на UpdateExisting до следующего запуска; автоматического fallback нет.

Бюджет кооперативный: отмена передаётся driver/EF и проверяется между шагами, но не доказывает остановку зависшего OS/server I/O за фиксированное время. Приложение ждёт cleanup, а не допускает host после прекращения внешнего ожидания. Отказ после dispatch backup/HEADERONLY/VERIFYONLY либо неопределённый migration/cleanup исход отравляет локальный gate; повтор не выполняется. Проверка серверного состояния и recovery принадлежат оператору. Автоматического restore/recovery SQL Server API в библиотеке нет; сценарий автоматического восстановления заблокирован, собственный SQL-обход не создан.

До запуска ConsoleLifetime `StartupCancellation` временно подписывает Ctrl+C и Unix SIGTERM, отменяет связанный token и удерживает ожидание cleanup. Подписки снимаются после возвращения startup. ApplicationStopping также включён в этот token. Реальные Windows/Unix сигналы, принудительное убийство процесса и supervisor shutdown grace period не проверены; grace period должен позволять await cleanup, а внешний kill не подтверждает остановку серверной команды.

## Резервные копии и постоянные каталоги

ServerBackupDirectory задаётся оператором для файловой системы SQL Server. Приложение не создаёт локальную папку и не проверяет этот путь через локальный Directory.Exists. Нужны права SQL service account на каталог, права maintenance user на inspection/backup/migrations, достаточное место и доступность ключей шифрования при restore. Конкретный путь, права и версия реального SQL неизвестны и являются блокерами эксплуатации.

Поддержаны EngineEdition 2/3/4 и стабильный прямой endpoint. Требуются `ConnectRetryCount=0`, отсутствие routing/failover/reconnect, MultiSubnetFailover/Failover Partner, ReadOnly intent, AttachDBFilename, повторяющей EF execution strategy и внешней transaction. Azure SQL Database/MI/Synapse и неподтверждённые topology не поддерживаются. Library gate singleton ограничивает только один root DI container: другие процессы, фоновые и HTTP writers и DDL оператор останавливает до запуска и держит остановленными до завершения обслуживания. Значение SingleInitializerConfirmed документирует эту предпосылку, но не проверяет и не останавливает процессы. Rolling deployment с активными другими экземплярами заблокирован; распределённого lease API нет.

SQL Server-модуль подтверждает COPY_ONLY/CHECKSUM backup, один соответствующий HEADERONLY set и VERIFYONLY с FILE position/CHECKSUM. Receipt не означает успешного restore; требуется реальное операторское восстановление отдельно. Локального hash для server backup не вычисляют. Обычный startup-журнал не раскрывает locator; его retention, доступ и recovery-процедура согласуются в эксплуатационной приёмке.

Data Protection по умолчанию хранит ключи в прежних `keys-windows`/`keys-linux` под content root с явным стабильным discriminator `CostVision.Web`. Web runtime options настраивают FileSystemXmlRepository без создания каталога при регистрации. Его запись/создание происходят при реальном использовании Data Protection; нужны постоянный mount и права process identity на чтение/запись. Изменение discriminator или потеря ring делает прежние cookie несовместимыми. Encryption at rest, backup ключей и права отдельно проверяет оператор; эта правка не включает новый механизм шифрования. Для совместимых экземпляров необходим один ring/discriminator, но это не снимает блокер active multi-instance maintenance.

Собственных API-контроллеров в текущем дереве не обнаружено; приложение использует Razor handlers. AddControllers/MapControllers и Swagger сохранены для совместимости, документ v1 теперь назван CostVision API. Swagger не является health/readiness endpoint и не подтверждает доступность бизнес-сценариев. Политика экспозиции по умолчанию не ужесточалась; отключение выбирает оператор через Enabled.

## Журналирование

Serilog читает настройки из конфигурации, а SimpleClassNameEnricher добавляет короткое имя класса. Web middleware пишет путь и TraceId; фоновые компоненты сохраняют свои действующие логи. Maintenance/startup пишет только закрытые stage/code/outcome. [SafeDatabaseLoggerFactory](../../CostVision.Infrastructure/Services/DataBase/SafeDatabaseLoggerFactory.cs) передаёт EF events как числовой EventCode без исходных SQL, category/scope/state, formatter и exception chains. InnerException библиотеки остаётся диагностическим объектом и не направляется в обычный журнал.

Конкретные sinks, лимиты и срок хранения определяются действующим окружением. Ротация и права записи не проверялись запуском и не считаются подтверждёнными этой документацией. Журнал не должен содержать токены и полные пользовательские документы.

## Контейнер

[Dockerfile](../../CostVision.Web/Dockerfile) использует SDK 10 для сборки и aspnet 10 для итогового образа. На этапе сборки устанавливаются Node/npm, восстанавливаются пакеты, компилируется TypeScript и публикуется Web с SkipFrontendBuild.

Итоговый образ открывает порт 8080, запускает `dotnet CostVision.Web.dll` от appuser с uid 1001 и подготавливает каталоги Config, Logs и keys-linux. SQL Server в этот образ не входит.

Config и key ring требуют корректных mounts и прав. Объявленный EXPOSE не подтверждает фактическую маршрутизацию трафика. Сборка образа, запуск контейнера, восстановление backup и deployment в этой задаче не выполнялись.
