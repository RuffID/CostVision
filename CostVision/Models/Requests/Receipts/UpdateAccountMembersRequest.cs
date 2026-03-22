namespace CostVision.Models.Requests.Receipts
{
    public class UpdateAccountMembersRequest
    {
        public Guid AccountId { get; set; }

        public List<UpdateAccountMemberRequest> Members { get; set; } = new();
    }
}