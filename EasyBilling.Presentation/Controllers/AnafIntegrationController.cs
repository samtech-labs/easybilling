using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace EasyBilling.Presentation.Controllers;

[ApiController]
[Route("api/anaf")]
public class AnafAuthController(
    IConfiguration config,
    ICurrentUserService currentUserService,
    IAnafIntegrationService anafIntegrationService,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider,
    IOAuthCallbackResponseGenerator callbackResponseGenerator,
    ILogger<AnafAuthController> logger) : ControllerBase
{
    private readonly IConfiguration _config = config;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IAnafIntegrationService _anafIntegrationService = anafIntegrationService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Anaf.OAuth.State");
    private readonly IOAuthCallbackResponseGenerator _callbackResponseGenerator = callbackResponseGenerator;
    private readonly ILogger<AnafAuthController> _logger = logger;

    [HttpGet("authorize")]
    [Authorize]
    public IActionResult Authorize()
    {
        _logger.LogInformation("ANAF authorization request initiated");

        try
        {
            var userId = _currentUserService.UserId;

            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Authorization request with invalid UserId");
                return Unauthorized();
            }

            _logger.LogDebug("Generating authorization URL for user {UserId}", userId);

            var state = _protector.Protect(userId.ToString());

            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["response_type"] = "code";
            queryParams["client_id"] = _config["Anaf:ClientId"];
            queryParams["redirect_uri"] = _config["Anaf:RedirectUri"];
            queryParams["token_content_type"] = "jwt";
            queryParams["state"] = state;

            var authUrl = $"{_config["Anaf:AuthUrl"]}?{queryParams}";

            _logger.LogInformation("Authorization URL generated for user {UserId}", userId);

            return Ok(new { authUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ANAF authorization URL");
            throw;
        }
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ANAF callback received");

        try
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                _logger.LogWarning("Callback received with missing code or state parameter");
                var errorHtml = _callbackResponseGenerator.GenerateErrorResponse("Missing code or state parameter");
                return Content(errorHtml, "text/html");
            }

            _logger.LogDebug("Callback parameters received, decrypting state");

            Guid userId;
            try
            {
                var decrypted = _protector.Unprotect(state);
                userId = Guid.Parse(decrypted);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning(ex, "Invalid or tampered state parameter in callback");
                return BadRequest("Invalid or tampered state");
            }

            _logger.LogDebug("State decrypted successfully for user {UserId}", userId);

            var clientId = _config["Anaf:ClientId"];
            var clientSecret = _config["Anaf:ClientSecret"];
            var redirectUri = _config["Anaf:RedirectUri"];
            var tokenUrl = _config["Anaf:TokenUrl"];

            var httpClient = _httpClientFactory.CreateClient();

            var basicAuthValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", basicAuthValue);

            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri ?? string.Empty },
                { "token_content_type", "jwt" }
            };

            _logger.LogDebug("Exchanging authorization code for token from ANAF");

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to exchange authorization code for token. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                var errorHtml = _callbackResponseGenerator.GenerateErrorResponse($"Failed to exchange code for token: {errorContent}");
                return Content(errorHtml, "text/html");
            }

            var tokenResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenData = JsonSerializer.Deserialize<AnafTokenResponseDto>(tokenResponse);

            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                _logger.LogError("Invalid token response received from ANAF");
                var errorHtml = _callbackResponseGenerator.GenerateErrorResponse("Invalid token response from ANAF");
                return Content(errorHtml, "text/html");
            }

            _logger.LogDebug("Token received from ANAF, saving for user {UserId}", userId);

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId, cancellationToken);

            if (existingToken != null)
            {
                _logger.LogInformation("Updating existing ANAF token for user {UserId}", userId);

                existingToken.AccessToken = tokenData.AccessToken;
                existingToken.RefreshToken = tokenData.RefreshToken ?? existingToken.RefreshToken;
                existingToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.CreatedAt = DateTime.UtcNow;

                await _anafIntegrationService.UpdateAnafTokenAsync(existingToken, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Creating new ANAF token for user {UserId}", userId);

                var newToken = new AnafTokenCreateDto
                {
                    UserId = userId,
                    AccessToken = tokenData.AccessToken,
                    RefreshToken = tokenData.RefreshToken ?? string.Empty,
                    AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn),
                    RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(365),
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn),
                    CreatedAt = DateTime.UtcNow
                };

                await _anafIntegrationService.SaveAnafTokenAsync(newToken, cancellationToken);
            }

            _logger.LogInformation("ANAF token successfully saved/updated for user {UserId}", userId);

            var successHtml = _callbackResponseGenerator.GenerateSuccessResponse();
            return Content(successHtml, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ANAF callback");
            throw;
        }
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ANAF token refresh request initiated");

        try
        {
            var userId = _currentUserService.UserId;

            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Token refresh request with invalid UserId");
                return Unauthorized();
            }

            _logger.LogDebug("Retrieving existing token for user {UserId}", userId);

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId, cancellationToken);

            if (existingToken == null || string.IsNullOrEmpty(existingToken.RefreshToken))
            {
                _logger.LogWarning("No valid refresh token found for user {UserId}", userId);
                return BadRequest(new { error = "No refresh token found. Please re-authorize." });
            }

            _logger.LogDebug("Exchanging refresh token for new access token for user {UserId}", userId);

            var clientId = _config["Anaf:ClientId"];
            var clientSecret = _config["Anaf:ClientSecret"];
            var tokenUrl = _config["Anaf:TokenUrl"];

            var httpClient = _httpClientFactory.CreateClient();

            var basicAuthValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", basicAuthValue);

            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", existingToken.RefreshToken }
            };

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to refresh token for user {UserId}. Status: {StatusCode}, Error: {Error}",
                    userId, response.StatusCode, errorContent);
                return StatusCode((int)response.StatusCode,
                    new { error = "Failed to refresh token", details = errorContent });
            }

            var tokenResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenData = JsonSerializer.Deserialize<AnafTokenResponseDto>(tokenResponse);

            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                _logger.LogError("Invalid token response received when refreshing token for user {UserId}", userId);
                return BadRequest(new { error = "Invalid token response" });
            }

            _logger.LogDebug("New token received for user {UserId}, updating stored token", userId);

            existingToken.AccessToken = tokenData.AccessToken;
            existingToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
            existingToken.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);

            if (!string.IsNullOrEmpty(tokenData.RefreshToken))
            {
                existingToken.RefreshToken = tokenData.RefreshToken;
                existingToken.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(365);
            }

            await _anafIntegrationService.UpdateAnafTokenAsync(existingToken, cancellationToken);

            _logger.LogInformation("ANAF token successfully refreshed for user {UserId}", userId);

            return Ok(new
            {
                message = "Token refreshed successfully",
                expiresAt = existingToken.AccessTokenExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing ANAF token");
            throw;
        }
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> TokenStatus(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ANAF token status check requested");

        try
        {
            var userId = _currentUserService.UserId;

            _logger.LogDebug("Retrieving token status for user {UserId}", userId);

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId, cancellationToken);

            if (existingToken == null || string.IsNullOrEmpty(existingToken.RefreshToken))
            {
                _logger.LogInformation("User {UserId} is not authorized with ANAF", userId);
                return Ok(new { isAuthorized = false });
            }

            _logger.LogInformation("User {UserId} is authorized with ANAF, access token expires at {ExpiresAt}",
                userId, existingToken.AccessTokenExpiresAt);

            return Ok(new
            {
                isAuthorized = true,
                accessTokenExpiresAt = existingToken.AccessTokenExpiresAt,
                refreshTokenExpiresAt = existingToken.RefreshTokenExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking ANAF token status");
            throw;
        }
    }
}
