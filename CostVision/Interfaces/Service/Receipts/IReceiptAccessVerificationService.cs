using CostVision.Models.Authorization;
using CostVision.Models.Receipts;

namespace CostVision.Interfaces.Service.Receipts
{
    public interface IReceiptAccessVerificationService
    {
        bool UserHasAccessToReceipt(User currentUser, Receipt receipt);
    }
}