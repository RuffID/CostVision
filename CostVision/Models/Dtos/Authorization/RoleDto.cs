using CostVision.Models.Enums.Authorization;

namespace CostVision.Models.Dtos.Authorization
{
    public class RoleDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public RoleType RoleType { get; set; }
    }
}
