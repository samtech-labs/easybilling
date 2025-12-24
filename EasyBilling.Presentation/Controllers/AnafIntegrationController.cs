using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

namespace EasyBilling.Presentation.Controllers
{
    [ApiController]
    [Route("api/anaf")]
    public class AnafAuthController(
        IConfiguration config,
        ICurrentUserService currentUserService,
        IAnafIntegrationService anafIntegrationService,
        IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private readonly IConfiguration _config = config;
        private readonly ICurrentUserService _currentUserService = currentUserService;
        private readonly IAnafIntegrationService _anafIntegrationService = anafIntegrationService;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        [HttpGet("authorize")]
        [Authorize]
        public IActionResult Authorize()
        {
            var userId = _currentUserService.UserId;

            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var state = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(userId.ToString()));

            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["response_type"] = "code";
            queryParams["client_id"] = _config["Anaf:ClientId"];
            queryParams["redirect_uri"] = _config["Anaf:RedirectUri"];
            queryParams["token_content_type"] = "jwt";
            queryParams["state"] = state;

            var authUrl = $"{_config["Anaf:AuthUrl"]}?{queryParams}";

            return Ok(new { authUrl });
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return BadRequest("Missing code or state parameter");
            }

            Guid userId;
            try
            {
                var userIdString = Encoding.UTF8.GetString(Convert.FromBase64String(state));
                userId = Guid.Parse(userIdString);
            }
            catch
            {
                return BadRequest("Invalid state parameter");
            }

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

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode,
                    new { error = "Failed to exchange code for token", details = errorContent });
            }

            var tokenResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<AnafTokenResponseDto>(tokenResponse);

            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                return BadRequest("Invalid token response");
            }

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId);

            if (existingToken != null)
            {
                existingToken.AccessToken = tokenData.AccessToken;
                existingToken.RefreshToken = tokenData.RefreshToken ?? existingToken.RefreshToken;
                existingToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.CreatedAt = DateTime.UtcNow;

                await _anafIntegrationService.UpdateAnafTokenAsync(existingToken);
            }
            else
            {
                var newToken = new AnafTokenCreateDto
                {
                    UserId = userId,
                    AccessToken = tokenData.AccessToken,
                    RefreshToken = tokenData.RefreshToken ?? string.Empty,
                    AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn),
                    RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(90),
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn),
                    CreatedAt = DateTime.UtcNow
                };

                await _anafIntegrationService.SaveAnafTokenAsync(newToken);
            }

            return Ok(new { message = "ANAF integration successful", userId });
        }

        [HttpPost("refresh")]
        [Authorize]
        public async Task<IActionResult> RefreshToken()
        {
            var userId = _currentUserService.UserId;

            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId);

            if (existingToken == null || string.IsNullOrEmpty(existingToken.RefreshToken))
            {
                return BadRequest(new { error = "No refresh token found. Please re-authorize." });
            }

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

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode,
                    new { error = "Failed to refresh token", details = errorContent });
            }

            var tokenResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<AnafTokenResponseDto>(tokenResponse);

            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                return BadRequest(new { error = "Invalid token response" });
            }

            existingToken.AccessToken = tokenData.AccessToken;
            existingToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
            existingToken.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);

            if (!string.IsNullOrEmpty(tokenData.RefreshToken))
            {
                existingToken.RefreshToken = tokenData.RefreshToken;
                existingToken.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(365);
            }

            await _anafIntegrationService.UpdateAnafTokenAsync(existingToken);

            return Ok(new
            {
                message = "Token refreshed successfully",
                expiresAt = existingToken.AccessTokenExpiresAt
            });
        }
    }
}
