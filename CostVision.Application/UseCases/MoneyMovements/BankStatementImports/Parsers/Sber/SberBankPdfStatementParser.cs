using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;

namespace CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsers.Sber
{
    internal class SberBankPdfStatementParser : IBankStatementParser
    {
        public string BankId => BankStatementImportConstants.SBER_BANK_PDF_BANK_ID;

        public string BankName => "СберБанк";

        public string Description => "PDF-выписка";

        public bool IsConfigured => false;

        public bool CanParseFile(string fileName)
        {
            return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public BankStatementImportPreviewDto Parse(Guid accountId, IReadOnlyList<string> pages)
        {
            return new BankStatementImportPreviewDto
            {
                BankId = BankId,
                AccountId = accountId
            };
        }
    }
}
