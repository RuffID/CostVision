using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class DeleteReceiptUseCase(IUnitOfWork unitOfWork) : IDeleteReceiptUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemByIdAsync(id: receiptId, asNoTracking: false, ct: ct);
            if (receipt == null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Чек не найден.");

            if (receipt.CreatedByUserId != currentUser.Id)
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Можно удалять только собственный чек.");

            unitOfWork.Receipt.Delete(receipt);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
