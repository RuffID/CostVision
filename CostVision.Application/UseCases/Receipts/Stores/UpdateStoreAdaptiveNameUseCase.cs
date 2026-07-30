using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public class UpdateStoreAdaptiveNameUseCase(IUnitOfWork unitOfWork) : IUpdateStoreAdaptiveNameUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(UpdateStoreAdaptiveNameRequest request, CancellationToken ct)
        {
            if (request.StoreId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Магазин не указан.");

            if (!Store.TryNormalizeAdaptiveName(request.AdaptiveName, out string? adaptiveName, out string? error))
                return ServiceResult<bool>.Fail(400, error!);

            Store? store = await unitOfWork.Store.GetItemByIdAsync(request.StoreId, ct: ct);
            if (store == null)
                return ServiceResult<bool>.Fail(404, "Магазин не найден.");

            if (!store.TryUpdateAdaptiveName(adaptiveName, out error))
                return ServiceResult<bool>.Fail(400, error!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
