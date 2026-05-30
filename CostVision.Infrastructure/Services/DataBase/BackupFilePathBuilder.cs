using System.Reflection;

namespace CostVision.Infrastructure.Services.DataBase
{
    public class BackupFilePathBuilder : IBackupFilePathBuilder
    {
        public string Build(string backupFolder)
        {
            return Build(backupFolder, DateTime.Now);
        }

        public string Build(string backupFolder, DateTime timestamp)
        {
            string projectName = GetProjectName();
            string formattedTimestamp = timestamp.ToString("yyyy.MM.dd_HHmmss");

            return Path.Combine(backupFolder, $"backup_{projectName}_{formattedTimestamp}.bak");
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
