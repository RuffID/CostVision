using CostVision.Services.Attributes;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CostVision.Pages
{
    [CookieAuthorize]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {

        }
    }
}
