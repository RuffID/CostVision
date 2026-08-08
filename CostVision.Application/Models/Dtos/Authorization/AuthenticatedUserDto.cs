using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.Models.Dtos.Authorization
{
    public class AuthenticatedUserDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<RoleType> Roles { get; set; } = new();
    }
}
