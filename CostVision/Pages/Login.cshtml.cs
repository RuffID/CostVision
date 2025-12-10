using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CostVision.Pages
{
    public class LoginModel : PageModel
    {
        [Required(ErrorMessage = "Укажи логин.")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажи пароль.")]
        public string Password { get; set; } = string.Empty;

        public void OnGet()
        {
        }
    }
}