namespace CostVision.Infrastructure.Models.ConfigClass;

public class ExternalRequestOptions
{
    public const string SECTION_NAME = "ExternalRequests";

    public double TimeoutSeconds { get; set; } = 180;
}
