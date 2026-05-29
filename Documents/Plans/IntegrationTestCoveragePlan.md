# План integration-тестирования CostVision

## Границы integration-тестов

Integration-тесты проверяют сценарии, где важны настоящие границы инфраструктуры: EF Core, схема БД, HTTP pipeline, Razor Pages routing, cookie/claims, файловая система, hosted service lifecycle и работа с реальными файлами.

Для внешних API использовать тестовые адаптеры, фикстуры или мок HTTP-слоя. Не ходить в реальные внешние сервисы без отдельного явного решения.

Если пункт плана не выполнен, пропущен или признан неприменимым, рядом с ним обязательно указывать короткий комментарий с причиной.

## EF Core и база данных

- `CostVision.Infrastructure/DataBase/Repositories/*`
  - [x] CRUD и query-поведение репозиториев на тестовой БД или SQLite/in-memory provider;
  - [x] корректность `Include`/`ThenInclude` в сценариях, где это важно для вызывающего кода;
  - [x] поведение уникальных индексов и ограничений схемы.

- EF migrations и model configuration
  - [x] схема создаётся из миграций;
  - [x] ограничения длины, обязательность полей и индексы соответствуют модели;
  - [x] миграции применяются на пустую тестовую БД.

- `CostVision.Application/UseCases/Receipts/Receipts/DeleteReceiptUseCase.cs`
  - [x] связанные сущности удаляются или сохраняются согласно текущей EF-конфигурации каскадов.

## Web и авторизация

- `CostVision/Pages/Login.cshtml.cs`
  - [x] успешный логин устанавливает cookie и claims;
  - [x] неверный логин/пароль не устанавливает авторизационную cookie;
  - [x] заблокированный пользователь не получает авторизационную cookie.

- Razor Pages routing и полный HTTP-поток
  - [x] защищённые страницы требуют авторизации;
  - [x] AJAX endpoints возвращают ожидаемые HTTP-статусы и JSON;
  - [x] `ServiceResult` корректно мапится в HTTP-ответы в реальном pipeline.

- `CostVision/Middleware/ExceptionHandlingMiddleware.cs`
  - [x] необработанные исключения мапятся в ожидаемый HTTP-ответ;
  - [x] логирование срабатывает без разрыва pipeline.

## Component-тесты инфраструктуры

- `CostVision.Infrastructure/Services/MoneyMovements/BankStatementPdfTextExtractor.cs`
  - [ ] чтение PDF-файла с тестовой выпиской;
  - [ ] обработка повреждённого или неподдерживаемого PDF;
  - [ ] корректная ошибка при пустом файле.

- `CostVision.Infrastructure/Services/DataBase/BackupService.cs`
  - [ ] построение имени и пути backup-файла;
  - [ ] обработка ошибок файловой системы;
  - [ ] поведение при некорректной строке подключения.

- `CostVision.Infrastructure/Services/DataBase/DataBaseCheckUpService.cs`
  - [ ] проверка доступности БД;
  - [ ] применение pending migrations в тестовой среде;
  - [ ] логирование результата проверки.

- `CostVision.Infrastructure/Services/BackgroundServices/ReceiptRefreshBackgroundService.cs`
  - [ ] hosted service запускает refresh workflow по расписанию;
  - [ ] ошибки refresh не завершают lifecycle сервиса;
  - [ ] отмена через `CancellationToken` корректно останавливает работу.

## Frontend/component/e2e

- Razor `.cshtml` и TypeScript UI
  - [ ] основные пользовательские сценарии через браузер;
  - [ ] AJAX-действия проходят без полной перезагрузки страницы;
  - [ ] ошибки backend отображаются в UI ожидаемым способом;
  - [ ] формы авторизации, пользователей, чеков, счетов и движений денег проходят smoke-сценарии.
