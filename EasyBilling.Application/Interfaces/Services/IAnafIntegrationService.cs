using EasyBilling.Application.Dtos;
using EasyBilling.Application.Responses;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Services
{
    public interface IAnafIntegrationService
    {
        Task SaveAnafTokenAsync(AnafTokenCreateDto anafTokenCreateDto, CancellationToken cancellationToken = default);
        Task UpdateAnafTokenAsync(AnafToken anafToken, CancellationToken cancellationToken = default);
        Task<AnafToken?> GetAnafTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsTokenExpiredAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<AnafUploadResult> UploadXmlToAnaf(Guid invoiceId, CancellationToken cancellationToken = default);
    }
}
