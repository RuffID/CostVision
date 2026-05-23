using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Application.UseCases.Receipts.Receipts.Helpers;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class RemoveReceiptFromAccountUseCase(IUnitOfWork unitOfWork) : IRemoveReceiptFromAccountUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

            if (accountId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор счёта.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                receipt => receipt.Id == receiptId,
                asNoTracking: false,
                include: query => query
                    .Include(receipt => receipt.Accounts)
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

            ServiceResult<bool> accountAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(account, currentUserId);
            if (!accountAccessResult.Success)
                return ServiceResult<bool>.Fail(accountAccessResult.Error!.StatusCode, accountAccessResult.Error.Message);

            if (receipt.Accounts.Count < 2)
            {
                unitOfWork.Receipt.Delete(receipt);
                await UserActivityUpdater.MarkReceiptActivityAsync(unitOfWork, currentUserId, ct);
                await unitOfWork.SaveChangesAsync(ct);
                return ServiceResult<bool>.Ok(true);
            }

            unitOfWork.ReceiptAccount.Delete(link);
            await UserActivityUpdater.MarkReceiptActivityAsync(unitOfWork, currentUserId, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
