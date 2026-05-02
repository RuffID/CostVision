﻿namespace CostVision.Application.Models.Dtos.Authorization
{
    public class UserEditDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Login { get; set; } = string.Empty;

        public List<Guid> RoleIds { get; set; } = new();
    }
}
