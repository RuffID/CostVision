using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;
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

            string projectName = GetProjectName();
            string timestamp = DateTime.Now.ToString("yyyy.MM.dd_HHmmss");
            string backupFilePath = Path.Combine(backupFolder, $"backup_{projectName}_{timestamp}.bak");

            using SqlConnection connection = new(connectionString);
            connection.Open();

            string dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            string sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{backupFilePath}' WITH FORMAT, INIT, NAME = 'Scheduled Backup';";

            using SqlCommand command = new(sql, connection);
            command.ExecuteNonQuery();

            _logger.LogInformation("[Method:{MethodName}] Backup created at: {BackupFilePath}", nameof(CreateSqlServerBackup), backupFilePath);
        }

        private static string GetProjectName()
        {
            string? projectName = Assembly.GetEntryAssembly()?.GetName().Name;

            if (string.IsNullOrWhiteSpace(projectName))
                throw new InvalidOperationException("Не удалось определить имя проекта для имени файла резервной копии.");

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                projectName = projectName.Replace(invalidChar, '_');
            }

            return projectName;
        }
    }
}