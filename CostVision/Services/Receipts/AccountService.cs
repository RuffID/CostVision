using CostVision.Abstractions.DataBase.Repositories;
using CostVision.Abstractions.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.Dtos.Receipts;
using CostVision.Models.Enums.Authorization;
using CostVision.Models.Enums.Services.Receipts;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Responses.Results;
using CostVision.Models.Services.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CostVision.Services.Receipts
{
    public class AccountService(IUnitOfWork unitOfWork) : IAccountService
    {
        private static readonly Regex colorHexRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        public async Task<ServiceResult<Account>> CreateAccountAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct)
        {
            ServiceResult<string> colorHexResult = NormalizeColorHex(request.ColorHex);
            if (!colorHexResult.Success || colorHexResult.Data == null)
                return ServiceResult<Account>.Fail(colorHexResult.Error!.StatusCode, colorHexResult.Error.Message);

            Account account = new()
            {
                Name = request.Name,
                Description = request.Description,
                ColorHex = colorHexResult.Data,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = ownerUserId,
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

            return ServiceResult<Account>.Ok(account);
        }

        public async Task<ServiceResult<Account>> UpdateAccountAsync(Guid ownerUserId, Account account, CancellationToken ct)
        {
            ServiceResult<string> colorHexResult = NormalizeColorHex(account.ColorHex);
            if (!colorHexResult.Success || colorHexResult.Data == null)
                return ServiceResult<Account>.Fail(colorHexResult.Error!.StatusCode, colorHexResult.Error.Message);

            Account? current = await unitOfWork.Account.GetItemByPredicateAsync(a => a.Id == account.Id && a.CreatedByUserId == ownerUserId, ct: ct);

            if (current == null)
                return ServiceResult<Account>.Fail(404, "Счёт не найден.");

            // Применить новые значения к текущему аккаунту
            current.Name = account.Name;
            current.Description = account.Description;
            current.ColorHex = colorHexResult.Data;
            current.IsArchived = account.IsArchived;

            await unitOfWork.SaveChangesAsync(ct);

            // Обновить ссылочный объект, который уходит наверх
            account.ColorHex = current.ColorHex;
            account.IsArchived = current.IsArchived;

            return ServiceResult<Account>.Ok(account);
        }

        public async Task<ServiceResult<List<AccountShareUserDto>>> GetAccountShareUsersAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<List<AccountShareUserDto>>.Fail(400, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetItemByPredicateAsync(a => a.Id == accountId && a.CreatedByUserId == ownerUserId,
                asNoTracking: true,
                include: q => q.Include(a => a.Members),
                ct: ct);

            if (account == null)
                return ServiceResult<List<AccountShareUserDto>>.Fail(404, "Счёт не найден.");

            Dictionary<Guid, AccountAccessRole> selectedUsers = account.Members
                .Where(m => m.UserId != ownerUserId)
                .ToDictionary(m => m.UserId, m => m.Role);

            List<User> users = await unitOfWork.User.GetItemsByPredicateAsync(u => u.IsActive && u.Id != ownerUserId, asNoTracking: true, ct: ct);

            List<AccountShareUserDto> items = users
                .OrderBy(u => u.Name)
                .Select(u => new AccountShareUserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Login = u.Login,
                    IsSelected = selectedUsers.ContainsKey(u.Id),
                    Role = selectedUsers.TryGetValue(u.Id, out AccountAccessRole role) ? role : null
                })
                .ToList();

            return ServiceResult<List<AccountShareUserDto>>.Ok(items);
        }

        public async Task<ServiceResult<bool>> UpdateAccountMembersAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<UpdateAccountMemberRequest> members, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetItemByPredicateAsync(a => a.Id == accountId && a.CreatedByUserId == ownerUserId,
                asNoTracking: false,
                include: q => q.Include(a => a.Members),
                ct: ct);

            if (account == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден.");

            List<UpdateAccountMemberRequest> desiredMembers = members
                .Where(x => x.UserId != Guid.Empty && x.UserId != ownerUserId)
                .GroupBy(x => x.UserId)
                .Select(x => x.Last())
                .ToList();

            if (desiredMembers.Any(x => !Enum.IsDefined(typeof(AccountAccessRole), x.Role) || x.Role == AccountAccessRole.Owner))
                return ServiceResult<bool>.Fail(400, "Можно назначить только роли Viewer или Editor.");

            List<Guid> desiredUserIds = desiredMembers
                .Select(x => x.UserId)
                .ToList();

            List<User> availableUsers = desiredUserIds.Count == 0
                ? new()
                : await unitOfWork.User.GetItemsByPredicateAsync(u => desiredUserIds.Contains(u.Id) && u.IsActive, asNoTracking: true, ct: ct);

            if (availableUsers.Count != desiredUserIds.Count)
                return ServiceResult<bool>.Fail(400, "Один или несколько выбранных пользователей недоступны.");

            List<AccountMember> currentMembers = account.Members
                .Where(m => m.UserId != ownerUserId)
                .ToList();

            HashSet<Guid> currentUserIds = currentMembers
                .Select(m => m.UserId)
                .ToHashSet();

            List<AccountMember> membersToRemove = currentMembers
                .Where(m => !desiredUserIds.Contains(m.UserId))
                .ToList();

            List<Guid> userIdsToAdd = desiredUserIds
                .Where(id => !currentUserIds.Contains(id))
                .ToList();

            List<AccountMember> membersToUpdate = currentMembers
                .Where(m => desiredUserIds.Contains(m.UserId))
                .ToList();

            Dictionary<Guid, AccountAccessRole> desiredRolesByUserId = desiredMembers
                .ToDictionary(x => x.UserId, x => x.Role);

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                if (membersToRemove.Count > 0)
                    unitOfWork.AccountMember.DeleteRange(membersToRemove);

                foreach (AccountMember member in membersToUpdate)
                {
                    AccountAccessRole desiredRole = desiredRolesByUserId[member.UserId];
                    if (member.Role != desiredRole)
                        member.Role = desiredRole;
                }

                foreach (Guid userId in userIdsToAdd)
                {
                    unitOfWork.AccountMember.Create(new AccountMember
                    {
                        AccountId = accountId,
                        UserId = userId,
                        Role = desiredRolesByUserId[userId]
                    });
                }

                await Task.CompletedTask;
            }, ct);

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> ValidateReceiptCreationAccessAsync(Guid accountId, Guid userId, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            AccountMember? membership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                m => m.AccountId == accountId && m.UserId == userId,
                asNoTracking: true,
                ct: ct);

            if (membership == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден или доступ к нему отсутствует.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult<bool>.Fail(403, "Недостаточно прав для добавления чеков в этот счёт.");

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<AccountMember> AddMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, AccountAccessRole role, CancellationToken ct)
        {
            // Проверяет, что вызывающий является владельцем счёта
            Account? account = await unitOfWork.Account.GetItemByPredicateAsync(a => a.Id == accountId, asNoTracking: false, ct: ct);

            if (account == null)
                throw new InvalidOperationException("Счёт не найден.");

            if (account.CreatedByUserId != ownerUserId)
                throw new InvalidOperationException("Нет прав управлять участниками счёта.");

            // Проверяет, что участник ещё не добавлен
            AccountMember? exist = await unitOfWork.AccountMember.GetItemByPredicateAsync(m => m.AccountId == accountId && m.UserId == targetUserId,
                    asNoTracking: false,
                    ct: ct);

            if (exist != null)
            {
                // Обновляет роль, если нужно
                exist.Role = role;
                await unitOfWork.SaveChangesAsync(ct);
                return exist;
            }

            AccountMember member = new()
            {
                AccountId = accountId,
                UserId = targetUserId,
                Role = role
            };

            unitOfWork.AccountMember.Create(member);
            await unitOfWork.SaveChangesAsync(ct);

            return member;
        }

        public async Task RemoveMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, CancellationToken ct)
        {
            // Проверяет права владельца
            Account? account = await unitOfWork.Account.GetItemByPredicateAsync(a => a.Id == accountId, asNoTracking: false, ct: ct);

            if (account == null)
                throw new InvalidOperationException("Счёт не найден.");

            if (account.CreatedByUserId != ownerUserId)
                throw new InvalidOperationException("Нет прав управлять участниками счёта.");

            // Нельзя удалить самого владельца
            if (targetUserId == ownerUserId)
                throw new InvalidOperationException("Нельзя удалить владельца счёта.");

            AccountMember? member = await unitOfWork.AccountMember.GetItemByPredicateAsync(m => m.AccountId == accountId && m.UserId == targetUserId,
                    asNoTracking: false,
                    ct: ct);

            if (member == null)
                return;

            unitOfWork.AccountMember.Delete(member);
            await unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<List<UserAccountViewModel>> GetUserAccountsAsync(Guid userId, bool includeArchived, CancellationToken ct)
        {
            // Получает все счета, где пользователь является участником
            List<AccountMember> memberships = await unitOfWork.AccountMember.GetItemsByPredicateAsync(m => m.UserId == userId, asNoTracking: true, ct: ct);

            List<Guid> accountIds = memberships.Select(m => m.AccountId).Distinct().ToList();

            if (accountIds.Count == 0)
                return new();

            List<Account> accounts = await unitOfWork.Account.GetItemsByPredicateAsync(a => accountIds.Contains(a.Id) && (includeArchived || !a.IsArchived),
                    asNoTracking: true,
                    include: query => query.Include(a => a.CreatedByUser),
                    ct: ct);

            Dictionary<Guid, AccountAccessRole> rolesByAccountId = memberships
                .GroupBy(m => m.AccountId)
                .ToDictionary(g => g.Key, g => g.Select(m => m.Role).First());

            return accounts.Select(account => new UserAccountViewModel()
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                ColorHex = account.ColorHex,
                IsActive = !account.IsArchived,
                CanManage = account.CreatedByUserId == userId,
                OwnerName = account.CreatedByUser?.Name ?? string.Empty,
                AccessRole = rolesByAccountId[account.Id]
            }).ToList();
        }

        public async Task<List<UserAccountViewModel>> GetUserAccountsForReceiptCreationAsync(Guid userId, CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await GetUserAccountsAsync(userId, false, ct);

            return accounts
                .Where(x => x.AccessRole == AccountAccessRole.Owner || x.AccessRole == AccountAccessRole.Editor)
                .ToList();
        }

        public async Task<ReceiptAccountLinkResult> LinkReceiptToAccountAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            // Проверяет, что счёт существует и пользователь имеет к нему доступ
            Account? account = await unitOfWork.Account.GetItemByPredicateAsync(a => a.Id == accountId, asNoTracking: false, include: a => a.Include(a => a.Members), ct: ct);

            if (account == null)
            {
                return new ReceiptAccountLinkResult
                {
                    Status = ReceiptAccountLinkStatusEnum.AccountNotFound,
                    ErrorMessage = "Счёт не найден."
                };
            }

            ServiceResult<bool> accessResult = ValidateReceiptAccountModificationAccess(account, currentUserId);
            if (!accessResult.Success)
            {
                return new ReceiptAccountLinkResult
                {
                    Status = ReceiptAccountLinkStatusEnum.AccessDenied,
                    ErrorMessage = accessResult.Error!.Message
                };
            }

            // Проверяет, что чек существует
            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(r => r.Id == receiptId, asNoTracking: false, ct: ct);

            if (receipt == null)
            {
                return new ReceiptAccountLinkResult
                {
                    Status = ReceiptAccountLinkStatusEnum.ReceiptNotFound,
                    ErrorMessage = "Чек не найден."
                };
            }

            // Проверяет, что чек ещё не привязан к этому счёту (один чек → один счёт)
            ReceiptAccount? exist = await unitOfWork.ReceiptAccount.GetItemByPredicateAsync(x => x.AccountId == accountId && x.ReceiptId == receiptId,
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
            await unitOfWork.SaveChangesAsync(ct);

            return new ReceiptAccountLinkResult
            {
                Status = ReceiptAccountLinkStatusEnum.Success,
                Link = link
            };
        }

        public async Task<ServiceResult<bool>> MoveReceiptToAccountAsync(Guid sourceAccountId, Guid targetAccountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

            if (sourceAccountId == Guid.Empty || targetAccountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            if (sourceAccountId == targetAccountId)
                return ServiceResult<bool>.Fail(400, "Счёт назначения должен отличаться от исходного счёта.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByIdAsync(receiptId,
                asNoTracking: false,
                include: query => query
                    .Include(r => r.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
                return ServiceResult<bool>.Fail(404, "Чек не найден.");

            if (receipt.CreatedByUserId != currentUserId)
                return ServiceResult<bool>.Fail(403, "Можно изменять только собственный чек.");

            ReceiptAccount? sourceLink = receipt.Accounts.FirstOrDefault(link => link.AccountId == sourceAccountId);
            if (sourceLink == null)
                return ServiceResult<bool>.Fail(404, "Чек не привязан к выбранному исходному счёту.");

            if (receipt.Accounts.Any(link => link.AccountId == targetAccountId))
                return ServiceResult<bool>.Fail(409, "Чек уже привязан к счёту назначения.");

            Account? sourceAccount = sourceLink.Account;
            if (sourceAccount == null)
                return ServiceResult<bool>.Fail(404, "Исходный счёт не найден.");

            ServiceResult<bool> sourceAccessResult = ValidateReceiptAccountModificationAccess(sourceAccount, currentUserId);
            if (!sourceAccessResult.Success)
                return ServiceResult<bool>.Fail(sourceAccessResult.Error!.StatusCode, sourceAccessResult.Error.Message);

            Account? targetAccount = await unitOfWork.Account.GetItemByPredicateAsync(a => a.Id == targetAccountId,
                asNoTracking: false,
                include: query => query.Include(account => account.Members),
                ct: ct);

            if (targetAccount == null)
                return ServiceResult<bool>.Fail(404, "Счёт назначения не найден.");

            ServiceResult<bool> targetAccessResult = ValidateReceiptAccountModificationAccess(targetAccount, currentUserId);
            if (!targetAccessResult.Success)
                return ServiceResult<bool>.Fail(targetAccessResult.Error!.StatusCode, targetAccessResult.Error.Message);

            Receipt? duplicateReceiptInTargetAccount = await unitOfWork.Receipt.GetItemByPredicateAsync(r =>
                r.Id != receiptId &&
                r.FiscalDriveNumber == receipt.FiscalDriveNumber &&
                r.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
                r.FiscalSign == receipt.FiscalSign &&
                r.DateTime == receipt.DateTime &&
                r.TotalSum == receipt.TotalSum &&
                r.OperationType == receipt.OperationType &&
                r.Accounts.Any(link => link.AccountId == targetAccountId),
                asNoTracking: true,
                ct: ct);

            if (duplicateReceiptInTargetAccount != null)
                return ServiceResult<bool>.Fail(409, "В счёте назначения уже есть такой же чек.");

            unitOfWork.ReceiptAccount.Delete(sourceLink);
            unitOfWork.ReceiptAccount.Create(new ReceiptAccount
            {
                ReceiptId = receiptId,
                AccountId = targetAccountId
            });

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> RemoveReceiptFromAccountAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByIdAsync(receiptId,
                asNoTracking: false,
                include: query => query
                    .Include(r => r.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            if (receipt == null)
                return ServiceResult<bool>.Fail(404, "Чек не найден.");

            ReceiptAccount? link = receipt.Accounts.FirstOrDefault(item => item.AccountId == accountId);
            if (link == null)
                return ServiceResult<bool>.Fail(404, "Чек не привязан к выбранному счёту.");

            Account? account = link.Account;
            if (account == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден.");

            ServiceResult<bool> accountAccessResult = ValidateReceiptAccountModificationAccess(account, currentUserId);
            if (!accountAccessResult.Success)
                return ServiceResult<bool>.Fail(accountAccessResult.Error!.StatusCode, accountAccessResult.Error.Message);

            if (receipt.Accounts.Count < 2)
            {
                unitOfWork.Receipt.Delete(receipt);
                await unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<bool>.Ok(true);
            }

            unitOfWork.ReceiptAccount.Delete(link);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }

        private static ServiceResult<string> NormalizeColorHex(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                return ServiceResult<string>.Ok(Account.DEFAULT_COLOR_HEX);

            string normalizedColorHex = colorHex.Trim().ToUpperInvariant();
            if (!colorHexRegex.IsMatch(normalizedColorHex))
                return ServiceResult<string>.Fail(400, "Некорректный цвет счёта.");

            return ServiceResult<string>.Ok(normalizedColorHex);
        }

        private static ServiceResult<bool> ValidateReceiptAccountModificationAccess(Account account, Guid currentUserId)
        {
            if (account.CreatedByUserId == currentUserId)
                return ServiceResult<bool>.Ok(true);

            AccountMember? membership = account.Members.FirstOrDefault(member => member.UserId == currentUserId);
            if (membership == null)
                return ServiceResult<bool>.Fail(403, "Нет доступа к указанному счёту.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult<bool>.Fail(403, "Недостаточно прав для изменения чека в выбранном счёте.");

            return ServiceResult<bool>.Ok(true);
        }
    }
}
