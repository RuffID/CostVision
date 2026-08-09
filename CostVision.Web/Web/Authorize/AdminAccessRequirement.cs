using Microsoft.AspNetCore.Authorization;

namespace CostVision.Web.Authorize
{
    /// <summary>
    /// Требует наличие актуальной роли администратора.
    /// </summary>
    public class AdminAccessRequirement : IAuthorizationRequirement
    {
    }
}
