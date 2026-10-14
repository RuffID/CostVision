using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class SaveManualReceiptUseCase(
        IReceiptCreationUnitOfWork unitOfWork,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase) : ISaveManualReceiptUseCase
    {
        public async Task<ServiceResult<AddReceiptManualResponse>> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct)
        {
            ServiceResult shape = RequestShapeValidation.Validate(request);
            if (!shape.Success)
            {
                return shape.PropagateFailure<AddReceiptManualResponse>();
            }

            if (request.AccountId != Guid.Empty)
            {
                ServiceResult accessResult = await validateReceiptCreationAccessUseCase.ExecuteAsync(request.AccountId, currentUserId, ct);
                if (!accessResult.Success)
                {
                    return accessResult.PropagateFailure<AddReceiptManualResponse>();
                }
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

            ServiceResult<AddReceiptManualResponse>? result = null;
            List<ReceiptAccount> addedLinks = [];
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    result = await SaveReceiptAsync(createdReceipt!, request.AccountId, currentUserId, addedLinks, transactionCt);
                }, IsolationLevel.Serializable, ct);
            }
            catch (ReceiptWriteConflictException conflict)
            {
                UndoAddedLinks(addedLinks);
                return ServiceResult<AddReceiptManualResponse>.Fail(ServiceErrorType.Conflict, conflict.Message);
            }
            catch
            {
                UndoAddedLinks(addedLinks);
                throw;
            }

            return result ?? throw new InvalidOperationException("Транзакция ручного чека завершилась без результата.");
        }

        private async Task<ServiceResult<AddReceiptManualResponse>> SaveReceiptAsync(
            Receipt receipt, Guid accountId, Guid currentUserId, List<ReceiptAccount> addedLinks, CancellationToken ct)
        {
            ReceiptIdentity identity = receipt.GetIdentity();
            if (accountId != Guid.Empty)
            {
                Receipt? existingReceiptInAccount = await unitOfWork.Receipt.GetItemByPredicateAsync(
                    identity.InAccount(accountId), asNoTracking: true, ct: ct);
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
            }

            Receipt? existingReceipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                identity.ForAuthorCopy(currentUserId), asNoTracking: false,
                include: query => query.Include(item => item.Accounts), ct: ct);
            if (existingReceipt != null)
            {
                if (accountId != Guid.Empty)
                {
                    if (!existingReceipt.TryAddAccount(accountId, out ReceiptAccount? link, out string? linkError))
                    {
                        return ServiceResult<AddReceiptManualResponse>.Fail(
                            ServiceErrorType.Validation, linkError ?? "Не удалось добавить чек на выбранный счёт.");
                    }

                    addedLinks.Add(link!);
                    unitOfWork.ReceiptAccount.Create(link!);
                    await unitOfWork.SaveChangesAsync(ct);
                    Receipt linkedReceipt = await LoadReceiptWithAccountsAsync(existingReceipt.Id, ct);

                    return ServiceResult<AddReceiptManualResponse>.Ok(new AddReceiptManualResponse
                    {
                        Outcome = ManualReceiptOutcome.AddedToAccount,
                        Message = "Чек уже существовал и был добавлен на выбранный счёт.",
                        Receipt = linkedReceipt.MapReceiptDto(currentUserId)
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

            if (accountId != Guid.Empty)
            {
                if (!receipt.TryAddAccount(accountId, out ReceiptAccount? link, out string? linkError))
                {
                    return ServiceResult<AddReceiptManualResponse>.Fail(
                        ServiceErrorType.Validation, linkError ?? "Не удалось добавить чек на выбранный счёт.");
                }

                addedLinks.Add(link!);
            }

            unitOfWork.Receipt.Create(receipt);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<AddReceiptManualResponse>.Ok(new AddReceiptManualResponse
            {
                Outcome = ManualReceiptOutcome.Created,
                Receipt = receipt.MapReceiptDto(currentUserId)
            });
        }

        private static void UndoAddedLinks(List<ReceiptAccount> links)
        {
            foreach (ReceiptAccount link in links)
            {
                if (!link.Receipt!.TryRemoveAccount(link.AccountId, out _, out string? error))
                {
                    throw new InvalidOperationException(error);
                }
            }
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
