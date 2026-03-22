using CostVision.Models.Authorization;
using CostVision.Models.Receipts;

namespace CostVision.Abstractions.Service.Receipts
{
    public interface IReceiptAccessVerificationService
    {
        bool UserHasAccessToReceipt(User currentUser, Receipt receipt);
    }
}