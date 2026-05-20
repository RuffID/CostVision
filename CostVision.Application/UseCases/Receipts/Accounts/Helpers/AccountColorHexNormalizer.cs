using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Responses.Results;
using System.Text.RegularExpressions;

namespace CostVision.Application.UseCases.Receipts.Accounts.Helpers
{
    internal static class AccountColorHexNormalizer
    {
        private static readonly Regex COLOR_HEX_REGEX = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        public static ServiceResult<string> Normalize(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                return ServiceResult<string>.Ok(Account.DEFAULT_COLOR_HEX);

            string normalizedColorHex = colorHex.Trim().ToUpperInvariant();
            if (!COLOR_HEX_REGEX.IsMatch(normalizedColorHex))
                return ServiceResult<string>.Fail(400, "Некорректный цвет счёта.");

            return ServiceResult<string>.Ok(normalizedColorHex);
        }
    }
}
