﻿namespace CostVision.Application.Models.Requests.Authorize
{
    public class UserUpsertRequest
    {
        public Guid? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Login { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public List<Guid> RoleIds { get; set; } = new();
    }
}
