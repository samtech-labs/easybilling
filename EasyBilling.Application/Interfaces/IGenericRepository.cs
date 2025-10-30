using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyBilling.Application.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T> AddAsync(T entity, CancellationToken ct = default);

        Task<T> UpdateAsync(T entity, CancellationToken ct = default);

        Task DeleteAsync(T entity, CancellationToken ct = default);

        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

        Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    }
}
