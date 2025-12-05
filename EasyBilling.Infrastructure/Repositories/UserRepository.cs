using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using EasyBilling.Infrastructure.Persistence;

namespace EasyBilling.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}