# Web-слой и HTTP-контракты

## Путь пользовательского действия

Razor-страница отдаёт каркас интерфейса, клиентский модуль загружает данные через именованный обработчик. PageModel принимает запрос, получает текущего пользователя, вызывает сценарий Application и преобразует результат в JSON. Финансовые права должны проверяться внутри сценария независимо от видимости кнопок.

[Program.cs](../../CostVision.Web/Program.cs) включает forwarded headers, единственный ExceptionHandlingMiddleware, проверку базы, Swagger, статические файлы, маршрутизацию, аутентификацию и авторизацию; затем отображает Razor Pages и controllers. Второй production UseExceptionHandler удалён в 09. Startup-БД не менялся и не запускался для проверки HTTP-контракта.

## Карта страниц

| Страница | Ответственность |
|---|---|
| [Index](../../CostVision.Web/Pages/Index.cshtml.cs) | Доступные счета и отчёт доходов/расходов |
| [AddReceipt](../../CostVision.Web/Pages/AddReceipt.cshtml.cs) | Выбор счёта, QR-пакет и ручное создание |
| [Receipts](../../CostVision.Web/Pages/Receipts.cshtml.cs) | Список и детали чеков, обновление, перенос, удаление и сверка |
| [MoneyMovements](../../CostVision.Web/Pages/MoneyMovements.cshtml.cs) | Операции, кандидаты, связи, предпросмотр и сохранение выписки |
| [Products](../../CostVision.Web/Pages/Products.cshtml.cs) | Каталог товаров, покупки и адаптивные названия |
| [Stores](../../CostVision.Web/Pages/Stores.cshtml.cs) | Магазины, их чеки и действия с ними |
| [UserSettings](../../CostVision.Web/Pages/Settings/UserSettings.cshtml.cs) | Счета и совместный доступ |
| [Users](../../CostVision.Web/Pages/Settings/Users.cshtml.cs) | Административное управление пользователями |

Вход и выход обслуживают Login и Logout. [UserActivity](../../CostVision.Web/Pages/UserActivity.cshtml.cs) использует маршрут `/user-activity`; обработчик Ping отмечает активность. Стандартные имена остальных маршрутов приводятся к нижнему регистру в настройках Razor Pages.

## Пользователь и защита запросов

Cookie содержит идентичность, а request-фильтры загружают актуального активного пользователя. Общая политика Admin защищает управление пользователями. Эта политика не даёт автоматически прав на чужие счета.

Layout выдаёт antiforgery-токен. TypeScript передаёт его через заголовок `RequestVerificationToken` для изменяющих запросов. JSON-формы и загрузка файлов используют разные заголовки: при FormData браузер сам задаёт multipart boundary.

Новый обработчик не должен переносить бизнес-проверки в PageModel. Он передаёт идентификатор текущего пользователя в use case и сохраняет единый формат ответа.

В 09 57 AJAX-handlers основных страниц/настроек проверяют ModelState до сценария и чтения request. Ошибочный binding дат/чисел/Guid/JSON возвращает общий Validation 400 без служебной диагностики. RequestShapeValidation явно проверяет null request, Receipt ручного ввода, Results/Rows/Members/RoleIds/AccountIds/StoreNames и null-элементы списков. Empty Members допустим и передаётся существующему сценарию; пустой QR/import получает прежнюю бизнес-валидацию. Доступ, роли, identity и правила суммы остаются Application. Прямые тесты устанавливают ModelState сами: это не проверка реального model binding.

CookieHttpContract подключён в AddCookie. Accept=application/json, X-Requested-With=XMLHttpRequest либо JSON Content-Type обозначают JSON-запрос: challenge и forbid возвращают 401/403 и success=false/message без Location/HTML логина. Обычная HTML-навигация сохраняет редиректы, cookie name/sliding expiration/14 дней и antiforgery. Неактивный пользователь по-прежнему удаляется из cookie через CookieAuthorize; Object-level доступ не переносится в cookie events. Реальный auth/antiforgery pipeline ещё не запускался.

В 08 MoneyMovementsModel переведён с механического page-фасада на 14 конкретных сценариев. Имена handlers, FromQuery/FromBody/FromForm, параметры страниц/фильтров, multipart bankId/accountId/file и status/envelopes не менялись. Проверка отсутствующего/пустого файла находится в Application preview; Web создаёт PreviewBankStatementImportRequest и освобождает stream после результата или исключения. Никаких Save/transaction/repository-проверок в PageModel нет. Прямые handler-тесты и actual DI без хоста выполнены отдельно; HTTP/auth/antiforgery-приёмка открыта.

## Ответы и ошибки

[JsonResultMapper](../../CostVision.Web/Web/Mappers/JsonResultMapper.cs) формирует:

| Результат | Содержимое |
|---|---|
| Успех без данных | `{ success: true }` |
| Успех с данными | `{ success: true, data: ... }` |
| Ожидаемая ошибка | `{ success: false, message: ... }` |

Ошибки Validation, Unauthorized, Forbidden, NotFound, Conflict и ExternalService соответствуют HTTP 400, 401, 403, 404, 409 и 502. Неизвестный тип ошибки вызывает исключение.

[ExceptionHandlingMiddleware](../../CostVision.Web/Web/Middleware/ExceptionHandlingMiddleware.cs) журналирует исходную ошибку, путь и TraceId. Unknown и внутренняя OperationCanceledException дают 500; необработанный TimeoutException — 504. Это не меняет provider/refresh/retry06: ожидаемый ExternalService продолжает мапиться в 502, SQL Conflict — в 409. BadHttpRequestException сохраняет транспортный статус, например 400/413, с безопасным сообщением.

До начала ответа middleware очищает прежние headers/body и формирует success=false/message для JSON/AJAX, либо общий HTML с ошибочным статусом для навигации. При RequestAborted до начала остаётся только 499 без тела; начатая клиентская отмена не пишет повторно. Остальные ошибки после Response.HasStarted журналируются и пробрасываются хосту без второго ответа. Изолированные fake-context/feature tests проверяют ветки; поведение настоящего сервера/оборванного соединения требует отдельной приёмки.

Календарные периоды сравниваются по Date и включают DateTo. 31.12.9999 отклоняется до AddDays(1); каталоги также отклоняют обратный необязательный диапазон. Оба окна кандидатов проверяют часы относительно доступного исходного объекта до умножения/AddMinutes. При UseTimeWindow календарные параметры не вычисляются. Непредставимое начальное окно day±1 даёт Validation, а не нулевой кандидатный count; последний месяц dashboard не вычисляет следующий месяц после финальной точки. Новых продуктовых лимитов периода/часов/суммы/пакета нет: сохранены страницы 1–100 и существующие серверные ограничения размера/binding, preview отвергает неположительную FileLength. Финансовые периоды, суммы и identity не округляются и не обрезаются.

## Границы текущей реализации

Этап 05 добавил OperationType в ReceiptDto и IncomeSum в итоговые DTO чеков/магазинов. TotalSum списка теперь расход, не сумма номиналов всех видов документов; TotalSpent магазина также расход. PageModel передаёт эти результаты Application, не определяет знаки и покрытие. Каркас до загрузки обозначает неизвестный итог; главная страница называет переключатель «Источники», поскольку он действует на оба направления. Прямые handler-тесты не подтверждают реальный HTTP-путь и браузер.

Успешный HTTP-ответ импорта может содержать ошибки отдельных строк: клиент должен обрабатывать данные результата, а не только `success`. Отдельная массовая сверка существует в Application, но не подключена к PageModel. Пропуски фильтрации финансовых данных описаны в плане; наличие фильтра авторизации на странице их не закрывает.
