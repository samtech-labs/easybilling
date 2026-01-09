using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using EasyBilling.Application.Interfaces.Repositories;

namespace EasyBilling.Infrastructure.Repositories
{
    public class ClientRepository(AppDbContext db) : IClientRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Clients
                .Where(c => c.CompanyId == companyId)
                .ToListAsync(cancellationToken);
        }

        public async Task<Client?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default)
        {
            return await _db.Clients.FindAsync(new object?[] { clientId }, cancellationToken: cancellationToken);
        }

        public async Task<Client?> GetByCuiAndCompanyIdAsync(string cui, Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _db.Clients
                .FirstOrDefaultAsync(c => c.CUI == cui && c.CompanyId == companyId, cancellationToken);
        }

        public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
        {
            await _db.Clients.AddAsync(client, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Client client, CancellationToken cancellationToken = default)
        {
            _db.Clients.Remove(client);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
