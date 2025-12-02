using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces;

public interface IUserService
{
    Task<bool> DecodeToken(string authHeader);
}