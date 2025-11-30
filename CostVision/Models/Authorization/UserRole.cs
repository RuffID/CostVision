using CostVision.Interfaces.Entity;

namespace CostVision.Models.Authorization
{
    public class UserRole : ICopyable<UserRole>
    {
        public Guid UserId { get; set; }

        public Guid RoleId { get; set; }

        public virtual Role? Role { get; set; }

        public virtual User? User { get; set; }

        public void CopyData(UserRole newItem)
        {
            UserId = newItem.UserId;
            RoleId = newItem.RoleId;
        }
    }
}
