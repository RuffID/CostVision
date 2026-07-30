namespace CostVision.Domain.Models.Authorization
{
    public class UserRole
    {
        internal UserRole()
        {
        }

        public Guid UserId { get; internal set; }

        public Guid RoleId { get; internal set; }

        public Role? Role { get; internal set; }

        public User? User { get; internal set; }

        internal static UserRole Create(User user, Guid roleId)
        {
            return new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                User = user
            };
        }
    }
}
