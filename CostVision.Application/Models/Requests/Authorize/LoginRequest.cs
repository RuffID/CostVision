﻿using System.ComponentModel.DataAnnotations;

namespace CostVision.Application.Models.Requests.Authorize
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Логин обязателен.")]
        public string Login { get; set; } = string.Empty;
        [Required(ErrorMessage = "Пароль обязателен.")]
        public string Password { get; set; } = string.Empty;
    }
}
