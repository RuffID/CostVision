using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.Extensions;

public static class RequestShapeValidation
{
    public static ServiceResult Validate(object? request)
    {
        if (request == null)
        {
            return ServiceResult.Fail(ServiceErrorType.Validation, "Отсутствуют данные запроса.");
        }

        bool invalidShape = request switch
        {
            QrScanRequest scan => scan.Results == null || scan.Results.Any(item => item == null),
            ReceiptManualCreateRequest manual => manual.Receipt == null,
            SaveBankStatementImportRequest import => import.Rows == null || import.Rows.Any(item => item == null),
            UpdateAccountMembersRequest members => members.Members == null || members.Members.Any(item => item == null),
            UserUpsertRequest user => user.RoleIds == null,
            DashboardIncomeExpenseReportRequest report => report.AccountIds == null || report.StoreNames == null ||
                report.StoreNames.Any(item => item == null),
            _ => false
        };
        if (invalidShape)
        {
            return ServiceResult.Fail(ServiceErrorType.Validation, "Некорректная форма запроса: проверьте обязательные данные и элементы списка.");
        }

        return ServiceResult.Ok();
    }
}
