using System.Data;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Общая граница сохранения и транзакций одной scoped-сессии, без доступа к репозиториям.
/// </summary>
public interface IUnitOfWorkScope
{
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Выполняет action и автоматически сохраняет изменения перед commit.
    /// </summary>
    Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken ct = default);

    /// <summary>
    /// Выполняет action с заданной изоляцией; SaveChangesAsync явно вызывает сценарий.
    /// </summary>
    Task ExecuteInTransaction(Func<CancellationToken, Task> action, IsolationLevel isolationLevel, CancellationToken ct = default);
}
