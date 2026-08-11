# Пользователи, роли и счета

Пользователь — это [User](../CostVision.Domain/Models/Authorization/User.cs). У него есть роли, признак активности и время последней активности. Неактивный пользователь не может войти.

Счёт — это [Account](../CostVision.Domain/Models/Receipts/Account.cs). У счёта есть владелец и список участников [AccountMember](../CostVision.Domain/Models/Receipts/AccountMember.cs). Владелец управляет участниками; участник получает доступ к данным счёта в пределах разрешённого сценария.

Вход выполняет [AuthenticateUserUseCase](../CostVision.Application/UseCases/Authorize/Authentication/AuthenticateUserUseCase.cs). Он проверяет пароль через контракт [IPasswordHasher](../CostVision.Application/Abstractions/Service/Authorize/IPasswordHasher.cs), статус пользователя и обновляет время входа.

Проверка прав не должна жить в Razor Page. Например, [MoneyMovementAccountAccessValidator](../CostVision.Application/UseCases/MoneyMovements/Management/MoneyMovementAccountAccessValidator.cs) загружает счёт и подтверждает, что пользователь вправе его менять. На веб-уровне атрибуты [CookieAuthorizeAttribute](../CostVision.Web/Web/Authorize/Attributes/CookieAuthorizeAttribute.cs) и [LoadUserAttribute](../CostVision.Web/Web/Authorize/Attributes/LoadUserAttribute.cs) лишь обеспечивают наличие авторизованного пользователя в запросе.
