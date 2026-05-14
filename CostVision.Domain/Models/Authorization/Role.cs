using CostVision.Domain.Models.Enums.Authorization;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Authorization
{
    public class Role : IEntity<Guid>
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public RoleType RoleType { get; set; } = RoleType.User;

        public ICollection<User> Users { get; set; } = new List<User>();

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
