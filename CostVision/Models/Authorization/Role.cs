using CostVision.Models.Enums.Authorization;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Models.Authorization
{
    public class Role : IEntity<Guid>
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public RoleType RoleType { get; set; } = RoleType.User;

        public virtual ICollection<User> Users { get; set; } = new List<User>();

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
