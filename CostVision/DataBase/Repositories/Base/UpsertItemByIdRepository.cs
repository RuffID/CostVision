using CostVision.Interfaces.DataBase;
using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.Entity;
using Microsoft.EntityFrameworkCore;

namespace CostVision.DataBase.Repositories.Base
{
    public class UpsertItemByIdRepository<TEntity, TId>(IAppDbContext _context) : 
        IUpsertItemByIdRepository<TEntity, TId> where TEntity : class, 
        IEntity<TId>, ICopyable<TEntity> where TId : notnull, 
        IEquatable<TId>
    {
        public async Task Upsert(TEntity item, CancellationToken ct = default)
        {
            TEntity? existing = await _context.Set<TEntity>().FirstOrDefaultAsync(e => e.Id.Equals(item.Id), ct);

            if (existing is null)
                _context.Set<TEntity>().Add(item);
            else
                existing.CopyData(item);
        }

        public async Task Upsert(IEnumerable<TEntity> items, CancellationToken ct = default)
        {
            List<TEntity> incoming = items.ToList();

            List<TId> keys = incoming
                .Select(i => i.Id)
                .Where(id => !IsDefault(id))
                .Distinct()
                .ToList();

            List<TEntity> existingList = keys.Count == 0
                ? new List<TEntity>()
                : await _context.Set<TEntity>()
                    .Where(e => keys.Contains(e.Id))
                    .ToListAsync(ct);

            Dictionary<TId, TEntity> existingById = existingList.ToDictionary(e => e.Id);

            foreach (TEntity item in incoming)
            {
                if (!IsDefault(item.Id) && existingById.TryGetValue(item.Id, out TEntity? existing))
                    existing.CopyData(item);
                else
                    _context.Set<TEntity>().Add(item);
            }
        }

        private static bool IsDefault(TId id)
        {
            return EqualityComparer<TId>.Default.Equals(id, default!);
        }
    }
}
