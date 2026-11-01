namespace CostVision.Infrastructure.Models.ConfigClass;

/// <summary>Эксплуатационные предпосылки startup; каталог находится на SQL Server.</summary>
public class DatabaseMaintenanceSettings
{
    public const string SECTION_NAME = "DatabaseMaintenance";

    public DatabaseStartupOperation Operation { get; set; } = DatabaseStartupOperation.UpdateExisting;
    public int TimeoutSeconds { get; set; } = 60;
    public string ServerBackupDirectory { get; set; } = string.Empty;

    /// <summary>Программное подтверждение режима SingleInitializer; из конфигурации не изменяется.</summary>
    public bool SingleInitializerConfirmed { get; } = true;
}
