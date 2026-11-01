# Конфигурация и эксплуатация

## Обязательные настройки

[ConfigurationExtensions](../../CostVision.Web/Web/Extensions/ConfigurationExtensions.cs), вызываемый Program, добавляет обязательный `Config/config.json` по абсолютному пути от `builder.Environment.ContentRootPath`, сохраняя base path остальных providers. Приоритет от меньшего к большему: стандартные providers builder (appsettings/environment-specific JSON, development user secrets, стандартные environment/command line), затем config.json, затем повторно environment без префикса, затем command line. Для вложенных ключей environment использует `__`. Файл не перезагружается; maintenance options целиком захватываются при DI-регистрации и требуют перезапуска.

Content root выбирает ASP.NET Core при создании builder; явный host content-root через deployment configuration/command line должен указывать на каталог приложения. Без явного выбора стандартный builder обычно использует начальный cwd: переход к content-root пути не определяет за оператора каталог production. После создания builder поиск config.json не зависит от дальнейшего изменения cwd. Наличие секрета в environment/command line не разрешает обход отсутствующего обязательного файла. Изолированные проверки используют отдельный root и тестовые значения; реальные настройки не читались/не публиковались.

| Ключ | Назначение и проверка |
|---|---|
| `ConnectionStrings:MSSql` | Непустая строка подключения; отсутствие/пробелы приводят к ошибке регистрации. ConnectRetryCount необязателен: сохраняется явное значение либо стандартное значение драйвера 1 для обычного MSSQL |
| `ApiEndpoints:ProverkachekaApiUrl` | Абсолютный HTTP(S) адрес внешнего сервиса; ValidateOnStart |
| `ProverkachekaApiToken` | Непустой токен; ValidateOnStart |
| `ForwardedHeaders:KnownProxies` | Непустой список корректных IP; ValidateOnStart |
| `Serilog` | Уровни, обогащение и sinks журнала |
| `DatabaseMaintenance:Operation` | `UpdateExisting` по умолчанию; `InitializeNew` выбирается явно только для первой установки |
| `DatabaseMaintenance:TimeoutSeconds` | Конечный бюджет 1–3600 секунд, default 60 |
| `DatabaseMaintenance:ServerBackupDirectory` | Обязательный каталог на SQL Server из конфигурации; отсутствие и пустое значение запрещены, пути по умолчанию в коде нет |
| `ExternalRequests:TimeoutSeconds` | Default180; конечное положительное значение, представимое в TimeSpan и HttpClient.Timeout (до int.MaxValue миллисекунд) |
| `BankStatementPdf:MaxBytes` | Положительное число, согласованный default20 МиБ = 20 971 520 байт; только банковские выписки |
| `BankStatementPdf:MaxPages` | Положительное число, согласованный default200; только банковские выписки |
| `Uploads:MaxRequestBodyBytes` | Положительное число; прежний общий Kestrel default1 000 000 000 байт |
| `DataProtection:KeyDirectory` | Пустой default выбирает прежние keys-windows/keys-linux от content root; относительный override также от content root, абсолютный сохраняется |
| `DataProtection:ApplicationName` | Непустой discriminator, defaultCostVision.Web соответствует AssemblyName; одинаковый для совместимых экземпляров |
| `Swagger:Enabled` | Defaulttrue сохраняет прежние все среды; явный false отключает Swagger JSON/UI |

Параметры MaintenanceExecutionMode и SingleInitializerConfirmed удалены. Из секции DatabaseMaintenance привязываются только Operation, TimeoutSeconds и ServerBackupDirectory. Единственного исполнителя обслуживания обеспечивает оператор: другие экземпляры, записи и DDL должны быть остановлены на время обслуживания. Внутренний gate библиотеки не создаёт межпроцессную блокировку.

Значения секретов не входят в документацию. Прежде чем вызывать maintenance, Web явно выполняет `IStartupValidator.Validate()` всех зарегистрированных модулей; одного отложенного ValidateOnStart при RunAsync недостаточно. Неверные options не допускают SQL/HTTP/hosted startup. Registration/composition/options failures дают закрытый startup code без exception chains, connection string и server paths. Проверки остаются изолированными; реальное обслуживание и права SQL Server не проверены.

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

ServerBackupDirectory задаётся в конфигурации для файловой системы SQL Server. В локальном `CostVision.Web/Config/config.json` выбран `C:\DISTR\backups`; в коде этот путь не закреплён. Приложение не создаёт локальную папку и не проверяет этот путь через локальный Directory.Exists. Нужны права SQL service account на каталог, права maintenance user на inspection/backup/migrations, достаточное место и доступность ключей шифрования при restore. Наличие выбранного каталога, права и версия реального SQL не проверены.

Поддержаны EngineEdition 2/3/4 и стабильный прямой endpoint. ApplicationContext получает строку подключения без изменения ConnectRetryCount; EF и библиотечный maintenance используют общий контекст. Для обычного MSSQL отсутствие ключа сохраняет стандартное значение драйвера 1; явное допустимое значение, в том числе 0, сохраняется. Это восстановление соединения, а не повтор SQL-команды или миграции; сохранение прежней серверной сессии не гарантируется. MultiSubnetFailover/Failover Partner, ReadOnly intent, AttachDBFilename, повторяющая EF execution strategy и внешняя transaction запрещены. Azure SQL Database/MI/Synapse и неподтверждённые topology не поддерживаются. Library gate singleton ограничивает только один root DI container: другие процессы, фоновые и HTTP writers и DDL оператор останавливает до запуска и держит остановленными до завершения обслуживания. Rolling deployment с активными другими экземплярами заблокирован; распределённого lease API нет.

SQL Server-модуль получает ProjectName="CostVision" и создаёт `backup_CostVision_dd.MM.yyyy_HHmmssfff.bak` с UTC-временем. До BACKUP batch в master берёт session applock на путь и проверяет через sys.dm_os_file_exists отсутствие файла и наличие родителя; занятый либо неподтверждённый путь вызывает отказ. Нужны соответствующие права в master; доступность и права реального сервера не проверены. Модуль подтверждает COPY_ONLY/CHECKSUM backup, один соответствующий HEADERONLY set и VERIFYONLY с FILE position/CHECKSUM. Receipt не означает успешного restore; требуется реальное операторское восстановление отдельно. Локального hash для server backup не вычисляют. Обычный startup-журнал не раскрывает locator; его retention, доступ и recovery-процедура согласуются в эксплуатационной приёмке.

Data Protection по умолчанию хранит ключи в прежних `keys-windows`/`keys-linux` под content root с явным стабильным discriminator `CostVision.Web`. Web runtime options настраивают FileSystemXmlRepository без создания каталога при регистрации. Его запись/создание происходят при реальном использовании Data Protection; нужны постоянный mount и права process identity на чтение/запись. Изменение discriminator или потеря ring делает прежние cookie несовместимыми. Encryption at rest, backup ключей и права отдельно проверяет оператор; эта правка не включает новый механизм шифрования. Для совместимых экземпляров необходим один ring/discriminator, но это не снимает блокер active multi-instance maintenance.

Собственных API-контроллеров в текущем дереве не обнаружено; приложение использует Razor handlers. AddControllers/MapControllers и Swagger сохранены для совместимости, документ v1 теперь назван CostVision API. Swagger не является health/readiness endpoint и не подтверждает доступность бизнес-сценариев. Политика экспозиции по умолчанию не ужесточалась; отключение выбирает оператор через Enabled.

## Журналирование

Serilog читает настройки из конфигурации, а SimpleClassNameEnricher добавляет короткое имя класса. Web middleware пишет путь и TraceId; фоновые компоненты сохраняют свои действующие логи. Maintenance/startup пишет только закрытые stage/code/outcome. [SafeDatabaseLoggerFactory](../../CostVision.Infrastructure/Services/DataBase/SafeDatabaseLoggerFactory.cs) передаёт EF events как числовой EventCode без исходных SQL, category/scope/state, formatter и exception chains. InnerException библиотеки остаётся диагностическим объектом и не направляется в обычный журнал.

Конкретные sinks, лимиты и срок хранения определяются действующим окружением. Ротация и права записи не проверялись запуском и не считаются подтверждёнными этой документацией. Журнал не должен содержать токены и полные пользовательские документы.

В appsettings.json для полной категории `CostVision.Infrastructure.Services.DataBase.SafeDatabaseLoggerFactory` установлен Serilog override `Warning`: информационные EF events скрыты в консоли и файле, предупреждения и ошибки сохраняются. Фильтр `Microsoft.EntityFrameworkCore` отдельно не покрывает события безопасного адаптера. Итоговые сообщения maintenance/startup остаются на своих категориях и не отключаются этим override.

## Контейнер

[Dockerfile](../../CostVision.Web/Dockerfile) закрепляет SDK10.0.401, aspnet10.0.12 и отдельный Node22.18.0-bookworm-slim build-platform stage с проверкой npm10.9.3. До явного restore копируются libs/global.json/NuGet.Config; npm ci явно отключает lifecycle scripts/audit/fund. TypeScript компилируется до publish; publish использует no-restore/SkipFrontendBuild и копируется целиком. Node в final stage не копируется. Это статически проверенный порядок; Docker/publish/Linux/ARM64 не запускались, tags не immutable digests.

Итоговый образ открывает порт 8080, запускает `dotnet CostVision.Web.dll` от appuser с uid 1001 и подготавливает каталоги Config, Logs и keys-linux. SQL Server в этот образ не входит.

CD workflow полностью удалён15.10.2026 по решению пользователя; автоматического deployment нет. Для будущего отдельно разрешённого deployment сохранить mounts `costVision_Config:/app/Config`, `costVision_Logs:/app/Logs`, `costvision_Keys:/app/keys-linux`, uid1001/права и discriminator. Сначала требуется отдельная приёмка SingleInitializer, actual restore, совместимости schema/data/artifact и readiness. Замена контейнера не откатывает схему. EXPOSE не подтверждает маршрутизацию. Сборка/запуск образа, восстановление backup и deployment не выполнялись; порядок проверки — [матрица15](../Plans/project-refactoring-2026-10-13/15-acceptance-matrix.md).
