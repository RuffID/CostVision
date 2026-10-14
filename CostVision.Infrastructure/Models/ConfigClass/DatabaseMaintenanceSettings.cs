namespace CostVision.Infrastructure.Models.ConfigClass;

/// <summary>Эксплуатационные предпосылки startup; каталог находится на SQL Server.</summary>
public class DatabaseMaintenanceSettings
{
    public const string SECTION_NAME = "DatabaseMaintenance";

    public DatabaseStartupOperation Operation { get; set; } = DatabaseStartupOperation.UpdateExisting;
    public int TimeoutSeconds { get; set; } = 60;
    public string ServerBackupDirectory { get; set; } = string.Empty;

    /// <summary>Подтверждение оператором остановки других экземпляров, writes и DDL на всё обслуживание.</summary>
    public bool SingleInitializerConfirmed { get; set; }
}
