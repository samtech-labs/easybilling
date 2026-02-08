using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces.Helpers
{
    public interface IAnafIntegrationHelper
    {
        HttpClient CreateAuthenticatedClient(string accessToken);
    }
}