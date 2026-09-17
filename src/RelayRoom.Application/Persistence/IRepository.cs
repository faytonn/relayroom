using System.Linq.Expressions;
using RelayRoom.Core.Domain;

namespace RelayRoom.Application.Persistence;

public interface IRepository<T> where T : class, IEntity
{
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    Task<T?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}
