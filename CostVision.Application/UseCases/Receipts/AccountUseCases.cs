using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using System.Text.RegularExpressions;

namespace CostVision.Application.UseCases.Receipts
{
    public interface IGetUserAccountsUseCase
    {
        Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, bool includeArchived, CancellationToken ct);
    }

    public interface IGetUserAccountsForReceiptCreationUseCase
    {
        Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, CancellationToken ct);
    }

    public interface IValidateReceiptCreationAccessUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid userId, CancellationToken ct);
    }

    public interface ICreateAccountUseCase
    {
        Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct);
    }

    public interface IUpdateAccountUseCase
    {
        Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, Account account, CancellationToken ct);
    }

    public interface IGetAccountShareUsersUseCase
    {
        Task<ServiceResult<List<AccountShareUserDto>>> ExecuteAsync(Guid accountId, Guid ownerUserId, CancellationToken ct);
    }

    public interface IUpdateAccountMembersUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<UpdateAccountMemberRequest> members, CancellationToken ct);
    }

    public interface IMoveReceiptToAccountUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid sourceAccountId, Guid targetAccountId, Guid receiptId, Guid currentUserId, CancellationToken ct);
    }

    public interface IRemoveReceiptFromAccountUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct);
    }

    public class GetUserAccountsUseCase(IUnitOfWork unitOfWork) : IGetUserAccountsUseCase
    {
        public async Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, bool includeArchived, CancellationToken ct)
        {
            List<AccountMember> memberships = await unitOfWork.AccountMember.GetItemsByPredicateAsync(membership => membership.UserId == userId, asNoTracking: true, ct: ct);
            List<Guid> accountIds = memberships.Select(membership => membership.AccountId).Distinct().ToList();
            if (accountIds.Count == 0)
                return new List<UserAccountViewModel>();

            List<Account> accounts = await unitOfWork.Account.GetAccessibleByUserAsync(userId, includeArchived, ct);
            Dictionary<Guid, AccountAccessRole> rolesByAccountId = memberships
                .GroupBy(membership => membership.AccountId)
                .ToDictionary(group => group.Key, group => group.Select(membership => membership.Role).First());

            return accounts.Select(account => new UserAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                Description = account.Description,
                ColorHex = account.ColorHex,
                IsActive = !account.IsArchived,
                CanManage = account.CreatedByUserId == userId,
                OwnerName = account.CreatedByUser!.Name,
                AccessRole = rolesByAccountId[account.Id]
            }).ToList();
        }
    }

    public class GetUserAccountsForReceiptCreationUseCase(IGetUserAccountsUseCase getUserAccountsUseCase) : IGetUserAccountsForReceiptCreationUseCase
    {
        public async Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getUserAccountsUseCase.ExecuteAsync(userId, includeArchived: false, ct);

            return accounts
                .Where(account => account.AccessRole == AccountAccessRole.Owner || account.AccessRole == AccountAccessRole.Editor)
                .ToList();
        }
    }

    public class ValidateReceiptCreationAccessUseCase(IUnitOfWork unitOfWork) : IValidateReceiptCreationAccessUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid userId, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            AccountMember? membership = await unitOfWork.AccountMember.GetItemByPredicateAsync(
                member => member.AccountId == accountId && member.UserId == userId,
                asNoTracking: true,
                ct: ct);

            if (membership == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден или доступ к нему отсутствует.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult<bool>.Fail(403, "Недостаточно прав для добавления чеков в этот счёт.");

            return ServiceResult<bool>.Ok(true);
        }
    }

    public class CreateAccountUseCase(IUnitOfWork unitOfWork) : ICreateAccountUseCase
    {
        public async Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct)
        {
            ServiceResult<string> colorHexResult = AccountColorHexNormalizer.Normalize(request.ColorHex);
            if (!colorHexResult.Success || colorHexResult.Data == null)
                return ServiceResult<Account>.Fail(colorHexResult.Error!.StatusCode, colorHexResult.Error.Message);

            Account account = new()
            {
                Name = request.Name,
                Description = request.Description,
                ColorHex = colorHexResult.Data,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = ownerUserId
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
    }

    public class UpdateAccountUseCase(IUnitOfWork unitOfWork) : IUpdateAccountUseCase
    {
        public async Task<ServiceResult<Account>> ExecuteAsync(Guid ownerUserId, Account account, CancellationToken ct)
        {
            ServiceResult<string> colorHexResult = AccountColorHexNormalizer.Normalize(account.ColorHex);
            if (!colorHexResult.Success || colorHexResult.Data == null)
                return ServiceResult<Account>.Fail(colorHexResult.Error!.StatusCode, colorHexResult.Error.Message);

            Account? current = await unitOfWork.Account.GetItemByPredicateAsync(item => item.Id == account.Id && item.CreatedByUserId == ownerUserId, ct: ct);
            if (current == null)
                return ServiceResult<Account>.Fail(404, "Счёт не найден.");

            current.Name = account.Name;
            current.Description = account.Description;
            current.ColorHex = colorHexResult.Data;
            current.IsArchived = account.IsArchived;

            await unitOfWork.SaveChangesAsync(ct);

            account.ColorHex = current.ColorHex;
            account.IsArchived = current.IsArchived;

            return ServiceResult<Account>.Ok(account);
        }
    }

    public class GetAccountShareUsersUseCase(IUnitOfWork unitOfWork) : IGetAccountShareUsersUseCase
    {
        public async Task<ServiceResult<List<AccountShareUserDto>>> ExecuteAsync(Guid accountId, Guid ownerUserId, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<List<AccountShareUserDto>>.Fail(400, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetOwnedByIdWithMembersAsync(accountId, ownerUserId, asNoTracking: true, ct: ct);
            if (account == null)
                return ServiceResult<List<AccountShareUserDto>>.Fail(404, "Счёт не найден.");

            Dictionary<Guid, AccountAccessRole> selectedUsers = account.Members
                .Where(member => member.UserId != ownerUserId)
                .ToDictionary(member => member.UserId, member => member.Role);

            List<User> users = await unitOfWork.User.GetItemsByPredicateAsync(user => user.IsActive && user.Id != ownerUserId, asNoTracking: true, ct: ct);

            List<AccountShareUserDto> items = users
                .OrderBy(user => user.Name)
                .Select(user => new AccountShareUserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Login = user.Login,
                    IsSelected = selectedUsers.ContainsKey(user.Id),
                    Role = selectedUsers.TryGetValue(user.Id, out AccountAccessRole role) ? role : null
                })
                .ToList();

            return ServiceResult<List<AccountShareUserDto>>.Ok(items);
        }
    }

    public class UpdateAccountMembersUseCase(IUnitOfWork unitOfWork) : IUpdateAccountMembersUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<UpdateAccountMemberRequest> members, CancellationToken ct)
        {
            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            Account? account = await unitOfWork.Account.GetOwnedByIdWithMembersAsync(accountId, ownerUserId, asNoTracking: false, ct: ct);
            if (account == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден.");

            List<UpdateAccountMemberRequest> desiredMembers = members
                .Where(member => member.UserId != Guid.Empty && member.UserId != ownerUserId)
                .GroupBy(member => member.UserId)
                .Select(group => group.Last())
                .ToList();

            if (desiredMembers.Any(member => !Enum.IsDefined(typeof(AccountAccessRole), member.Role) || member.Role == AccountAccessRole.Owner))
                return ServiceResult<bool>.Fail(400, "Можно назначить только роли Viewer или Editor.");

            List<Guid> desiredUserIds = desiredMembers.Select(member => member.UserId).ToList();
            List<User> availableUsers = desiredUserIds.Count == 0
                ? new List<User>()
                : await unitOfWork.User.GetItemsByPredicateAsync(user => desiredUserIds.Contains(user.Id) && user.IsActive, asNoTracking: true, ct: ct);

            if (availableUsers.Count != desiredUserIds.Count)
                return ServiceResult<bool>.Fail(400, "Один или несколько выбранных пользователей недоступны.");

            List<AccountMember> currentMembers = account.Members
                .Where(member => member.UserId != ownerUserId)
                .ToList();

            HashSet<Guid> currentUserIds = currentMembers
                .Select(member => member.UserId)
                .ToHashSet();

            List<AccountMember> membersToRemove = currentMembers
                .Where(member => !desiredUserIds.Contains(member.UserId))
                .ToList();

            List<Guid> userIdsToAdd = desiredUserIds
                .Where(userId => !currentUserIds.Contains(userId))
                .ToList();

            List<AccountMember> membersToUpdate = currentMembers
                .Where(member => desiredUserIds.Contains(member.UserId))
                .ToList();

            Dictionary<Guid, AccountAccessRole> desiredRolesByUserId = desiredMembers
                .ToDictionary(member => member.UserId, member => member.Role);

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
    }

    public class MoveReceiptToAccountUseCase(IUnitOfWork unitOfWork) : IMoveReceiptToAccountUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid sourceAccountId, Guid targetAccountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

            if (sourceAccountId == Guid.Empty || targetAccountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            if (sourceAccountId == targetAccountId)
                return ServiceResult<bool>.Fail(400, "Счёт назначения должен отличаться от исходного счёта.");

            Receipt? receipt = await unitOfWork.Receipt.GetByIdWithAccountsAndMembersAsync(receiptId, asNoTracking: false, ct: ct);
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

            ServiceResult<bool> sourceAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(sourceAccount, currentUserId);
            if (!sourceAccessResult.Success)
                return ServiceResult<bool>.Fail(sourceAccessResult.Error!.StatusCode, sourceAccessResult.Error.Message);

            Account? targetAccount = await unitOfWork.Account.GetByIdWithMembersAsync(targetAccountId, asNoTracking: false, ct: ct);
            if (targetAccount == null)
                return ServiceResult<bool>.Fail(404, "Счёт назначения не найден.");

            ServiceResult<bool> targetAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(targetAccount, currentUserId);
            if (!targetAccessResult.Success)
                return ServiceResult<bool>.Fail(targetAccessResult.Error!.StatusCode, targetAccessResult.Error.Message);

            Receipt? duplicateReceiptInTargetAccount = await unitOfWork.Receipt.GetItemByPredicateAsync(item =>
                item.Id != receiptId &&
                item.FiscalDriveNumber == receipt.FiscalDriveNumber &&
                item.FiscalDocumentNumber == receipt.FiscalDocumentNumber &&
                item.FiscalSign == receipt.FiscalSign &&
                item.DateTime == receipt.DateTime &&
                item.TotalSum == receipt.TotalSum &&
                item.OperationType == receipt.OperationType &&
                item.Accounts.Any(link => link.AccountId == targetAccountId),
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
    }

    public class RemoveReceiptFromAccountUseCase(IUnitOfWork unitOfWork) : IRemoveReceiptFromAccountUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            Receipt? receipt = await unitOfWork.Receipt.GetByIdWithAccountsAndMembersAsync(receiptId, asNoTracking: false, ct: ct);
            if (receipt == null)
                return ServiceResult<bool>.Fail(404, "Чек не найден.");

            ReceiptAccount? link = receipt.Accounts.FirstOrDefault(item => item.AccountId == accountId);
            if (link == null)
                return ServiceResult<bool>.Fail(404, "Чек не привязан к выбранному счёту.");

            Account? account = link.Account;
            if (account == null)
                return ServiceResult<bool>.Fail(404, "Счёт не найден.");

            ServiceResult<bool> accountAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(account, currentUserId);
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
    }

    internal static class AccountColorHexNormalizer
    {
        private static readonly Regex COLOR_HEX_REGEX = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        public static ServiceResult<string> Normalize(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                return ServiceResult<string>.Ok(Account.DEFAULT_COLOR_HEX);

            string normalizedColorHex = colorHex.Trim().ToUpperInvariant();
            if (!COLOR_HEX_REGEX.IsMatch(normalizedColorHex))
                return ServiceResult<string>.Fail(400, "Некорректный цвет счёта.");

            return ServiceResult<string>.Ok(normalizedColorHex);
        }
    }

    internal static class AccountReceiptAccessValidator
    {
        public static ServiceResult<bool> ValidateModificationAccess(Account account, Guid currentUserId)
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
