using CostVision.Models.Dto.Authorization;
using System.ComponentModel.DataAnnotations;

namespace CostVision.Models.Requests.Authorize
{
    public class RegisterRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "Заполните логин.")]
        public string Login { get; set; } = string.Empty;

        [Required]
        [MinLength(1, ErrorMessage = "Заполните имя.")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MinLength(1, ErrorMessage = "Заполните пароль.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MinLength(1, ErrorMessage = "Выберите хотя бы одну роль.")]
        public ICollection<Guid> Roles { get; set; } = new List<Guid>();
    }
}