using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Accounts.Helpers
{
    internal static class AccountReceiptAccessValidator
    {
        public static ServiceResult ValidateModificationAccess(Account account, Guid currentUserId)
        {
            if (account.CreatedByUserId == currentUserId)
                return ServiceResult.Ok();

            AccountMember? membership = account.Members.FirstOrDefault(member => member.UserId == currentUserId);
            if (membership == null)
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Нет доступа к указанному счёту.");

            if (membership.Role == AccountAccessRole.Viewer)
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Недостаточно прав для изменения чека в выбранном счёте.");

            return ServiceResult.Ok();
        }
    }
}
