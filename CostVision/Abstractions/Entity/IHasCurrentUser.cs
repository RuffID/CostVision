using CostVision.Models.Authorization;

namespace CostVision.Abstractions.Entity
{
    public interface IHasCurrentUser
    {
        User CurrentUser { get; set; }
    }
}
