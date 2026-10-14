# Авторизация, пользователи и счета

## Cookie-аутентификация

[LoginModel](../../CostVision.Web/Pages/Login.cshtml.cs) вызывает [AuthenticateUserUseCase](../../CostVision.Application/UseCases/Authorize/Authentication/AuthenticateUserUseCase.cs). Use case требует непустые логин и пароль, ищет пользователя, проверяет hash, отклоняет неактивного пользователя и обновляет `LastLoginAtUtc`.

Cookie называется `.CostVision.Cookies`, имеет sliding expiration и срок неактивности 14 дней. Login path — `/login`. Ключи Data Protection сохраняются в `keys-windows` или `keys-linux` под content root, чтобы cookie переживала перезапуск процесса.

CookieHttpContract сохраняет редиректы HTML-навигации, но JSON/AJAX получает 401/403 с success=false/message без HTML логина. JSON определяется Accept, X-Requested-With либо Content-Type. Antiforgery не отключён. Options/фильтры проверяются изолированно; реальное истечение cookie, повторный вход и antiforgery остаются HTTP-/браузерной приёмкой.

[CookieAuthorizeAttribute](../../CostVision.Web/Web/Authorize/Attributes/CookieAuthorizeAttribute.cs) используется на закрытых Razor Pages. Он загружает активного пользователя для запроса; отключение пользователя тем самым отзывает доступ, не дожидаясь естественного истечения cookie. `/user-activity?handler=Ping` обновляет время активности.

## Общие роли

`RoleType` содержит `User` и `Admin`. Политика `Admin` реализована [AdminAccessHandler](../../CostVision.Web/Web/Authorize/AdminAccessHandler.cs) и защищает `/settings/users`. Остальные финансовые страницы требуют аутентификации, но не общей роли Admin.

Admin может читать список пользователей и ролей, создавать и редактировать пользователя и переключать его активность. Общая роль не обходит проверки участия в финансовом счёте.

## Правила пользователя

[UserUpsertRequestValidator](../../CostVision.Application/UseCases/Authorize/Users/Helpers/UserUpsertRequestValidator.cs) задаёт контракт:

- логин: 3–128 символов;
- имя: 2–256 символов;
- при создании пароль обязателен, при обновлении пустое значение сохраняет прежний;
- пароль: 8–128 печатных ASCII-символов, минимум одна заглавная буква, цифра и специальный символ;
- должна быть передана хотя бы одна существующая роль. Guid.Empty отклоняется, дубли ID схлопываются. CreateUser/UpdateUser сверяют полный набор через базовый Role.GetItemsByPredicateAsync до Hash/Create/TryUpdate; неизвестный ID даёт Validation без мутации профиля/hash/ролей и SaveChanges.

Логин проверяется на уникальность без учёта регистра в use case и имеет unique index в БД. Пароль хранится только как hash. Пользователи не удаляются из UI: активность переключается, чтобы сохранить ссылки аудита.

Логин сохраняется после Trim; запрос входа использует Trim и ToUpperInvariant, сравнивая Login.ToUpper(). Пароль создаётся/меняется после Trim, как раньше; AuthenticateUser выполняет единственный Verify с Password.Trim(). Внутренние пробелы нового пароля запрещены. Пустое/пробельное обновление оставляет hash прежним; массового rehash или скрытых повторов Verify нет. Изолированно проверены действующий формат PBKDF2 и реальные create/update/login с Hasher.

## Модель счёта

[Account](../../CostVision.Domain/Models/Receipts/Account.cs) содержит:

- название длиной 3–128 символов;
- необязательное описание до 512 символов;
- цвет `#RRGGBB`, по умолчанию `#0D6EFD`;
- владельца, время создания и признак архива;
- участников, связи с чеками и денежные операции.

При создании одновременно появляется `AccountMember` с ролью `Owner`. Владелец определяется и `CreatedByUserId`, и обязательной owner membership. Изменение деталей и архива доступно только владельцу.

## Роли внутри счёта

`AccountAccessRole`:

| Роль | Чтение | Чеки и операции | Настройки счёта и участники |
|---|---:|---:|---:|
| `Viewer` | да | нет | нет |
| `Editor` | да | да | нет |
| `Owner` | да | да | да |

`Owner` нельзя назначить обычному участнику через update members. Владельца нельзя удалить или понизить. Список участников может содержать только активных пользователей; дубли входного списка схлопываются по `UserId`, последнее значение роли побеждает.

## Проверка доступа

Чтение финансовых данных в сценариях этапа 01 использует [FinancialDataAccess](../../CostVision.Application/Extensions/FinancialDataAccess.cs). Счёт и его операции видны владельцу и текущим участникам независимо от роли Owner/Editor/Viewer. Авторство операции не сохраняет доступ после удаления участия. Собственный чек остаётся доступным автору, но каждый вложенный счёт, операция и связанный чек проверяются отдельно. Общая роль Admin не расширяет финансовую область чтения.

Use case добавляют выражение доступа к собственному predicate и задают include через базовые методы репозиториев. Мапперы получают явный ID текущего пользователя; перегрузка ReceiptMapper с неявной подстановкой автора удалена. Изолированные тесты проверяют предикаты и DTO, но перевод этих выражений и загрузка графов на SQL Server ещё требуют отдельной приёмки.

[GetUserAccountsUseCase](../../CostVision.Application/UseCases/Receipts/Accounts/GetUserAccountsUseCase.cs) возвращает счета, где пользователь владелец или участник. Архивные счета включаются только по отдельному параметру. Для добавления чека список дополнительно ограничивается `Owner` и `Editor`.

Оба списка счетов используют FinancialDataAccess и Include Members/CreatedByUser. GetRequiredAccountRole требует единственной Owner membership владельца, запрещает стороннего Owner и проверяет роль текущего участника. Нарушение вызывает явную InvalidOperationException вместо запасного Owner, ошибки словаря или пустого списка владельца.

ValidateReceiptCreationAccess загружает Account membership и возвращает Forbidden для архива. CreateMoneyMovement и ImportMoneyMovements вызывают общий валидатор с requireActive=true до транзакции/записи. Остальные исторические изменения и предпросмотр импорта не получают архивного запрета автоматически.

[AccountReceiptAccessValidator](../../CostVision.Application/UseCases/Receipts/Accounts/Helpers/AccountReceiptAccessValidator.cs) и `MoneyMovementAccountAccessValidator` проверяют доступ к изменениям, но их контракты различаются. Валидатор чеков возвращает `Forbidden` при отсутствии участия, валидатор операций — `NotFound`; Viewer получает запрет изменения. Условия определения владельца также различаются. Объединять эти правила без проверки существующих сценариев нельзя.

Важное дополнительное правило: переносить между счетами, отвязывать и удалять можно только собственный чек, даже если пользователь Editor в счёте. Это отделяет право управлять общим бюджетом от права уничтожить чужой первичный документ.

## EF-связи

`AccountMembers` имеет составной ключ пользователя и счёта. Удаление счёта каскадно удаляет memberships, receipt links и money movements, но удаление владельца ограничено. Архивирование является штатным пользовательским действием и не удаляет данные.
