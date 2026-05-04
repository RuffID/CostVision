using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.Models.Dtos.Authorization
{
    public class RoleDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public RoleType RoleType { get; set; }
    }
}
