namespace CostVision.Infrastructure.Services.DataBase
{
    public interface IBackupFilePathBuilder
    {
        string Build(string backupFolder);
    }
}
