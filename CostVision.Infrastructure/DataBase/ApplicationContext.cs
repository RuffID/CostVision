using CostVision.Application.Abstractions.DataBase;
using CostVision.Infrastructure.DataBase.ModelsConfigure.Authorization;
using CostVision.Infrastructure.DataBase.ModelsConfigure.MoneyMovements;
using CostVision.Infrastructure.DataBase.ModelsConfigure.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Infrastructure.DataBase
{
    public partial class ApplicationContext(DbContextOptions<ApplicationContext> options) : AppDbContextBase(options)
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
                .ApplyConfiguration(new ProductConfigure())
                .ApplyConfiguration(new MoneyMovementConfigure())
                .ApplyConfiguration(new MoneyMovementReceiptConfigure());

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
