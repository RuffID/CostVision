namespace CostVision.Web.Options;

public class DataProtectionSettings
{
    public const string SECTION_NAME = "DataProtection";

    public string KeyDirectory { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "CostVision.Web";
}
