using EasyBilling.Application.Dtos;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IAnafIntegrationService
    {
        Task SaveAnafTokenAsync(AnafTokenCreateDto anafTokenCreateDto, CancellationToken cancellationToken = default);
        Task UpdateAnafTokenAsync(AnafToken anafToken, CancellationToken cancellationToken = default);
        Task<AnafToken?> GetAnafTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsTokenExpiredAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<string> UploadXmlToAnaf(Guid invoiceId, string xmlContent, CancellationToken cancellationToken = default);
    }
}
