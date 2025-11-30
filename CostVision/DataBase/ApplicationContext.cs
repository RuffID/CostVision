using CostVision.DataBase.ModelsConfigure.Authorization;
using CostVision.DataBase.ModelsConfigure.Products;
using CostVision.DataBase.ModelsConfigure.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.DataBase
{
    public partial class ApplicationContext(DbContextOptions<ApplicationContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .ApplyConfiguration(new UserConfigure())
                .ApplyConfiguration(new RoleConfigure())
                .ApplyConfiguration(new UserRoleConfigure())
                .ApplyConfiguration(new AccountConfigure())
                .ApplyConfiguration(new ReceiptAccountConfigure())
                .ApplyConfiguration(new AccountMemberConfigure())
                .ApplyConfiguration(new ExpenseCategoryConfigure())
                .ApplyConfiguration(new ReceiptConfigure())
                .ApplyConfiguration(new ReceiptItemConfigure())
                .ApplyConfiguration(new ProductConfigure());

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
