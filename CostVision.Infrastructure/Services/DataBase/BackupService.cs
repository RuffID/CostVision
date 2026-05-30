using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace CostVision.Infrastructure.Services.DataBase
{
    public class BackupService<TContext>(
        string connectionString,
        string backupFolder,
        ILoggerFactory logger,
        IBackupFilePathBuilder backupFilePathBuilder) : IBackupService<TContext> where TContext : DbContext
    {
        private readonly ILogger<BackupService<TContext>> _logger = logger.CreateLogger<BackupService<TContext>>();

        public BackupService(string connectionString, string backupFolder, ILoggerFactory logger)
            : this(connectionString, backupFolder, logger, new BackupFilePathBuilder())
        {
        }

        public void CreateSqlServerBackup()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);
            }

            string backupFilePath = backupFilePathBuilder.Build(backupFolder);

            using SqlConnection connection = new(connectionString);
            connection.Open();

            string dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            string sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{backupFilePath}' WITH FORMAT, INIT, NAME = 'Scheduled Backup';";

            using SqlCommand command = new(sql, connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("[Method:{MethodName}] Backup created at: {BackupFilePath}", nameof(CreateSqlServerBackup), backupFilePath);
        }
    }
}
