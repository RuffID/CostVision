# Веб-интерфейс

Пользователь работает с Razor Pages в проекте `CostVision.Web`. Например, [ReceiptsModel](../CostVision.Web/Pages/Receipts.cshtml.cs) обслуживает экран чеков, [MoneyMovementsModel](../CostVision.Web/Pages/MoneyMovements.cshtml.cs) — денежные операции, а [UserSettingsModel](../CostVision.Web/Pages/Settings/UserSettings.cshtml.cs) — настройки счетов.

Страница принимает JSON или query-параметры, получает текущего пользователя и вызывает нужный use case. Затем [JsonResultMapper](../CostVision.Web/Web/Mappers/JsonResultMapper.cs) превращает `ServiceResult` в единый JSON-ответ. Поэтому пользовательские ошибки не становятся необработанными исключениями и одинаково обрабатываются на клиенте.

Авторизация основана на cookie. [CookieAuthorizeAttribute](../CostVision.Web/Web/Authorize/Attributes/CookieAuthorizeAttribute.cs) ограничивает доступ к страницам, а [AdminAccessHandler](../CostVision.Web/Web/Authorize/AdminAccessHandler.cs) проверяет административную политику. Необработанные исключения перехватывает [ExceptionHandlingMiddleware](../CostVision.Web/Web/Middleware/ExceptionHandlingMiddleware.cs).

Клиентская логика находится в `CostVision.Web/wwwroot/ts`. Она обращается к страницам асинхронно, поэтому основные действия не требуют полной перезагрузки страницы.
