namespace ClaimsIntake.Application.Abstractions;

public interface IRepository<T> where T : class
{
    /// <summary>Returns the entity with the given id, or <c>null</c> if it does not exist.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns all entities.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Stages a new entity for insertion; call <see cref="SaveChangesAsync"/> to persist.</summary>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Stages an entity for deletion; call <see cref="SaveChangesAsync"/> to persist.</summary>
    void Remove(T entity);

    /// <summary>Persists all staged changes and returns the number of affected rows.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
