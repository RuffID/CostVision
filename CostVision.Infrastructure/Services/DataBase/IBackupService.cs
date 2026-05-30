using Microsoft.EntityFrameworkCore;

namespace CostVision.Infrastructure.Services.DataBase
{
    public interface IBackupService<TContext> where TContext : DbContext
    {
        void CreateSqlServerBackup();
    }
}
