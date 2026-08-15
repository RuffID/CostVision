namespace CostVision.Web.Options;

public class ForwardedHeadersSettings
{
    public const string SECTION_NAME = "ForwardedHeaders";

    public List<string> KnownProxies { get; set; } = [];
}
