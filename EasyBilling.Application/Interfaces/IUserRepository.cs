using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByClientId(string client_id, string client_secret);
}