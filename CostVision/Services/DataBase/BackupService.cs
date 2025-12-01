using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Services.DataBase
{
    public class BackupService<TContext>(string connectionString, string backupFolder, ILoggerFactory logger) where TContext : DbContext
    {
        private readonly ILogger<BackupService<TContext>> _logger = logger.CreateLogger<BackupService<TContext>>();

        public void CreateSqlServerBackup()
        {
            string timestamp = DateTime.Now.ToString("yyyy.MM.dd_HHmmss");
            string backupFilePath = Path.Combine(backupFolder, $"backup_{timestamp}.sql");
            Directory.CreateDirectory(backupFolder);
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
