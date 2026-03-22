using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CostVision.Abstractions.DataBase
{
    public interface IAppDbContext
    {
        DbSet<TEntity> Set<TEntity>() where TEntity : class;

        Task<int> SaveChanges(CancellationToken ct = default);

        DatabaseFacade Database { get; }

        EntityEntry Entry(object entity);
    }
}
