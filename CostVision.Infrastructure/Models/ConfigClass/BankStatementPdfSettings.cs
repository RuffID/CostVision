namespace CostVision.Infrastructure.Models.ConfigClass;

public class BankStatementPdfSettings
{
    public const string SECTION_NAME = "BankStatementPdf";

    public int MaxBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxPages { get; set; } = 200;
}
