using CostVision.Web.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Web.Pages
{
    [CookieAuthorize]
    public class IndexModel() : PageModel
    {
    }
}
