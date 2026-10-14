using CostVision.Application.Abstractions.DataBase.Repositories;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CostVision.Infrastructure.DataBase.Repositories
{
    public class UnitOfWorkScope(IAppDbContext<ApplicationContext> context) : IUnitOfWorkScope
    {
        private bool _writeFailed;
        private bool _transactionActive;

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
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql &&
                ReceiptWriteErrors.IsCatalogUniqueViolation(sql.Number, sql.Message))
            {
                _writeFailed = true;
                throw new ReceiptWriteConflictException("Товар или магазин уже создан другим запросом. Повторите запрос в новом scope.", exception);
            }
            catch
            {
                _writeFailed = true;
                throw;
            }
        }

        public async Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken ct = default)
        {
            await ExecuteTransactionAsync(action, null, true, ct);
        }

        public async Task ExecuteInTransaction(Func<CancellationToken, Task> action, IsolationLevel isolationLevel, CancellationToken ct = default)
        {
            await ExecuteTransactionAsync(action, isolationLevel, false, ct);
        }

        private async Task ExecuteTransactionAsync(Func<CancellationToken, Task> action,
            IsolationLevel? isolationLevel, bool saveAfterAction, CancellationToken ct)
        {
            if (_writeFailed)
            {
                throw new InvalidOperationException("После отказа транзакции необходимо создать новый scope хранения.");
            }

            ArgumentNullException.ThrowIfNull(action);
            if (_transactionActive)
            {
                _writeFailed = true;
                throw new InvalidOperationException("В одном scope уже выполняется транзакция. Составной сценарий должен иметь одного владельца.");
            }

            _transactionActive = true;
            IDbContextTransaction? transaction = null;
            try
            {
                if (isolationLevel.HasValue)
                {
                    transaction = await context.Database.BeginTransactionAsync(isolationLevel.Value, ct);
                }
                else
                {
                    transaction = await context.Database.BeginTransactionAsync(ct);
                }

                await action(ct);
                if (saveAfterAction)
                {
                    await SaveChangesAsync(ct);
                }

                if (_writeFailed)
                {
                    throw new InvalidOperationException("После отказа транзакции необходимо создать новый scope хранения.");
                }

                await transaction.CommitAsync(ct);
            }
            catch (Exception exception)
            {
                _writeFailed = true;
                if (ReceiptWriteErrors.IsDeadlock(exception))
                {
                    // SQL Server уже откатил жертву deadlock; повторный rollback может скрыть исходный отказ.
                    throw new ReceiptWriteConflictException("Конкурентная запись чека была отменена. Повторите запрос.", exception);
                }

                if (transaction != null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                throw;
            }
            finally
            {
                try
                {
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
                catch
                {
                    _writeFailed = true;
                    throw;
                }
                finally
                {
                    _transactionActive = false;
                }
            }
        }
    }
}
