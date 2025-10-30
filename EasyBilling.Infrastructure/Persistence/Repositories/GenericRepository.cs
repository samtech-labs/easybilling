using EasyBilling.Application.Interfaces;
using Microsoft.EntityFrameworkCore;


namespace EasyBilling.Infrastructure.Persistence.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {

        private readonly AppDbContext _context;
        private readonly DbSet<T> _entities;

        public GenericRepository(AppDbContext context)
        {
            _context = context;
            _entities = _context.Set<T>();
        }
        public async Task<T> AddAsync(T entity, CancellationToken ct = default)
        {
            await _entities.AddAsync(entity, ct);
            return entity;
        }

        public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _entities.FindAsync(id, ct);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        {
            return await _entities.ToListAsync(ct);
        }
        public async Task<T> UpdateAsync(T entity, CancellationToken ct = default)
        {
            _entities.Update(entity);
            return entity;
        }

        public async Task DeleteAsync(T entity, CancellationToken ct = default)
        {
            _entities.Remove(entity);
        }
    }
}
