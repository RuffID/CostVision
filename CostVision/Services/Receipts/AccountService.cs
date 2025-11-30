using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.Service.Receipt;
using CostVision.Models.Enums.Authorization;
using CostVision.Models.Enums.Services.Receipts;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Services.Receipts;

namespace CostVision.Services.Receipts
{
    public class AccountService(IUnitOfWork unitOfWork) : IAccountService
    {
        public async Task<Account> CreateAccountAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct)
        {
            Account? existDefault = await unitOfWork.Account.GetItemByPredicate(a => a.IsDefault && a.CreatedByUserId == ownerUserId, ct: ct);

            if (existDefault != null && existDefault.IsDefault)
                existDefault.IsDefault = false;

            Account account = new()
            {
                Name = request.Name,
                Description = request.Description,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = ownerUserId,
                IsDefault = request.IsDefault,
            };

            AccountMember ownerMember = new()
            {
                UserId = ownerUserId,
                Account = account,
                Role = AccountAccessRole.Owner
            };

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                unitOfWork.Account.Create(account);
                unitOfWork.AccountMember.Create(ownerMember);
            }, ct);

            return account;
        }

        public async Task<bool> UpdateAccountAsync(Guid ownerUserId, Account account, CancellationToken ct)
        {
            Account? current = await unitOfWork.Account.GetItemByPredicate(a => a.Id == account.Id && a.CreatedByUserId == ownerUserId, ct: ct);

            if (current == null)
                return false;

            bool wasDefault = current.IsDefault;
            bool willBeDefault = account.IsDefault;
            bool willBeArchived = account.IsArchived;

            // Нельзя делать дефолтным удалённый счёт
            if (willBeDefault && willBeArchived)
                return false;

            // Нельзя удалять (архивировать) счёт по умолчанию
            // Запретить, если счёт сейчас дефолтный и его пытаются заархивировать
            if (willBeArchived && wasDefault)
                return false;

            // Нельзя снимать "по умолчанию" с текущего дефолтного счёта
            // Т.е. запрет: было IsDefault = true, станет IsDefault = false, и при этом счёт не архивируется
            if (wasDefault && !willBeDefault && !willBeArchived)
                return false;

            // Если делать счёт дефолтным — снять флаг со всех остальных активных
            if (willBeDefault && !willBeArchived)
            {
                List<Account> activeAccounts = await unitOfWork.Account
                    .GetItemsByPredicate(a => a.CreatedByUserId == ownerUserId && !a.IsArchived, ct: ct);

                foreach (Account acc in activeAccounts)
                {
                    if (acc.Id != current.Id && acc.IsDefault)
                        acc.IsDefault = false;
                }
            }

            // Применить новые значения к текущему аккаунту
            current.Name = account.Name;
            current.Description = account.Description;
            current.IsArchived = account.IsArchived;
            current.IsDefault = willBeDefault;

            await unitOfWork.Account.Upsert(current, ct);
            await unitOfWork.SaveAsync(ct);

            // Обновить ссылочный объект, который уходит наверх
            account.IsArchived = current.IsArchived;
            account.IsDefault = current.IsDefault;

            return true;
        }

        public async Task<AccountMember> AddMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, AccountAccessRole role, CancellationToken ct)
        {
            // Проверяет, что вызывающий является владельцем счёта
            Account? account = await unitOfWork.Account.GetItemByPredicate(a => a.Id == accountId, asNoTracking: false, ct: ct);

            if (account == null)
                throw new InvalidOperationException("Счёт не найден.");

            if (account.CreatedByUserId != ownerUserId)
                throw new InvalidOperationException("Нет прав управлять участниками счёта.");

            // Проверяет, что участник ещё не добавлен
            AccountMember? exist = await unitOfWork.AccountMember.GetItemByPredicate(m => m.AccountId == accountId && m.UserId == targetUserId,
                    asNoTracking: false,
                    ct: ct);

            if (exist != null)
            {
                // Обновляет роль, если нужно
                exist.Role = role;
                await unitOfWork.SaveAsync(ct);
                return exist;
            }

            AccountMember member = new()
            {
                AccountId = accountId,
                UserId = targetUserId,
                Role = role
            };

            unitOfWork.AccountMember.Create(member);
            await unitOfWork.SaveAsync(ct);

            return member;
        }

        public async Task RemoveMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, CancellationToken ct)
        {
            // Проверяет права владельца
            Account? account = await unitOfWork.Account.GetItemByPredicate(a => a.Id == accountId, asNoTracking: false, ct: ct);

            if (account == null)
                throw new InvalidOperationException("Счёт не найден.");

            if (account.CreatedByUserId != ownerUserId)
                throw new InvalidOperationException("Нет прав управлять участниками счёта.");

            // Нельзя удалить самого владельца
            if (targetUserId == ownerUserId)
                throw new InvalidOperationException("Нельзя удалить владельца счёта.");

            AccountMember? member = await unitOfWork.AccountMember.GetItemByPredicate(m => m.AccountId == accountId && m.UserId == targetUserId,
                    asNoTracking: false,
                    ct: ct);

            if (member == null)
                return;

            unitOfWork.AccountMember.Delete(member);
            await unitOfWork.SaveAsync(ct);
        }

        public async Task<List<UserAccountViewModel>> GetUserAccountsAsync(Guid userId, bool includeArchived, CancellationToken ct)
        {
            // Получает все счета, где пользователь является участником
            List<AccountMember> memberships = await unitOfWork.AccountMember.GetItemsByPredicate(m => m.UserId == userId, asNoTracking: true, ct: ct);

            List<Guid> accountIds = memberships.Select(m => m.AccountId).Distinct().ToList();

            if (accountIds.Count == 0)
                return new();

            List<Account> accounts = await unitOfWork.Account.GetItemsByPredicate(a => accountIds.Contains(a.Id) && (includeArchived || !a.IsArchived),
                    asNoTracking: true,
                    ct: ct);

            return accounts.Select(account => new UserAccountViewModel()
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                IsActive = !account.IsArchived,
                IsDefault = account.IsDefault
            }).ToList();
        }

        public async Task<ReceiptAccountLinkResult> LinkReceiptToAccountAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            // Проверяет, что счёт существует и пользователь имеет к нему доступ
            Account? account = await unitOfWork.Account.GetItemByPredicate(a => a.Id == accountId, asNoTracking: false, includes: a => a.Members, ct: ct);

            if (account == null)
            {
                return new ReceiptAccountLinkResult
                {
                    Status = ReceiptAccountLinkStatusEnum.AccountNotFound,
                    ErrorMessage = "Счёт не найден."
                };
            }

            bool hasAccess = account.CreatedByUserId == currentUserId
                             || account.Members.Any(m => m.UserId == currentUserId);

            if (!hasAccess)
            {
                return new ReceiptAccountLinkResult
                {
                    Status = ReceiptAccountLinkStatusEnum.AccessDenied,
                    ErrorMessage = "Нет доступа к указанному счёту."
                };
            }

            // Проверяет, что чек существует
            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicate(r => r.Id == receiptId, asNoTracking: false, ct: ct);

            if (receipt == null)
            {
                return new ReceiptAccountLinkResult
                {
                    Status = ReceiptAccountLinkStatusEnum.ReceiptNotFound,
                    ErrorMessage = "Чек не найден."
                };
            }

            // Проверяет, что чек ещё не привязан к этому счёту (один чек → один счёт)
            ReceiptAccount? exist = await unitOfWork.ReceiptAccount.GetItemByPredicate(x => x.AccountId == accountId && x.ReceiptId == receiptId,
                    asNoTracking: true,
                    ct: ct);

            if (exist != null)
            {
                return new ReceiptAccountLinkResult
                {
                    Status = ReceiptAccountLinkStatusEnum.AlreadyLinked,
                    ErrorMessage = "Чек уже привязан к этому счёту."
                };
            }

            // Создаёт привязку чека к счёту
            ReceiptAccount link = new()
            {
                AccountId = accountId,
                ReceiptId = receiptId,
            };

            unitOfWork.ReceiptAccount.Create(link);
            await unitOfWork.SaveAsync(ct);

            return new ReceiptAccountLinkResult
            {
                Status = ReceiptAccountLinkStatusEnum.Success,
                Link = link
            };
        }
    }
}
