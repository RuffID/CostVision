using CostVision.Domain.Models.Authorization;

namespace CostVision.Web.Abstractions.Entity
{
    public interface IHasCurrentUser
    {
        User CurrentUser { get; set; }
    }
}
