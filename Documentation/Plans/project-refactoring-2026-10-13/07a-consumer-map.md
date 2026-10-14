# Карта потребителей и владельцев транзакций 07A

Карта 53 Application-потребителей составлена до переноса 14.10.2026 по actual исходникам после 07; таблица актуализирована по итоговым зависимостям. Read-only получает прямые repository-зависимости; helper не владеет транзакцией. Общий projection/predicate/include контракт 07 сохраняется. В 08 пользователь отдельно сохранил явную EF/DLL-границу (вариант А); эта карта repositories/транзакций не менялась.

## Узкие контракты записи

- IUserManagementUnitOfWork: User, Role.
- IAccountCreationUnitOfWork: Account, AccountMember.
- IAccountMembershipUnitOfWork: Account, AccountMember, User.
- IReceiptCreationUnitOfWork: Receipt, ReceiptAccount.
- IReceiptTransferUnitOfWork: Receipt, ReceiptAccount, Account.
- IReceiptRemovalUnitOfWork: Receipt, ReceiptAccount.
- IReceiptRefreshUnitOfWork: Receipt, Product, Store.
- IProductNamingUnitOfWork: Product, Receipt.
- IStoreNamingUnitOfWork: Store, Receipt.
- IBankStatementImportUnitOfWork: MoneyMovement, AccountMember.
- IMoneyMovementEditUnitOfWork: MoneyMovement, AccountMember.
- IMoneyMovementRemovalUnitOfWork: MoneyMovement, MoneyMovementReceipt, AccountMember.
- IReceiptLinkUnitOfWork: MoneyMovement, Receipt, MoneyMovementReceipt, AccountMember.
- IReceiptReconciliationUnitOfWork: MoneyMovement, Receipt, MoneyMovementReceipt.

Пересечение репозиториев допустимо: это различные границы атомарных сценариев. User login/activity/active, Account details и удаление Receipt используют один repository и технический scope напрямую. Импорт отделён от исторического редактирования операций; новые записи проверяют архив, исторические изменения сохраняют прежнее правило. Создание операции и ручная привязка объединены общей записью денежной связи; создание сначала сохраняет операцию для ID. Автоматическая сверка не получает AccountMember: роль входит в actual predicate matcher. ReceiptItem не нужен ни одному записывающему UoW: refresh заменяет граф Receipt, ProductStorePurchases читает ReceiptItem напрямую.

## Полная карта Application

| Потребитель | Целевая зависимость | Владелец/режим |
|---|---|---|
| UpdateStoreAdaptiveNameUseCase | IStoreNamingUnitOfWork | Save без внешней transaction |
| GetStoreReceiptListUseCase | IReceiptRepository, IMoneyMovementRepository | чтение |
| GetStoreListUseCase | IStoreRepository | чтение |
| GetReceiptWithItemsUseCase | IReceiptRepository | чтение |
| GetReceiptListUseCase | IReceiptRepository | чтение |
| GetReceiptListPageUseCase | IReceiptRepository, IMoneyMovementRepository | чтение |
| SaveReceiptsScannedUseCase | IReceiptCreationUnitOfWork | Serializable / явное Save |
| SaveManualReceiptUseCase | IReceiptCreationUnitOfWork | Serializable / явное Save |
| RefreshReceiptFromApiUseCase | IReceiptRepository + workflow/projection | снимок/доступ; запись принадлежит workflow |
| RefreshPendingReceiptsUseCase | IServiceScopeFactory: IReceiptRepository / workflow | batch/document/retry scopes; запись принадлежит workflow |
| UnlinkMoneyMovementReceiptUseCase | IMoneyMovementRemovalUnitOfWork | обычная / auto Save |
| DeleteReceiptUseCase | IUnitOfWorkScope + IReceiptRepository | Save без внешней transaction |
| ReceiptRefreshWorkflow | IReceiptRefreshUnitOfWork | Serializable / явное Save |
| ReceiptReportCandidateCounts | IReceiptRepository, IMoneyMovementRepository | чтение |
| GetProductListUseCase | IProductRepository | чтение |
| ReceiptDocumentReportProjection | IReceiptRepository | чтение |
| LinkMoneyMovementReceiptUseCase | IReceiptLinkUnitOfWork | Serializable / явное Save |
| AutoLinkExactMoneyMovementReceiptsUseCase | IReceiptReconciliationUnitOfWork | Serializable / явное Save |
| UpdateProductAdaptiveNameUseCase | IProductNamingUnitOfWork | Save без внешней transaction |
| GetProductStorePurchasesUseCase | IReceiptItemRepository | чтение |
| GetMoneyMovementListUseCase | IMoneyMovementRepository, IReceiptRepository | чтение |
| GetMoneyMovementAccountsUseCase | IAccountRepository | чтение |
| GetLinkedReceiptMoneyMovementsUseCase | IReceiptRepository, IMoneyMovementReceiptRepository | чтение |
| GetLinkedMoneyMovementReceiptsUseCase | IMoneyMovementRepository, IMoneyMovementReceiptRepository | чтение |
| ExactMoneyMovementReceiptMatcher | IMoneyMovementRepository, IReceiptRepository | чтение |
| GetUserAccountsUseCase | IAccountRepository | чтение |
| GetAccountShareUsersUseCase | IAccountRepository, IUserRepository | чтение |
| CreateAccountUseCase | IAccountCreationUnitOfWork | обычная / auto Save |
| UpdateMoneyMovementCommentUseCase | IMoneyMovementEditUnitOfWork | Save без внешней transaction |
| MoveMoneyMovementToAccountUseCase | IMoneyMovementEditUnitOfWork | Serializable / явное Save |
| MoneyMovementAccountAccessValidator | IAccountMemberRepository | чтение |
| GetReceiptMoneyMovementCandidatesUseCase | IReceiptRepository, IMoneyMovementRepository | чтение |
| GetMoneyMovementReceiptCandidatesUseCase | IMoneyMovementRepository, IReceiptRepository | чтение |
| DeleteMoneyMovementUseCase | IMoneyMovementRemovalUnitOfWork | обычная / auto Save |
| CreateMoneyMovementUseCase | IReceiptLinkUnitOfWork | Serializable / явное Save |
| RemoveReceiptFromAccountUseCase | IReceiptRemovalUnitOfWork | Save без внешней transaction |
| ValidateReceiptCreationAccessUseCase | IAccountMemberRepository | чтение |
| UpdateAccountMembersUseCase | IAccountMembershipUnitOfWork | обычная / auto Save |
| MoveReceiptToAccountUseCase | IReceiptTransferUnitOfWork | Serializable / явное Save |
| UpdateAccountUseCase | IUnitOfWorkScope + IAccountRepository | Save без внешней transaction |
| GetUserListUseCase | IUserRepository | чтение |
| GetUserUseCase | IUserRepository | чтение |
| CreateUserUseCase | IUserManagementUnitOfWork | Save без внешней transaction |
| PreviewBankStatementImportUseCase | IMoneyMovementRepository, IAccountMemberRepository | чтение |
| GetRoleListUseCase | IRoleRepository | чтение |
| UserUpsertRequestValidator | IRoleRepository | чтение |
| AuthenticateUserUseCase | IUnitOfWorkScope + IUserRepository | Save без внешней transaction |
| MarkUserActivityUseCase | IUnitOfWorkScope + IUserRepository | Save без внешней transaction |
| UpdateUserUseCase | IUserManagementUnitOfWork | Save без внешней transaction |
| GetActiveUserForRequestUseCase | IUserRepository | чтение |
| ImportMoneyMovementsUseCase | IBankStatementImportUnitOfWork | Serializable / явное Save |
| ToggleUserActiveUseCase | IUnitOfWorkScope + IUserRepository | Save без внешней transaction |
| GetDashboardIncomeExpenseReportUseCase | IReceiptRepository, IMoneyMovementRepository | чтение |

## Helpers, проверки доступа и фон

- ReceiptReportCandidateCounts: Receipt + MoneyMovement напрямую; вызывается списками чеков/магазина после страницы, только для реальных открываемых ReceiptId и их Accounts. Не открывает транзакцию.
- ReceiptDocumentReportProjection: Receipt напрямую; детали и refresh читают копии identity с денежным графом вне транзакции записи, общего snapshot нет.
- UserUpsertRequestValidator: Role напрямую; Create/Update проверяют весь набор до мутации/hash.
- MoneyMovementAccountAccessValidator: AccountMember напрямую; вызывающий сценарий передаёт свой repository, Owner/Editor и requireActive сохраняются.
- ExactMoneyMovementReceiptMatcher: MoneyMovement + Receipt напрямую; Create/Auto владеют Serializable, helper только читает и строит связи. Вложенной транзакции нет.
- ValidateReceiptCreationAccessUseCase: AccountMember напрямую; manual/QR сохраняют существующую проверку membership/архива до обработки, identity/pending и запись — внутри Serializable.
- RefreshPendingReceiptsUseCase: IServiceScopeFactory; пакет разрешает только IReceiptRepository в освобождаемом scope, document scope разрешает IReceiptRefreshWorkflow. Retry-state получает новый scope после disposal отказавшего. Workflow владеет единственной короткой транзакцией после provider.
- FinancialDataAccess/mapper и domain-проверки остаются в Application/Domain. Собственный чек и текущий доступ к счёту/операции различаются; Admin/автор операции не обходят membership.

## Сохранения и отказ

Обычная перегрузка scope сохраняет после action; явная с IsolationLevel не сохраняет автоматически. Manual/QR сохраняют принятый пакет один раз, без записи при отсутствии принятых строк. Import сохраняет один раз при изменениях, сохраняя порядок/counters/pending. CreateMoneyMovement: Save для ID, matcher и второй Save только при связи; reload DTO внутри той же transaction. Link/Auto/оба переноса: один явный Save после проверок внутри Serializable. Refresh: один явный Save всего графа/каталогов; retry-state сохраняется отдельно с повторной проверкой версии/расписания.

Infrastructure: IUnitOfWorkScope → UnitOfWorkScope, единственный scoped владелец транзакции и отказа. Сценарные adapters делегируют scope и не содержат собственных контекстов/флагов. После 08A вместо двух DLL adapters одна AddEfCoreContext<ApplicationContext,CostVisionContextKey> привязка: IContext*Repository/query, IUnitOfWorkContext и maintenance используют тот же scoped контекст/tracker. Разные scopes независимы. После отказа сохранение/любая новая транзакция через любую обёртку запрещены; rollback либо техническая очистка tracker не сбрасывает проектный флаг отказа. Nested/concurrent transaction в одном scope запрещена до Begin. Все 14 контрактов, владельцы и порядок Save в карте сохранены.

## DI и тестовые потребители

AddApplication сохраняет регистрации use case/workflow, их constructors получают новые зависимости. AddInfrastructure регистрирует один scope и каждый узкий adapter вместо глобального UoW. Фикстуры Application проверяют прежние predicates/domain results и используют только тестовый набор mock-репозиториев; он не регистрируется в production. Infrastructure проверяет actual AddApplication/AddInfrastructure/DLL без hosting; integration fixtures переводятся на прямые repository + scope, их SQL/hosting тесты не запускаются.

После08 MoneyMovementsModel вызывает 14 конкретных сценариев напрямую; общий page-фасад/интерфейс/регистрация удалены. PageModel не получает repositories/scope и не открывает транзакцию. PreviewBankStatementImportRequest принадлежит операции, preview проверяет отсутствие/пустоту файла до чтений, Web освобождает поток. Import/Create/Move/Delete/Link/Unlink/Refresh сохраняют владельцев и число/порядок Save из таблицы. [Решение08](08-boundary-decision.md) и [поставка DLL](../../../libs/README.md) описывают текущую границу; новый UoW или отдельный контекст не добавлялись.

## Открытая приёмка

Открытые проверки и статусы 01–07 сохранены в исходных файлах этапов, обязательная SQL-приёмка07A также открыта. Общая scoped-сессия/mock/metadata/ToQueryString не доказывают SQL-конкурентность, rollback/fixup или snapshot. AddReceiptRowVersion уже присутствует: RowVersion rowversion + RefreshRevision bigint NOT NULL default 0; применение к БД неизвестно. Миграцию, designer/snapshot и схему 07A/08/08A не меняют. SQL/БД/hosting/provider HTTP/browser/production не запускались. Фактические файлы подтверждают: 01–11, 07A и 08A реализованы изолированно, проверка не завершена; 12–15 не начаты. Старые журналы и результаты сохранены.
