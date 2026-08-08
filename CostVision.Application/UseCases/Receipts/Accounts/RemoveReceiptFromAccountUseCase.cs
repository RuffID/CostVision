using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts.Helpers;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class RemoveReceiptFromAccountUseCase(IUnitOfWork unitOfWork) : IRemoveReceiptFromAccountUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");

            if (accountId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор счёта.");

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
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не найден.");

            ReceiptAccount? link = receipt.Accounts.FirstOrDefault(item => item.AccountId == accountId);
            if (link == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не привязан к выбранному счёту.");

            Account? account = link.Account;
            if (account == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Счёт не найден.");

            ServiceResult accountAccessResult = AccountReceiptAccessValidator.ValidateModificationAccess(account, currentUserId);
            if (!accountAccessResult.Success)
                return accountAccessResult.PropagateFailure();

            if (receipt.Accounts.Count < 2)
            {
                unitOfWork.Receipt.Delete(receipt);
                await unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Ok();
            }

            if (!receipt.TryRemoveAccount(accountId, out ReceiptAccount? removedLink, out string? removeError))
                return ServiceResult.Fail(
                    ServiceErrorType.Conflict,
                    removeError ?? throw new InvalidOperationException("Доменная операция не вернула причину отказа."));

            unitOfWork.ReceiptAccount.Delete(removedLink!);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
