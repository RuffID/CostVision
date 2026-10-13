using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class SaveReceiptsScannedUseCase(
        IUnitOfWork unitOfWork,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase,
        IQrParser qrParser) : ISaveReceiptsScannedUseCase
    {
        public async Task<ServiceResult<AddReceiptScanResponse>> ExecuteAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.Results.Count == 0)
            {
                return ServiceResult<AddReceiptScanResponse>.Fail(ServiceErrorType.Validation, "Нет данных для обработки.");
            }

            if (request.AccountId != Guid.Empty)
            {
                ServiceResult accessResult = await validateReceiptCreationAccessUseCase.ExecuteAsync(request.AccountId, currentUserId, ct);
                if (!accessResult.Success)
                {
                    return accessResult.PropagateFailure<AddReceiptScanResponse>();
                }
            }

            AddReceiptScanResponse? response = null;
            List<ReceiptAccount> addedLinks = [];
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    response = await ProcessBatchAsync(request, currentUserId, addedLinks, transactionCt);
                }, IsolationLevel.Serializable, ct);
            }
            catch (ReceiptWriteConflictException conflict)
            {
                UndoAddedLinks(addedLinks);
                return ServiceResult<AddReceiptScanResponse>.Fail(ServiceErrorType.Conflict, conflict.Message);
            }
            catch
            {
                UndoAddedLinks(addedLinks);
                throw;
            }

            return ServiceResult<AddReceiptScanResponse>.Ok(response
                ?? throw new InvalidOperationException("Транзакция обработки QR завершилась без результата."));
        }

        private async Task<AddReceiptScanResponse> ProcessBatchAsync(
            QrScanRequest request, Guid currentUserId, List<ReceiptAccount> addedLinks, CancellationToken ct)
        {
            int scannedCount = request.Results.Count(row => !string.IsNullOrWhiteSpace(row.DecodedText));
            int addedCount = 0;
            Dictionary<(ReceiptIdentity Identity, Guid AuthorId), Receipt> authorCopies = [];
            HashSet<(ReceiptIdentity Identity, Guid AccountId)> accountDocuments = [];

            foreach (QrScanResult result in request.Results)
            {
                if (string.IsNullOrWhiteSpace(result.DecodedText))
                {
                    continue;
                }

                result.ErrorMessage = null;
                result.Parsed = qrParser.Parse(result.DecodedText);
                if (result.Parsed == null)
                {
                    result.ErrorMessage = "Не удалось разобрать данные QR-кода.";
                    continue;
                }

                if (result.Parsed.Sum == null || result.Parsed.OperationType == null)
                {
                    result.ErrorMessage = "В данных QR-кода отсутствуют обязательные поля фискального чека.";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(result.FileName))
                {
                    result.FileName = "Browser file";
                }

                if (!Receipt.TryCreate(
                        result.Parsed.FiscalDriveNumber,
                        result.Parsed.FiscalDocumentNumber,
                        result.Parsed.FiscalSign,
                        result.Parsed.DateTime,
                        (ReceiptOperationType)result.Parsed.OperationType.Value,
                        result.Parsed.Sum.Value,
                        currentUserId,
                        DateTime.UtcNow,
                        out Receipt? createdReceipt,
                        out string? creationError))
                {
                    result.ErrorMessage = creationError;
                    continue;
                }

                Receipt receipt = createdReceipt!;
                ReceiptIdentity identity = receipt.GetIdentity();
                if (request.AccountId != Guid.Empty)
                {
                    bool existsInAccount = accountDocuments.Contains((identity, request.AccountId));
                    if (!existsInAccount)
                    {
                        Receipt? accountReceipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                            identity.InAccount(request.AccountId), asNoTracking: true, ct: ct);
                        existsInAccount = accountReceipt != null;
                    }

                    if (existsInAccount)
                    {
                        accountDocuments.Add((identity, request.AccountId));
                        result.ErrorMessage = "Такой чек уже есть на выбранном счёте.";
                        continue;
                    }
                }

                bool cached = authorCopies.TryGetValue((identity, currentUserId), out Receipt? existingReceipt);
                if (!cached)
                {
                    existingReceipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                        identity.ForAuthorCopy(currentUserId), asNoTracking: false,
                        include: query => query.Include(item => item.Accounts), ct: ct);
                }

                if (existingReceipt != null)
                {
                    authorCopies[(identity, currentUserId)] = existingReceipt;
                    if (request.AccountId == Guid.Empty)
                    {
                        result.ErrorMessage = "Такой чек уже существует у автора.";
                        continue;
                    }

                    if (!existingReceipt.TryAddAccount(request.AccountId, out ReceiptAccount? link, out string? linkError))
                    {
                        result.ErrorMessage = linkError;
                        continue;
                    }

                    addedLinks.Add(link!);
                    unitOfWork.ReceiptAccount.Create(link!);
                    accountDocuments.Add((identity, request.AccountId));
                    addedCount++;
                    continue;
                }

                if (request.AccountId != Guid.Empty)
                {
                    if (!receipt.TryAddAccount(request.AccountId, out ReceiptAccount? link, out string? linkError))
                    {
                        result.ErrorMessage = linkError;
                        continue;
                    }

                    addedLinks.Add(link!);
                    accountDocuments.Add((identity, request.AccountId));
                }

                unitOfWork.Receipt.Create(receipt);
                authorCopies.Add((identity, currentUserId), receipt);
                addedCount++;
            }

            if (addedCount > 0)
            {
                await unitOfWork.SaveChangesAsync(ct);
            }

            return new AddReceiptScanResponse
            {
                ScannedCount = scannedCount,
                AddedToDbCount = addedCount,
                ErrorCount = request.Results.Count(row => !string.IsNullOrWhiteSpace(row.ErrorMessage)),
                Results = request.Results
            };
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
    }
}
