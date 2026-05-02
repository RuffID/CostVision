using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace CostVision.Infrastructure.Services.DataBase
{
    public class BackupService<TContext>(string connectionString, string backupFolder, ILoggerFactory logger) where TContext : DbContext
    {
        private readonly ILogger<BackupService<TContext>> _logger = logger.CreateLogger<BackupService<TContext>>();

        public void CreateSqlServerBackup()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);
            }

            string timestamp = DateTime.Now.ToString("yyyy.MM.dd_HHmmss");
            string backupFilePath = Path.Combine(backupFolder, $"backup_{timestamp}.bak");

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