using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.Service.Receipts;
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

            Account? current = await unitOfWork.Account.GetItemByPredicate(a => a.Id == account.Id && a.CreatedByUserId == ownerUserId, ct: ct);

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

            Account? account = await unitOfWork.Account.GetItemByPredicate(a => a.Id == accountId && a.CreatedByUserId == ownerUserId,
                asNoTracking: true,
                include: q => q.Include(a => a.Members),
                ct: ct);

            if (account == null)
                return ServiceResult<List<AccountShareUserDto>>.Fail(404, "Счёт не найден.");

            HashSet<Guid> selectedUserIds = account.Members
                .Where(m => m.UserId != ownerUserId)
                .Select(m => m.UserId)
                .ToHashSet();

            List<User> users = await unitOfWork.User.GetItemsByPredicate(u => u.IsActive && u.Id != ownerUserId, asNoTracking: true, ct: ct);

            List<AccountShareUserDto> items = users
                .OrderBy(u => u.Name)
                .Select(u => new AccountShareUserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Login = u.Login,
                    IsSelected = selectedUserIds.Contains(u.Id)
                })
                .ToList();

            return ServiceResult<List<AccountShareUserDto>>.Ok(items);
        }

        public async Task<ServiceResult<bool>> UpdateAccountMembersAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetItemByPredicate(a => a.Id == accountId && a.CreatedByUserId == ownerUserId,
                asNoTracking: false,
                include: q => q.Include(a => a.Members),
                ct: ct);

            if (account == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден.");

            List<Guid> desiredUserIds = userIds
                .Where(x => x != Guid.Empty && x != ownerUserId)
                .Distinct()
                .ToList();

            List<User> availableUsers = desiredUserIds.Count == 0
                ? new()
                : await unitOfWork.User.GetItemsByPredicate(u => desiredUserIds.Contains(u.Id) && u.IsActive, asNoTracking: true, ct: ct);

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

            await unitOfWork.ExecuteInTransaction(async () =>
            {
                if (membersToRemove.Count > 0)
                    unitOfWork.AccountMember.DeleteRange(membersToRemove);

                foreach (Guid userId in userIdsToAdd)
                {
                    unitOfWork.AccountMember.Create(new AccountMember
                    {
                        AccountId = accountId,
                        UserId = userId,
                        Role = AccountAccessRole.Viewer
                    });
                }

                await Task.CompletedTask;
            }, ct);

            return ServiceResult<bool>.Ok(true);
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
            await unitOfWork.SaveChangesAsync(ct);
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
                ColorHex = account.ColorHex,
                IsActive = !account.IsArchived,
                CanManage = account.CreatedByUserId == userId
            }).ToList();
        }

        public async Task<ReceiptAccountLinkResult> LinkReceiptToAccountAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            // Проверяет, что счёт существует и пользователь имеет к нему доступ
            Account? account = await unitOfWork.Account.GetItemByPredicate(a => a.Id == accountId, asNoTracking: false, include: a => a.Include(a => a.Members), ct: ct);

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
            await unitOfWork.SaveChangesAsync(ct);

            return new ReceiptAccountLinkResult
            {
                Status = ReceiptAccountLinkStatusEnum.Success,
                Link = link
            };
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
    }
}
