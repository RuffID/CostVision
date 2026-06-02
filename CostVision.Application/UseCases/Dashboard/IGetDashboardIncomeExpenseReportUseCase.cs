using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.Dashboard
{
    public interface IGetDashboardIncomeExpenseReportUseCase
    {
        Task<ServiceResult<DashboardIncomeExpenseReportDto>> ExecuteAsync(User currentUser, DashboardIncomeExpenseReportRequest request, CancellationToken ct);
    }
}
