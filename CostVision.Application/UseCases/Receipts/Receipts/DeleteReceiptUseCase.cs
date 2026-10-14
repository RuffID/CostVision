using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class DeleteReceiptUseCase(IUnitOfWorkScope unitOfWork, IReceiptRepository receiptRepository) : IDeleteReceiptUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");

            Receipt? receipt = await receiptRepository.GetItemByIdAsync(id: receiptId, asNoTracking: false, ct: ct);
            if (receipt == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не найден.");

            if (receipt.CreatedByUserId != currentUser.Id)
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Можно удалять только собственный чек.");

            receiptRepository.Delete(receipt);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
