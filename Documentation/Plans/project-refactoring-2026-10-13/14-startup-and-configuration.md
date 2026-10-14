# Этап 14. Запуск и конфигурация

Статус: **Не начат**.
Приоритет: **П2**.
Зависимости: 06, 08, 08A, 09.

Переход на актуальные base/Maintenance/SqlServer, замена собственного backup/checkup и правила единого доступа к БД принадлежат [этапу 08A](08a-efcorelibrary-upgrade-and-maintenance.md). Ниже исходные проблемы сохранены как основание; после 08A этот этап завершает options/configuration и эксплуатационную приёмку, не создавая второй SQL/backup/migration путь.

## Найденные проблемы

1. [Program.cs](../../../CostVision.Web/Program.cs) добавляет обязательный `Config/config.json` после стандартных providers builder. При совпадающих ключах этот файл получает приоритет над ранее добавленными environment/command-line providers. Путь зависит от текущего рабочего каталога, а не явно от content root.
2. Общий лимит Kestrel — 1 ГБ; application-проверка preview принимает `FileLength`, но использует его только для отсутствия файла. [BankStatementPdfTextExtractor](../../../CostVision.Infrastructure/Services/MoneyMovements/BankStatementPdfTextExtractor.cs) синхронно открывает документ и читает все страницы. Cancellation проверяется только при обходе страниц; максимальный объём/число страниц и malformed PDF как ожидаемая ошибка не определены.
3. [WebApplicationExtensions](../../../CostVision.Web/Web/Extensions/WebApplicationExtensions.cs) запускает синхронный [DataBaseCheckUpService](../../../CostVision.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs) через `Task.Run` с `WaitAsync(60 секунд)`. Истечение ожидания не отменяет backup/migration; применяется `Environment.Exit`. Runtime SQL-user требует прав backup и изменения схемы.
4. [BackupService](../../../CostVision.Infrastructure/Services/DataBase/BackupService.cs) вручную собирает SQL с именем БД и путём, использует синхронные вызовы и не имеет собственного управляемого cancellation/timeout-контракта. Путь к backup относится к SQL Server, тогда как создание каталога выполняется в процессе приложения; для удалённого SQL эти файловые системы могут различаться.
5. Startup/backup path, HTTP timeout и расписание распределены между константами и DI. URL проверяется на абсолютность, но не на допустимый HTTP(S)-протокол. Data Protection и инфраструктура нуждаются в явном контракте путей/прав/единого key ring для нескольких экземпляров.
6. Swagger включён во всех средах с общим `My API`, при отсутствии отдельного описанного назначения контроллеров. Это область упрощения конфигурации, не доказанная утечка защищённых данных.

## План работ

- Описать порядок providers и использовать стабильный путь конфигурации; проверить перекрытие значений без вывода секретов.
- Ввести валидируемые options для загрузки файлов, PDF, startup и внешних запросов. Отклонять превышение до дорогостоящего разбора; ошибки PDF классифицировать на границе.
- Опираясь на 08A, отделить проверку готовности БД от обновления схемы либо явно оставить принятую автоматическую политику с корректным lifecycle через `IDatabaseMaintenance<TKey>.InspectAsync/UpdateExistingAsync`. Первую установку выполнять только явно выбранным `InitializeNewAsync`; ошибка подключения не разрешает создание БД. Изменение startup-policy согласовать; правило «миграции создаёт пользователь» не означает запрет применения уже созданных миграций библиотечным coordinator.
- Через валидируемые options передать `SqlServerMaintenanceOptions.ServerBackupDirectory`, конечный timeout и cancellation; проверить права, прямой endpoint, `ConnectRetryCount=0` и ограничения provider из 08A. Не исправлять старый BackupService новым ручным SQL: backup/receipt/target/migrations принадлежат библиотеке. SQL в этом этапе не выполнять без прямой просьбы.
- Согласовать readiness и порядок запуска HTTP/фонового refresh с SingleInitializer: другие экземпляры, writes и DDL остановлены на весь maintenance. Локальный gate не является распределённой блокировкой; неподдержанный контур — явный блокер. Не завершать только внешнее ожидание, оставляя SQL/cleanup работать в уже освобождённом scope.
- Сохранить fail-fast, ValidateScopes/ValidateOnBuild, Options ValidateOnStart и доверенные reverse proxies. Не подставлять фиктивную конфигурацию для успешного запуска.

## Проверки и приёмка

- [ ] Приоритет конфигурации и обязательные options проверены без раскрытия значений секретов.
- [ ] Большой/повреждённый PDF даёт ограниченный по ресурсам понятный отказ.
- [ ] Timeout startup не объявляется отменой продолжающейся операции.
- [ ] Backup path и права проверены в реальном разрешённом контуре отдельно.
- [ ] Выбранная политика migrations и нескольких экземпляров документирована; миграции создаёт пользователь.
- [ ] DI/options и startup используют поставку 08A и один maintenance coordinator; собственные SqlConnection/backup SQL/прямой Migrate не возвращены.
- [ ] SingleInitializer и readiness до допуска фоновых/HTTP writes проверены отдельно; логи maintenance не раскрывают секреты, пути или provider exception chains.

## Журнал выполнения

Дата начала: не назначена. Реализация и проверки: нет. 14.10.2026 план согласован с новым этапом 08A: сначала обновление библиотечной границы, затем общие options/configuration и эксплуатационная приёмка. SQL, startup, backup и deployment не запускались. Следующий шаг: завершить зависимости и согласовать эксплуатационный контракт.
