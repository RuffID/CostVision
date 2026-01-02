using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages
{
    [CookieAuthorize]
    public class IndexModel() : PageModel
    {
    }
}
