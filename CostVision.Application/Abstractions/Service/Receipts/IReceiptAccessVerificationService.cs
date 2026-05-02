using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.Abstractions.Service.Receipts
{
    public interface IReceiptAccessVerificationService
    {
        bool UserHasAccessToReceipt(User currentUser, Receipt receipt);
    }
}
