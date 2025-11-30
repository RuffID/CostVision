using CostVision.Models.Authorization;

namespace CostVision.Interfaces.Entity
{
    public interface IHasCurrentUser
    {
        User CurrentUser { get; set; }
    }
}
