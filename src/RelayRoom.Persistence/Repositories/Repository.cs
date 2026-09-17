using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RelayRoom.Application.Persistence;
using RelayRoom.Core.Domain;

namespace RelayRoom.Persistence.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : class, IEntity
{
    protected DbSet<T> Set => context.Set<T>();

    public void Add(T entity) => Set.Add(entity);

    public void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);

    public void Remove(T entity) => Set.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities) => Set.RemoveRange(entities);

    public async Task<T?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> GetWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        await Set.Where(predicate).ToListAsync(cancellationToken);
}
