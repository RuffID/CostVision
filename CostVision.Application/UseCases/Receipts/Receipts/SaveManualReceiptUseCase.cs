using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class SaveManualReceiptUseCase(
        IUnitOfWork unitOfWork,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase,
        IReceiptRefreshWorkflow receiptRefreshWorkflow) : ISaveManualReceiptUseCase
    {
        public async Task<ServiceResult<ManualReceiptResult>> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.AccountId != Guid.Empty)
            {
                ServiceResult<bool> accessResult = await validateReceiptCreationAccessUseCase.ExecuteAsync(request.AccountId, currentUserId, ct);
                if (!accessResult.Success)
                    return ServiceResult<ManualReceiptResult>.Fail(accessResult.Error!.StatusCode, accessResult.Error.Message);
            }

            if (!Receipt.TryCreate(
                    request.Receipt.FiscalDriveNumber,
                    request.Receipt.FiscalDocumentNumber,
                    request.Receipt.FiscalSign,
                    request.Receipt.DateTime,
                    request.Receipt.OperationType,
                    request.Receipt.Sum,
                    currentUserId,
                    DateTime.UtcNow,
                    out Receipt? createdReceipt,
                    out string? creationError))
            {
                return ServiceResult<ManualReceiptResult>.Ok(new ManualReceiptResult
                {
                    IsCreated = false,
                    ErrorMessage = creationError
                });
            }

            Receipt receipt = createdReceipt!;
            Receipt? existingReceiptInAccount = request.AccountId == Guid.Empty
                ? null
                : await FindExistingReceiptInAccountAsync(
                    receipt.FiscalDocumentNumber,
                    receipt.FiscalDriveNumber,
                    receipt.FiscalSign,
                    receipt.TotalSum,
                    receipt.DateTime,
                    receipt.OperationType,
                    request.AccountId,
                    ct);

            if (existingReceiptInAccount != null)
            {
                return ServiceResult<ManualReceiptResult>.Ok(new ManualReceiptResult
                {
                    IsCreated = false,
                    ErrorMessage = "Такой чек уже есть на выбранном счёте.",
                    Receipt = await LoadReceiptWithAccountsAsync(existingReceiptInAccount.Id, ct)
                });
            }

            Receipt? existingReceipt = await FindExistingReceiptAsync(
                receipt.FiscalDocumentNumber,
                receipt.FiscalDriveNumber,
                receipt.FiscalSign,
                receipt.TotalSum,
                receipt.DateTime,
                receipt.OperationType,
                currentUserId,
                ct);

            if (existingReceipt != null)
            {
                if (request.AccountId != Guid.Empty)
                {
                    if (!existingReceipt.TryAddAccount(request.AccountId, out ReceiptAccount? link, out string? linkError))
                    {
                        return ServiceResult<ManualReceiptResult>.Ok(new ManualReceiptResult
                        {
                            IsCreated = false,
                            ErrorMessage = linkError,
                            Receipt = existingReceipt
                        });
                    }

                    unitOfWork.ReceiptAccount.Create(link!);
                    await unitOfWork.SaveChangesAsync(ct);

                    Receipt linkedReceipt = await LoadReceiptWithAccountsAsync(existingReceipt.Id, ct);

                    return ServiceResult<ManualReceiptResult>.Ok(new ManualReceiptResult
                    {
                        IsCreated = true,
                        ErrorMessage = "Чек уже существовал и был добавлен на выбранный счёт.",
                        Receipt = linkedReceipt
                    });
                }

                return ServiceResult<ManualReceiptResult>.Ok(new ManualReceiptResult
                {
                    IsCreated = false,
                    ErrorMessage = "Такой чек уже существует в базе.",
                    Receipt = await LoadReceiptWithAccountsAsync(existingReceipt.Id, ct)
                });
            }

            if (request.AccountId != Guid.Empty)
            {
                if (!receipt.TryAddAccount(request.AccountId, out _, out string? linkError))
                {
                    return ServiceResult<ManualReceiptResult>.Ok(new ManualReceiptResult
                    {
                        IsCreated = false,
                        ErrorMessage = linkError
                    });
                }
            }

            unitOfWork.Receipt.Create(receipt);
            await unitOfWork.SaveChangesAsync(ct);
            Receipt refreshedReceipt = await receiptRefreshWorkflow.TryRefreshCreatedReceiptAsync(receipt, ct);

            return ServiceResult<ManualReceiptResult>.Ok(new ManualReceiptResult
            {
                IsCreated = true,
                Receipt = refreshedReceipt
            });
        }

        private Task<Receipt?> FindExistingReceiptAsync(string fiscalDocumentNumber, string fiscalDriveNumber, string fiscalSign, decimal totalSum, DateTime dateTime, ReceiptOperationType operationType, Guid currentUserId, CancellationToken ct)
        {
            return unitOfWork.Receipt.GetItemByPredicateAsync(
                r => r.FiscalDocumentNumber == fiscalDocumentNumber &&
                     r.FiscalDriveNumber == fiscalDriveNumber &&
                     r.FiscalSign == fiscalSign &&
                     r.TotalSum == totalSum &&
                     r.DateTime == dateTime &&
                     r.CreatedByUserId == currentUserId &&
                     r.OperationType == operationType,
                asNoTracking: false,
                ct: ct);
        }

        private Task<Receipt?> FindExistingReceiptInAccountAsync(string fiscalDocumentNumber, string fiscalDriveNumber, string fiscalSign, decimal totalSum, DateTime dateTime, ReceiptOperationType operationType, Guid accountId, CancellationToken ct)
        {
            return unitOfWork.Receipt.GetItemByPredicateAsync(
                r => r.FiscalDocumentNumber == fiscalDocumentNumber &&
                     r.FiscalDriveNumber == fiscalDriveNumber &&
                     r.FiscalSign == fiscalSign &&
                     r.TotalSum == totalSum &&
                     r.DateTime == dateTime &&
                     r.OperationType == operationType &&
                     r.Accounts.Any(link => link.AccountId == accountId),
                asNoTracking: true,
                ct: ct);
        }

        private async Task<Receipt> LoadReceiptWithAccountsAsync(Guid receiptId, CancellationToken ct)
        {
            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                receipt => receipt.Id == receiptId,
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                    .AsSplitQuery(),
                ct: ct);

            return receipt ?? throw new InvalidOperationException("Не удалось загрузить чек после сохранения.");
        }
    }
}
