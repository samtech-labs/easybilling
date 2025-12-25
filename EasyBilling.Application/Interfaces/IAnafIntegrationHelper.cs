using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IAnafIntegrationHelper
    {
        HttpClient CreateAuthenticatedClient(string accessToken);
        Task<AnafToken?> GetTokenForCifAsync(string cif, CancellationToken ct);
    }
}
