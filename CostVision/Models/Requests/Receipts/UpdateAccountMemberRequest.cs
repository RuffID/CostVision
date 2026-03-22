using CostVision.Models.Enums.Authorization;

namespace CostVision.Models.Requests.Receipts
{
    public class UpdateAccountMemberRequest
    {
        public Guid UserId { get; set; }

        public AccountAccessRole Role { get; set; }
    }
}