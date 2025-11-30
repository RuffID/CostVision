namespace CostVision.Services.Receipts
{
    public static class NameNormalizedHelper
    {
        public static string NormalizeProductName(string name)
        {
            return name
                .Trim()
                .ToUpperInvariant()
                .Replace("Ё", "Е")
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty)
                .Replace("-", string.Empty)
                .Replace("/", string.Empty);
        }
    }
}
