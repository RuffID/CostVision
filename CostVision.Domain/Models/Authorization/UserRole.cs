namespace CostVision.Domain.Models.Authorization
{
    public class UserRole
    {
        private User? _user;
        private Role? _role;

        private UserRole()
        {
        }

        public Guid UserId { get; private set; }

        public Guid RoleId { get; private set; }

        public Role? Role => _role;

        public User? User => _user;

        internal static UserRole Create(User user, Guid roleId)
        {
            return new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                _user = user
            };
        }

        internal static UserRole Create(User user, Role role)
        {
            return new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                _user = user,
                _role = role
            };
        }
    }
}
