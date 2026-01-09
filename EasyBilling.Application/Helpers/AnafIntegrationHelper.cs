using EasyBilling.Application.Interfaces.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace EasyBilling.Application.Helpers
{
    public class AnafIntegrationHelper(IHttpClientFactory httpClientFactory): IAnafIntegrationHelper
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public HttpClient CreateAuthenticatedClient(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }
    }
}
