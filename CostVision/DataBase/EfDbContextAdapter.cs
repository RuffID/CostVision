using CostVision.Interfaces.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CostVision.DataBase
{
    public sealed class EfDbContextAdapter<TContext>(TContext inner) : IAppDbContext where TContext : DbContext
    {
        public DbSet<TEntity> Set<TEntity>() where TEntity : class => inner.Set<TEntity>();

        public Task<int> SaveChanges(CancellationToken ct = default) => inner.SaveChangesAsync(ct);

        public DatabaseFacade Database => inner.Database;

        public EntityEntry Entry(object entity) => inner.Entry(entity);
    }
}
