using CostVision.Abstractions.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.Receipts;

namespace CostVision.Services.Helpers
{
    public class ReceiptAccessVerificationService : IReceiptAccessVerificationService
    {
        public bool UserHasAccessToReceipt(User currentUser, Receipt receipt)
        {
            if (receipt.CreatedByUserId == currentUser.Id)
                return true;

            bool hasAccountLink = receipt.Accounts
                .Any(link =>
                    link.Account != null &&
                    (
                        link.Account.CreatedByUserId == currentUser.Id ||
                        link.Account.Members.Any(m => m.UserId == currentUser.Id)
                    ));

            return hasAccountLink;
        }
    }
}
