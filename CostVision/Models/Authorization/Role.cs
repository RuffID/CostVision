using CostVision.Interfaces.Entity;

namespace CostVision.Models.Authorization
{
    public class Role : IEntity<Guid>, ICopyable<Role>
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public virtual ICollection<User> Users { get; set; } = new List<User>();

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public void CopyData(Role role)
        {
            Name = role.Name;
        }
    }
}
