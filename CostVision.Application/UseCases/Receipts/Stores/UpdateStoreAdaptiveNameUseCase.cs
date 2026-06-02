using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public class UpdateStoreAdaptiveNameUseCase(IUnitOfWork unitOfWork) : IUpdateStoreAdaptiveNameUseCase
    {
        private const int MAX_ADAPTIVE_NAME_LENGTH = 500;

        public async Task<ServiceResult<bool>> ExecuteAsync(UpdateStoreAdaptiveNameRequest request, CancellationToken ct)
        {
            if (request.StoreId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Магазин не указан.");

            if (request.AdaptiveName?.Length > MAX_ADAPTIVE_NAME_LENGTH)
                return ServiceResult<bool>.Fail(400, "Адаптивное название не должно быть длиннее 500 символов.");

            Store? store = await unitOfWork.Store.GetItemByIdAsync(request.StoreId, ct: ct);
            if (store == null)
                return ServiceResult<bool>.Fail(404, "Магазин не найден.");

            store.UpdateAdaptiveName(request.AdaptiveName);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
