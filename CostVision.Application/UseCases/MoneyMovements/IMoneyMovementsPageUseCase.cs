using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IMoneyMovementsPageUseCase
    {
        Task<ServiceResult<List<UserAccountViewModel>>> GetAccountsAsync(Guid userId, CancellationToken ct);

        Task<ServiceResult<List<BankStatementImportBankDto>>> GetImportBanksAsync(CancellationToken ct);

        Task<ServiceResult<List<MoneyMovementDto>>> GetListAsync(Guid userId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct);

        Task<ServiceResult<List<MoneyMovementReceiptDto>>> GetLinkedReceiptsAsync(Guid moneyMovementId, Guid userId, CancellationToken ct);

        Task<ServiceResult<List<MoneyMovementReceiptDto>>> GetReceiptCandidatesAsync(GetMoneyMovementReceiptCandidatesRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult<MoneyMovementDto>> CreateAsync(CreateMoneyMovementRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult> MoveToAccountAsync(MoveMoneyMovementToAccountRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult> DeleteAsync(DeleteMoneyMovementRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult> UpdateCommentAsync(UpdateMoneyMovementCommentRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult> LinkReceiptAsync(LinkMoneyMovementReceiptRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult> UnlinkReceiptAsync(UnlinkMoneyMovementReceiptRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult<ReceiptDto>> OpenReceiptAsync(OpenReceiptRequest request, User currentUser, CancellationToken ct);

        Task<ServiceResult<BankStatementImportPreviewDto>> PreviewImportAsync(PreviewBankStatementImportPageRequest request, Guid userId, CancellationToken ct);

        Task<ServiceResult<BankStatementImportResultDto>> ImportAsync(SaveBankStatementImportRequest request, Guid userId, CancellationToken ct);
    }
}
