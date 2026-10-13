using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CostVision.Infrastructure.DataBase.Repositories
{
    public class UnitOfWork(IAppDbContext<ApplicationContext> context,
        IUserRepository user,
        IRoleRepository role,
        IAccountRepository account,
        IReceiptRepository receipt,
        IReceiptItemRepository receiptItem,
        IStoreRepository store,
        IProductRepository product,
        IReceiptAccountRepository receiptAccount,
        IAccountMemberRepository accountMember,
        IMoneyMovementRepository moneyMovement,
        IMoneyMovementReceiptRepository moneyMovementReceipt) : IUnitOfWork
    {
        private bool _writeFailed;

        public IUserRepository User { get; } = user;
        public IRoleRepository Role { get; } = role;
        public IAccountRepository Account { get; } = account;
        public IReceiptRepository Receipt { get; } = receipt;
        public IReceiptItemRepository ReceiptItem { get; } = receiptItem;
        public IStoreRepository Store { get; } = store;
        public IProductRepository Product { get; } = product;
        public IReceiptAccountRepository ReceiptAccount { get; } = receiptAccount;
        public IAccountMemberRepository AccountMember { get; } = accountMember;
        public IMoneyMovementRepository MoneyMovement { get; } = moneyMovement;
        public IMoneyMovementReceiptRepository MoneyMovementReceipt { get; } = moneyMovementReceipt;

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            if (_writeFailed)
            {
                throw new InvalidOperationException("После отказа транзакции необходимо создать новый scope хранения.");
            }

            try
            {
                await context.SaveChanges(ct);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql &&
                ReceiptWriteErrors.IsKnownUniqueViolation(sql.Number, sql.Message))
            {
                _writeFailed = true;
                throw new ReceiptWriteConflictException("Такой чек или его связь уже сохранены. Обновите данные и повторите запрос.", exception);
            }
        }

        public async Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken ct = default)
        {
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

            try
            {
                await action(ct);
                await SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        public async Task ExecuteInTransaction(Func<CancellationToken, Task> action, IsolationLevel isolationLevel, CancellationToken ct = default)
        {
            if (_writeFailed)
            {
                throw new InvalidOperationException("После отказа транзакции необходимо создать новый scope хранения.");
            }

            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(isolationLevel, ct);

            try
            {
                await action(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception exception)
            {
                _writeFailed = true;
                if (ReceiptWriteErrors.IsDeadlock(exception))
                {
                    // SQL Server уже откатил транзакцию жертвы deadlock; повторный rollback может скрыть исходный отказ.
                    throw new ReceiptWriteConflictException("Конкурентная запись чека была отменена. Повторите запрос.", exception);
                }

                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }
}
