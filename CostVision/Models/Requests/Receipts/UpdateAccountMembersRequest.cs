namespace CostVision.Models.Requests.Receipts
{
    public class UpdateAccountMembersRequest
    {
        public Guid AccountId { get; set; }

        public List<Guid> UserIds { get; set; } = new();
    }
}
