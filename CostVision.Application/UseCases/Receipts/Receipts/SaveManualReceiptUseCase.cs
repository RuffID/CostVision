using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class SaveManualReceiptUseCase(
        IUnitOfWork unitOfWork,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase) : ISaveManualReceiptUseCase
    {
        public async Task<ServiceResult<AddReceiptManualResponse>> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.AccountId != Guid.Empty)
            {
                ServiceResult accessResult = await validateReceiptCreationAccessUseCase.ExecuteAsync(request.AccountId, currentUserId, ct);
                if (!accessResult.Success)
                    return accessResult.PropagateFailure<AddReceiptManualResponse>();
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
                return ServiceResult<AddReceiptManualResponse>.Fail(
                    ServiceErrorType.Validation,
                    creationError ?? "Некорректные данные чека.");
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
                Receipt loadedReceipt = await LoadReceiptWithAccountsAsync(existingReceiptInAccount.Id, ct);
                return ServiceResult<AddReceiptManualResponse>.Ok(new AddReceiptManualResponse
                {
                    Outcome = ManualReceiptOutcome.AlreadyExistsInAccount,
                    Message = "Такой чек уже есть на выбранном счёте.",
                    Receipt = loadedReceipt.MapReceiptDto(currentUserId)
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
                    if (!existingReceipt.TryAddAccount(request.AccountId, out ReceiptAccount? link, out string? existingLinkError))
                    {
                        return ServiceResult<AddReceiptManualResponse>.Fail(
                            ServiceErrorType.Validation,
                            existingLinkError ?? "Не удалось добавить чек на выбранный счёт.");
                    }

                    Receipt? linkedReceipt = null;
                    await unitOfWork.ExecuteInTransaction(async transactionCt =>
                    {
                        unitOfWork.ReceiptAccount.Create(link!);
                        await unitOfWork.SaveChangesAsync(transactionCt);
                        linkedReceipt = await LoadReceiptWithAccountsAsync(existingReceipt.Id, transactionCt);
                    }, ct);

                    return ServiceResult<AddReceiptManualResponse>.Ok(new AddReceiptManualResponse
                    {
                        Outcome = ManualReceiptOutcome.AddedToAccount,
                        Message = "Чек уже существовал и был добавлен на выбранный счёт.",
                        Receipt = (linkedReceipt
                            ?? throw new InvalidOperationException("Транзакция добавления чека на счёт завершилась без результата."))
                            .MapReceiptDto(currentUserId)
                    });
                }

                Receipt loadedReceipt = await LoadReceiptWithAccountsAsync(existingReceipt.Id, ct);
                return ServiceResult<AddReceiptManualResponse>.Ok(new AddReceiptManualResponse
                {
                    Outcome = ManualReceiptOutcome.AlreadyExists,
                    Message = "Такой чек уже существует в базе.",
                    Receipt = loadedReceipt.MapReceiptDto(currentUserId)
                });
            }

            if (request.AccountId != Guid.Empty &&
                !receipt.TryAddAccount(request.AccountId, out _, out string? linkError))
            {
                return ServiceResult<AddReceiptManualResponse>.Fail(
                    ServiceErrorType.Validation,
                    linkError ?? "Не удалось добавить чек на выбранный счёт.");
            }

            unitOfWork.Receipt.Create(receipt);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<AddReceiptManualResponse>.Ok(new AddReceiptManualResponse
            {
                Outcome = ManualReceiptOutcome.Created,
                Receipt = receipt.MapReceiptDto(currentUserId)
            });
        }

        private Task<Receipt?> FindExistingReceiptAsync(string fiscalDocumentNumber, string fiscalDriveNumber, string fiscalSign, decimal totalSum, DateTime dateTime, ReceiptOperationType operationType, Guid currentUserId, CancellationToken ct)
        {
            return unitOfWork.Receipt.GetItemByPredicateAsync(
                receipt => receipt.FiscalDocumentNumber == fiscalDocumentNumber &&
                           receipt.FiscalDriveNumber == fiscalDriveNumber &&
                           receipt.FiscalSign == fiscalSign &&
                           receipt.TotalSum == totalSum &&
                           receipt.DateTime == dateTime &&
                           receipt.CreatedByUserId == currentUserId &&
                           receipt.OperationType == operationType,
                asNoTracking: false,
                ct: ct);
        }

        private Task<Receipt?> FindExistingReceiptInAccountAsync(string fiscalDocumentNumber, string fiscalDriveNumber, string fiscalSign, decimal totalSum, DateTime dateTime, ReceiptOperationType operationType, Guid accountId, CancellationToken ct)
        {
            return unitOfWork.Receipt.GetItemByPredicateAsync(
                receipt => receipt.FiscalDocumentNumber == fiscalDocumentNumber &&
                           receipt.FiscalDriveNumber == fiscalDriveNumber &&
                           receipt.FiscalSign == fiscalSign &&
                           receipt.TotalSum == totalSum &&
                           receipt.DateTime == dateTime &&
                           receipt.OperationType == operationType &&
                           receipt.Accounts.Any(link => link.AccountId == accountId),
                asNoTracking: true,
                ct: ct);
        }

        private async Task<Receipt> LoadReceiptWithAccountsAsync(Guid receiptId, CancellationToken ct)
        {
            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                item => item.Id == receiptId,
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Accounts)
                        .ThenInclude(link => link.Account)
                    .AsSplitQuery(),
                ct: ct);

            return receipt ?? throw new InvalidOperationException("Не удалось загрузить чек после сохранения.");
        }
    }
}
