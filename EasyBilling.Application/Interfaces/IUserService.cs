using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces;

public interface IUserService
{
    Task<bool> DecodeBasicAuth(string authHeader);
}