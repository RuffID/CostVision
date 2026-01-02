using CostVision.Models.Dtos.Authorization;
using CostVision.Services.Authorize.Attributes;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CostVision.Pages
{
    [CookieAuthorize]
    public class RegisterModel : PageModel
    {
        [Required(ErrorMessage = "Укажите логин.")]
        [Display(Name = "Логин")]
        [StringLength(100, ErrorMessage = "Логин должен быть не длиннее {1} символов.")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите имя.")]
        [Display(Name = "Имя")]
        [StringLength(100, ErrorMessage = "Имя должно быть не длиннее {1} символов.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите пароль.")]
        [Display(Name = "Пароль")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть не короче {2} символов.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Подтвердите пароль.")]
        [Display(Name = "Подтверждение пароля")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Пароль и подтверждение пароля должны совпадать.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public List<RoleDto> AvailableRoles { get; set; } = new List<RoleDto>();

        [Required(ErrorMessage = "Выбери хотя бы одну роль.")]
        public List<Guid> SelectedRoleIds { get; set; } = new List<Guid>();

        public void OnGet()
        {
            // В реальном коде брать из БД через IUnitOfWork/репозиторий
            // Здесь захардкодить. GUID'ы подставь реальные Id ролей из таблицы Role.
            AvailableRoles = new ()
            {
                new ()
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Администратор"
                },
                new ()
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Пользователь"
                }
            };
        }
    }
}
