using EasyBilling.Application.Dtos;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IAnafIntegrationService
    {
        Task SaveAnafTokenAsync(AnafTokenCreateDto anafTokenCreateDto);
        Task UpdateAnafTokenAsync(AnafToken anafToken);
        Task<AnafToken?> GetAnafTokenByUserIdAsync(Guid userId);
        Task<bool> IsTokenExpiredAsync(Guid userId);
    }
}
