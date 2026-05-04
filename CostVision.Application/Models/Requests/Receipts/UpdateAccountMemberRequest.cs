using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.Models.Requests.Receipts
{
    public class UpdateAccountMemberRequest
    {
        public Guid UserId { get; set; }

        public AccountAccessRole Role { get; set; }
    }
}