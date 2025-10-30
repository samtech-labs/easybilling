using EasyBilling.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyBilling.Application.Interfaces
{
    public interface ICompanyRepository : IGenericRepository<Company>
    {
        Task<IReadOnlyList<Company>> ListByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
        
        Task<bool> TaxIdExistsForOwnerAsync(Guid ownerId,string taxId, CancellationToken ct = default);
    }
}
