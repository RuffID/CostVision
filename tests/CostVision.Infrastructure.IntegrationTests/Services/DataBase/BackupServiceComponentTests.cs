using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Services.DataBase;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.Services.DataBase;

public class BackupServiceComponentTests
{
    [Fact]
    public void BackupFilePathBuilder_Build_ReturnsBackupFilePathWithTimestamp()
    {
        BackupFilePathBuilder builder = new();
        string backupFolder = Path.Combine(Path.GetTempPath(), "CostVisionBackupTests");
        DateTime timestamp = new(2026, 05, 30, 14, 15, 16);

        string backupFilePath = builder.Build(backupFolder, timestamp);

        Assert.Equal(backupFolder, Path.GetDirectoryName(backupFilePath));
        Assert.StartsWith("backup_", Path.GetFileName(backupFilePath));
        Assert.EndsWith("_2026.05.30_141516.bak", backupFilePath);
    }

    [Fact]
    public void CreateSqlServerBackup_FilePathBuilderFails_ThrowsFileSystemException()
    {
        IOException expectedException = new("Backup folder is not writable.");
        BackupService<ApplicationContext> service = new(
            "Server=localhost;Database=CostVision;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=1",
            Path.GetTempPath(),
            NullLoggerFactory.Instance,
            new ThrowingBackupFilePathBuilder(expectedException));

        IOException actualException = Assert.Throws<IOException>(service.CreateSqlServerBackup);

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public void CreateSqlServerBackup_InvalidConnectionString_ThrowsConnectionStringException()
    {
        BackupService<ApplicationContext> service = new(
            "not a connection string",
            Path.GetTempPath(),
            NullLoggerFactory.Instance,
            new FixedBackupFilePathBuilder(Path.Combine(Path.GetTempPath(), "backup_invalid_connection.bak")));

        Assert.ThrowsAny<Exception>(service.CreateSqlServerBackup);
    }

    private sealed class ThrowingBackupFilePathBuilder(IOException exception) : IBackupFilePathBuilder
    {
        public string Build(string backupFolder)
        {
            throw exception;
        }
    }

    private sealed class FixedBackupFilePathBuilder(string backupFilePath) : IBackupFilePathBuilder
    {
        public string Build(string backupFolder)
        {
            return backupFilePath;
        }
    }
}
