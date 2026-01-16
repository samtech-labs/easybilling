using EasyBilling.Application.Interfaces.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace EasyBilling.Application.Helpers;

public class AnafIntegrationHelper(
    IHttpClientFactory httpClientFactory,
    ILogger<AnafIntegrationHelper> logger) : IAnafIntegrationHelper
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<AnafIntegrationHelper> _logger = logger;

    public HttpClient CreateAuthenticatedClient(string accessToken)
    {
        _logger.LogInformation("Creating authenticated HTTP client for ANAF integration");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("Access token is null or empty when creating authenticated client");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogDebug("Authenticated HTTP client created successfully");
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating authenticated HTTP client");
            throw;
        }
    }
}
