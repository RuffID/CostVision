using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Domain.Models.Authorization
{
    public class Role
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public RoleType RoleType { get; set; } = RoleType.User;

        public ICollection<User> Users { get; set; } = new List<User>();

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
