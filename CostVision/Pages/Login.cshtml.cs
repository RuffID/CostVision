using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CostVision.Pages
{
    public class LoginModel : PageModel
    {
        [Required(ErrorMessage = "”кажи логин.")]
        [Display(Name = "Ћогин")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "”кажи пароль.")]
        [Display(Name = "ѕароль")]
        public string Password { get; set; } = string.Empty;

        public void OnGet()
        {
        }
    }
}