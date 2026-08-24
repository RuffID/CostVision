# Web, данные, интеграции и эксплуатация

## Razor Pages и AJAX

Веб-интерфейс состоит из server-rendered Razor Pages и именованных JSON handlers. PageModel не хранит бизнес-решения: получает request, текущего пользователя, вызывает use case и передаёт `ServiceResult` в [JsonResultMapper](../../CostVision.Web/Web/Mappers/JsonResultMapper.cs).

Единый JSON-контракт:

- успех без данных: `{ success: true }`;
- успех с данными: `{ success: true, data: ... }`;
- ошибка: `{ success: false, message: ... }`.

HTTP mapping: 400 validation, 401 unauthorized, 403 forbidden, 404 not found, 409 conflict, 502 external service. Неизвестный `ServiceErrorType` считается ошибкой программирования.

## Страницы и handlers

- [IndexModel](../../CostVision.Web/Pages/Index.cshtml.cs): доступные счета и dashboard report.
- [AddReceiptModel](../../CostVision.Web/Pages/AddReceipt.cshtml.cs): счета для создания, QR batch и manual receipt.
- [ReceiptsModel](../../CostVision.Web/Pages/Receipts.cshtml.cs): список, карточка, refresh, delete, account move/remove и reconciliation.
- [MoneyMovementsModel](../../CostVision.Web/Pages/MoneyMovements.cshtml.cs): CRUD операций, кандидаты/связи и двухшаговый PDF import.
- [ProductsModel](../../CostVision.Web/Pages/Products.cshtml.cs): каталог, adaptive name и покупки.
- [StoresModel](../../CostVision.Web/Pages/Stores.cshtml.cs): каталог, чеки магазина и те же receipt actions.
- [UserSettingsModel](../../CostVision.Web/Pages/Settings/UserSettings.cshtml.cs): счета и участники.
- [UsersModel](../../CostVision.Web/Pages/Settings/Users.cshtml.cs): admin-only пользователи и роли.

`Login` и `Logout` управляют cookie, `UserActivity` принимает heartbeat. Swagger и controllers включены, но текущие продуктовые действия реализованы handlers Razor Pages.

## TypeScript

Исходники находятся в `CostVision.Web/wwwroot/ts`, результат — generated `wwwroot/dist`. [tsconfig.json](../../CostVision.Web/tsconfig.json) включает `strict`, `noEmitOnError`, DOM/ES2020, ES modules и source maps.

Общие модули `shared/http.ts`, `shared/bootstrap.ts`, formatters и validation переиспользуются страницами. Запросы отправляются через `sendJsonRequest`; изменяющие запросы получают antiforgery headers. Динамический интерфейс строится DOM API, а общий Bootstrap helper управляет модальными окнами.

`npm run build:ts` запускает TypeScript compiler. MSBuild перед обычной сборкой при необходимости выполняет `npm ci`, затем frontend build; `/p:SkipFrontendBuild=true` отключает эту часть для контролируемого повторного publish.

## EF Core и таблицы

[ApplicationContext](../../CostVision.Infrastructure/DataBase/ApplicationContext.cs) применяет отдельные configurations для всех сущностей. Основные таблицы:

- доступ: `Users`, `Roles`, `UserRoles`;
- бюджеты: `Accounts`, `AccountMembers`;
- чеки: `Receipts`, `ReceiptItems`, `ReceiptAccounts`, `Stores`, `Products`, `ExpenseCategories`;
- деньги: `MoneyMovements`, `MoneyMovementReceipts`.

GUID создаются через `NEWSEQUENTIALID()`. Денежные поля имеют precision `18,2`, количество позиции — `18,3`. Важные unique constraints: login, role name, normalized product name, normalized store name/address, пользовательская фискальная идентичность чека и имя категории внутри пользователя.

Cascade delete используется для зависимых link/item записей. `Restrict` защищает пользователей, магазины, товары и аудит от неявного удаления. Миграции находятся в `CostVision.Infrastructure/DataBase/Migrations` и являются generated history.

## Репозитории и запросы

[UnitOfWork](../../CostVision.Infrastructure/DataBase/Repositories/UnitOfWork.cs) агрегирует тонкие CRUD/query repositories. Use case передаёт predicate, tracking mode и `Include`/`AsSplitQuery`; специализированная бизнес-выборка не прячется внутри репозитория. Read-only списки используют `AsNoTracking`, изменения — tracking entities.

`ExecuteInTransaction` открывает database transaction, выполняет action, вызывает `SaveChangesAsync` и commit. При exception выполняется rollback и ошибка пробрасывается выше.

## Конфигурация

Помимо стандартных ASP.NET Core sources [Program.cs](../../CostVision.Web/Program.cs) обязательно читает `CostVision.Web/Config/config.json`. Нужны ключи:

| Ключ | Назначение |
|---|---|
| `ConnectionStrings:MSSql` | SQL Server connection string |
| `ApiEndpoints:ProverkachekaApiUrl` | абсолютный URL API чеков |
| `ProverkachekaApiToken` | токен внешнего API |
| `ForwardedHeaders:KnownProxies` | доверенные reverse proxies |
| `Serilog` | уровни и sinks |

URL и token валидируются на старте. Секретные значения не должны попадать в документацию, логи или репозиторий; production должен подставлять их защищённым конфигурационным каналом.

## Startup БД и backup

[InitializeDatabaseAsync](../../CostVision.Web/Web/Extensions/WebApplicationExtensions.cs) даёт проверке базы 60 секунд. [DataBaseCheckUpService](../../CostVision.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs):

1. проверяет `CanConnect`;
2. получает pending migrations;
3. при их наличии создаёт SQL Server backup;
4. вызывает `Database.Migrate()`.

На Windows backup пишется в `Backups` рядом с приложением, на Linux — `/var/opt/mssql/backups`. Linux-путь видит SQL Server, поэтому он должен существовать и быть доступен серверу БД. Backup использует `WITH FORMAT, INIT`; миграция без успешного backup не начинается.

## Логи, файлы и ограничения процесса

Serilog пишет консоль и ежедневные `Logs/log_.txt`, хранит до 31 файла и переключает файл при достижении лимита размера. Data Protection key ring требует права записи в `keys-windows`/`keys-linux`. Фоновый refresh живёт внутри web-процесса; при остановке следующий запуск будет рассчитан после старта.

Kestrel разрешает тело запроса до 1 ГБ, что относится прежде всего к загрузкам. Reverse proxy должен иметь согласованный, желательно более строгий лимит.

## Docker

[Dockerfile](../../CostVision.Web/Dockerfile) использует multi-stage restore/build/publish. Build stage устанавливает Node/npm, выполняет `npm ci` и TypeScript build. Финальный образ:

- основан на `aspnet:10.0`;
- слушает порт 8080;
- запускается непривилегированным `appuser` uid 1001;
- заранее создаёт `/app/Config`, `/app/Logs`, `/app/keys-linux`;
- запускает `dotnet CostVision.Web.dll`.

Config, логи, key ring и необходимые постоянные каталоги следует монтировать снаружи. SQL Server не входит в этот образ.
