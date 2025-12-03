using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByClientId(string client_id, string client_secret)
    {
        if (!Guid.TryParse(client_id, out var clientIdGuid))
            return null; 

        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Client_Id == clientIdGuid && u.Client_Secret == client_secret);
    }
}