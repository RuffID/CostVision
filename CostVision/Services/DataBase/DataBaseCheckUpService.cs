using CostVision.Abstractions.DataBase;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Services.DataBase
{
    public class DataBaseCheckUpService<TContext>(IAppDbContext dbContext, ILoggerFactory logger, BackupService<TContext> backupService) where TContext : DbContext
    {
        private readonly ILogger<DataBaseCheckUpService<TContext>> _logger = logger.CreateLogger<DataBaseCheckUpService<TContext>>();

        public void CheckOrUpdateDB()
        {
            if (!dbContext.Database.CanConnect())
            {
                _logger.LogError("[Method:{MethodName}] Failed to connect to the database.", nameof(CheckOrUpdateDB));
                throw new Exception();
            }

            _logger.LogInformation($"[Method:{nameof(CheckOrUpdateDB)}] Connection to the database was successful.");

            List<string> pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();

            if (pendingMigrations.Count != 0)
            {
                foreach (string migration in pendingMigrations)                
                    _logger.LogInformation("[Method:{MethodName}] Pending migration: {Migration}.", migration, nameof(CheckOrUpdateDB));                

                backupService.CreateSqlServerBackup();

                dbContext.Database.Migrate();
                _logger.LogInformation("[Method:{MethodName}] Database was updated.", nameof(CheckOrUpdateDB));
            }
            else
                _logger.LogInformation("[Method:{MethodName}] No changes to the database.", nameof(CheckOrUpdateDB));
        }
    }
}
