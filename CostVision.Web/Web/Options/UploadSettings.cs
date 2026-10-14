namespace CostVision.Web.Options;

public class UploadSettings
{
    public const string SECTION_NAME = "Uploads";

    public long MaxRequestBodyBytes { get; set; } = 1_000_000_000;
}
