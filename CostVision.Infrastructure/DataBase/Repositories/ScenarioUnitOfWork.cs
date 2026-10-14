using System.Data;
using CostVision.Application.Abstractions.DataBase.Repositories;

namespace CostVision.Infrastructure.DataBase.Repositories;

/// <summary>
/// Делегирует технические операции общей scoped-сессии; не хранит контекст или флаг отказа.
/// </summary>
public abstract class ScenarioUnitOfWork(IUnitOfWorkScope scope) : IUnitOfWorkScope
{
    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return scope.SaveChangesAsync(ct);
    }

    public Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        return scope.ExecuteInTransaction(action, ct);
    }

    public Task ExecuteInTransaction(Func<CancellationToken, Task> action, IsolationLevel isolationLevel, CancellationToken ct = default)
    {
        return scope.ExecuteInTransaction(action, isolationLevel, ct);
    }
}
