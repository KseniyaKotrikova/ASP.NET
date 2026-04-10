using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain;

namespace PromoCodeFactory.DataAccess.Repositories;

internal class EfRepository<T>(PromoCodeFactoryDbContext context) : IRepository<T> where T : BaseEntity
{
    //protected readonly PromoCodeFactoryDbContext _context = context;
    private readonly DbSet<T> _dbSet = context.Set<T>();
    protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query) => query;


    public async Task Add(T entity, CancellationToken ct)
    {
        await _dbSet.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task Delete(Guid id, CancellationToken ct)
    {
        var entity = await GetById(id, false, ct);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyCollection<T>> GetAll(bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;

        if (withIncludes)
            query = ApplyIncludes(query);

        return await query.ToListAsync(ct);
    }

    public async Task<T?> GetById(Guid id, bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;

        if (withIncludes)
            query = ApplyIncludes(query);

        return await query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<T>> GetByRangeId(IEnumerable<Guid> ids, bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;

        if (withIncludes)
        {
            query = ApplyIncludes(query);
        }

        return await query
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<T>> GetWhere(Expression<Func<T, bool>> predicate, bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = _dbSet;
        if (withIncludes)
        {
            query = ApplyIncludes(query);
        }

        return await query
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task Update(T entity, CancellationToken ct)
    {
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync(ct);
    }
}
