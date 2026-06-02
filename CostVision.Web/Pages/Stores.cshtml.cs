using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Abstractions.Entity;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class StoresModel(
        IGetStoreListUseCase getStoreListUseCase,
        IUpdateStoreAdaptiveNameUseCase updateStoreAdaptiveNameUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetListAsync([FromQuery] GetStoreListRequest request, CancellationToken ct)
        {
            ServiceResult<StoreListDto> result = await getStoreListUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUpdateAdaptiveNameAsync([FromBody] UpdateStoreAdaptiveNameRequest request, CancellationToken ct)
        {
            ServiceResult<bool> result = await updateStoreAdaptiveNameUseCase.ExecuteAsync(request, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
