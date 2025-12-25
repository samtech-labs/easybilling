using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace EasyBilling.Application.Helpers
{
    public class AnafIntegrationHelper(
        IHttpClientFactory httpClientFactory,
        ICompanyService companyService,
        IAnafIntegrationService anafIntegrationService): IAnafIntegrationHelper
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ICompanyService _companyService = companyService;
        private readonly IAnafIntegrationService _anafIntegrationService = anafIntegrationService;

        public HttpClient CreateAuthenticatedClient(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        public async Task<AnafToken?> GetTokenForCifAsync(string cif, CancellationToken ct)
        {
            var company = await _companyService.GetCompanyByCifAsync(cif, ct);
            if (company is null)
            {
                return null;
            }

            if (company.User is null)
            {
                return null;
            }

            var anafToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(company.User.Id, ct);

            return anafToken;
        }
    }
}
