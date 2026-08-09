using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class MoneyMovementsPageUseCase(
        IGetMoneyMovementAccountsUseCase getMoneyMovementAccountsUseCase,
        IGetMoneyMovementListUseCase getMoneyMovementListUseCase,
        ICreateMoneyMovementUseCase createMoneyMovementUseCase,
        IMoveMoneyMovementToAccountUseCase moveMoneyMovementToAccountUseCase,
        IDeleteMoneyMovementUseCase deleteMoneyMovementUseCase,
        IUpdateMoneyMovementCommentUseCase updateMoneyMovementCommentUseCase,
        IGetBankStatementImportBanksUseCase getBankStatementImportBanksUseCase,
        IPreviewBankStatementImportUseCase previewBankStatementImportUseCase,
        IImportMoneyMovementsUseCase importMoneyMovementsUseCase,
        IGetLinkedMoneyMovementReceiptsUseCase getLinkedMoneyMovementReceiptsUseCase,
        IGetMoneyMovementReceiptCandidatesUseCase getMoneyMovementReceiptCandidatesUseCase,
        ILinkMoneyMovementReceiptUseCase linkMoneyMovementReceiptUseCase,
        IUnlinkMoneyMovementReceiptUseCase unlinkMoneyMovementReceiptUseCase,
        IGetReceiptWithItemsUseCase getReceiptWithItemsUseCase) : IMoneyMovementsPageUseCase
    {
        public async Task<ServiceResult<List<UserAccountViewModel>>> GetAccountsAsync(Guid userId, CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getMoneyMovementAccountsUseCase.ExecuteAsync(userId, ct);
            return ServiceResult<List<UserAccountViewModel>>.Ok(accounts);
        }

        public async Task<ServiceResult<List<BankStatementImportBankDto>>> GetImportBanksAsync(CancellationToken ct)
        {
            List<BankStatementImportBankDto> banks = await getBankStatementImportBanksUseCase.ExecuteAsync(ct);
            return ServiceResult<List<BankStatementImportBankDto>>.Ok(banks);
        }

        public Task<ServiceResult<List<MoneyMovementDto>>> GetListAsync(Guid userId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct)
        {
            Guid? normalizedAccountId = accountId == Guid.Empty ? null : accountId;
            return getMoneyMovementListUseCase.ExecuteAsync(userId, dateFrom, dateTo, normalizedAccountId, ct);
        }

        public Task<ServiceResult<List<MoneyMovementReceiptDto>>> GetLinkedReceiptsAsync(Guid moneyMovementId, Guid userId, CancellationToken ct)
        {
            return getLinkedMoneyMovementReceiptsUseCase.ExecuteAsync(moneyMovementId, userId, ct);
        }

        public Task<ServiceResult<List<MoneyMovementReceiptDto>>> GetReceiptCandidatesAsync(GetMoneyMovementReceiptCandidatesRequest request, Guid userId, CancellationToken ct)
        {
            return getMoneyMovementReceiptCandidatesUseCase.ExecuteAsync(request, userId, ct);
        }

        public Task<ServiceResult<MoneyMovementDto>> CreateAsync(CreateMoneyMovementRequest request, Guid userId, CancellationToken ct)
        {
            return createMoneyMovementUseCase.ExecuteAsync(request, userId, ct);
        }

        public Task<ServiceResult> MoveToAccountAsync(MoveMoneyMovementToAccountRequest request, Guid userId, CancellationToken ct)
        {
            return moveMoneyMovementToAccountUseCase.ExecuteAsync(request, userId, ct);
        }

        public Task<ServiceResult> DeleteAsync(DeleteMoneyMovementRequest request, Guid userId, CancellationToken ct)
        {
            return deleteMoneyMovementUseCase.ExecuteAsync(request, userId, ct);
        }

        public Task<ServiceResult> UpdateCommentAsync(UpdateMoneyMovementCommentRequest request, Guid userId, CancellationToken ct)
        {
            return updateMoneyMovementCommentUseCase.ExecuteAsync(request, userId, ct);
        }

        public Task<ServiceResult> LinkReceiptAsync(LinkMoneyMovementReceiptRequest request, Guid userId, CancellationToken ct)
        {
            return linkMoneyMovementReceiptUseCase.ExecuteAsync(request, userId, ct);
        }

        public Task<ServiceResult> UnlinkReceiptAsync(UnlinkMoneyMovementReceiptRequest request, Guid userId, CancellationToken ct)
        {
            return unlinkMoneyMovementReceiptUseCase.ExecuteAsync(request, userId, ct);
        }

        public async Task<ServiceResult<ReceiptDto>> OpenReceiptAsync(OpenReceiptRequest request, User currentUser, CancellationToken ct)
        {
            return await getReceiptWithItemsUseCase.ExecuteAsync(request.ReceiptId, currentUser, ct);
        }

        public Task<ServiceResult<BankStatementImportPreviewDto>> PreviewImportAsync(PreviewBankStatementImportPageRequest request, Guid userId, CancellationToken ct)
        {
            if (request.FileStream == null || request.FileLength == 0)
                return Task.FromResult(ServiceResult<BankStatementImportPreviewDto>.Fail(ServiceErrorType.Validation, "Выберите файл выписки."));

            return previewBankStatementImportUseCase.ExecuteAsync(request.BankId, request.AccountId, request.FileName, request.FileStream, userId, ct);
        }

        public Task<ServiceResult<BankStatementImportResultDto>> ImportAsync(SaveBankStatementImportRequest request, Guid userId, CancellationToken ct)
        {
            return importMoneyMovementsUseCase.ExecuteAsync(request, userId, ct);
        }
    }
}
