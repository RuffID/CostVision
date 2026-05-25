using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Abstractions.Entity;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class UserActivityModel(IMarkUserActivityUseCase markUserActivityUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnPostPingAsync(CancellationToken ct)
        {
            ServiceResult<bool> result = await markUserActivityUseCase.ExecuteAsync(CurrentUser.Id, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
