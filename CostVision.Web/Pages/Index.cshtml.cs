using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Abstractions.Entity;
using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    [LoadUser]
    public class IndexModel(
        IGetDashboardIncomeExpenseReportUseCase getDashboardIncomeExpenseReportUseCase,
        IGetUserAccountsUseCase getUserAccountsUseCase) : PageModel, IHasCurrentUser
    {
        public User CurrentUser { get; set; } = null!;

        public async Task<JsonResult> OnGetAccountsAsync(CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getUserAccountsUseCase.ExecuteAsync(CurrentUser.Id, includeArchived: false, ct);
            return JsonResultMapper.ToJsonResult(ServiceResult<List<UserAccountViewModel>>.Ok(accounts));
        }

        public async Task<JsonResult> OnGetIncomeExpenseReportAsync([FromQuery] DashboardIncomeExpenseReportRequest request, CancellationToken ct)
        {
            ServiceResult<DashboardIncomeExpenseReportDto> result = await getDashboardIncomeExpenseReportUseCase.ExecuteAsync(CurrentUser, request, ct);
            return JsonResultMapper.ToJsonResult(result);
        }
    }
}
