using EasyBilling.Application.Interfaces;
using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using EasyBilling.Application.Requests;

namespace EasyBilling.Infrastructure.Repositories
{
    public class ClientRepository(AppDbContext db) : IClientRepository
    {
        private readonly AppDbContext _db = db;

        public async Task<List<Client>> GetAllClientsByCompanyIdAsync(Guid companyId)
        {
            return await _db.Clients
                .Where(c => c.CompanyId == companyId)
                .ToListAsync();
        }

        public async Task<Client?> GetByIdAsync(Guid clientId)
        {
            return await _db.Clients.FindAsync(clientId);
        }

        public async Task<Client?> GetByCuiAndCompanyIdAsync(string cui, Guid companyId)
        {
            return await _db.Clients
                .FirstOrDefaultAsync(c => c.CUI == cui && c.CompanyId == companyId);
        }

        public async Task AddAsync(Client client)
        {
            await _db.Clients.AddAsync(client);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Client client)
        {
            _db.Clients.Remove(client);
            await _db.SaveChangesAsync();
        }
    }
}
