using CostVision.Web.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Authorize.Attributes;
using CostVision.Web.Abstractions.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class ProductsModel(
        IGetProductListUseCase getProductListUseCase,
        IGetProductStorePurchasesUseCase getProductStorePurchasesUseCase,
        IGetUserAccountsUseCase getUserAccountsUseCase,
        IUpdateProductAdaptiveNameUseCase updateProductAdaptiveNameUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetListAsync([FromQuery] GetProductListRequest request, CancellationToken ct)
        {
            ServiceResult<ProductListDto> result = await getProductListUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnPostUpdateAdaptiveNameAsync([FromBody] UpdateProductAdaptiveNameRequest request, CancellationToken ct)
        {
            ServiceResult result = await updateProductAdaptiveNameUseCase.ExecuteAsync(request, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetStorePurchasesAsync([FromQuery] GetProductStorePurchasesRequest request, CancellationToken ct)
        {
            ServiceResult<List<ProductStorePurchaseDto>> result = await getProductStorePurchasesUseCase.ExecuteAsync(request, CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            ServiceResult<List<UserAccountViewModel>> result = await getUserAccountsUseCase.ExecuteAsync(CurrentUser.Id, includeArchived: false, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
