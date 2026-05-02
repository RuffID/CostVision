namespace CostVision.Infrastructure.Services.Helpers
{
    public static class NameNormalizedHelper
    {
        public static string GetNormalizedName(string name)
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