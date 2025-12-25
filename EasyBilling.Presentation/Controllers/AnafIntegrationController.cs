using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Services;
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
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return CallbackError("Missing code or state parameter");
            }

            Guid userId;
            try
            {
                var userIdString = Encoding.UTF8.GetString(Convert.FromBase64String(state));
                userId = Guid.Parse(userIdString);
            }
            catch
            {
                return CallbackError("Invalid state parameter");
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

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return CallbackError($"Failed to exchange code for token: {errorContent}");
            }

            var tokenResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenData = JsonSerializer.Deserialize<AnafTokenResponseDto>(tokenResponse);

            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                return CallbackError("Invalid token response from ANAF");
            }

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId, cancellationToken);

            if (existingToken != null)
            {
                existingToken.AccessToken = tokenData.AccessToken;
                existingToken.RefreshToken = tokenData.RefreshToken ?? existingToken.RefreshToken;
                existingToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.CreatedAt = DateTime.UtcNow;

                await _anafIntegrationService.UpdateAnafTokenAsync(existingToken, cancellationToken);
            }
            else
            {
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

            return CallbackSuccess();
        }

        [HttpPost("refresh")]
        [Authorize]
        public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;

            if (userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId, cancellationToken);

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

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return StatusCode((int)response.StatusCode,
                    new { error = "Failed to refresh token", details = errorContent });
            }

            var tokenResponse = await response.Content.ReadAsStringAsync(cancellationToken);
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

            await _anafIntegrationService.UpdateAnafTokenAsync(existingToken, cancellationToken);

            return Ok(new
            {
                message = "Token refreshed successfully",
                expiresAt = existingToken.AccessTokenExpiresAt
            });
        }

        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> TokenStatus(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;

            var existingToken = await _anafIntegrationService.GetAnafTokenByUserIdAsync(userId, cancellationToken);

            if (existingToken == null || string.IsNullOrEmpty(existingToken.RefreshToken))
            {
                return Ok(new { isAuthorized = false });
            }

            return Ok(new
            {
                isAuthorized = true,
                accessTokenExpiresAt = existingToken.AccessTokenExpiresAt,
                refreshTokenExpiresAt = existingToken.RefreshTokenExpiresAt
            });
        }

        private ContentResult CallbackSuccess()
        {
            var html = @"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>ANAF Authorization</title>
                    <style>
                        body { font-family: system-ui, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #f0fdf4; }
                        .container { text-align: center; padding: 2rem; }
                        .icon { font-size: 4rem; margin-bottom: 1rem; }
                        h1 { color: #166534; margin-bottom: 0.5rem; }
                        p { color: #6b7280; }
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='icon'>✓</div>
                        <h1>Authorization Successful</h1>
                        <p>This window will close automatically...</p>
                    </div>
                    <script>
                        if (window.opener) {
                            window.opener.postMessage({ type: 'ANAF_AUTH_SUCCESS' }, '*');
                            setTimeout(function() { window.close(); }, 1500);
                        }
                    </script>
                </body>
                </html>";

            return Content(html, "text/html");
        }

        private ContentResult CallbackError(string errorMessage)
        {
            var sanitizedError = errorMessage
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", " ")
                .Replace("\r", " ");

            var html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>ANAF Authorization Error</title>
                    <style>
                        body {{ font-family: system-ui, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #fef2f2; }}
                        .container {{ text-align: center; padding: 2rem; max-width: 500px; }}
                        .icon {{ font-size: 4rem; margin-bottom: 1rem; }}
                        h1 {{ color: #dc2626; margin-bottom: 0.5rem; }}
                        p {{ color: #6b7280; }}
                        .error {{ background: #fee2e2; padding: 1rem; border-radius: 8px; margin-top: 1rem; color: #991b1b; font-size: 0.875rem; word-break: break-word; }}
                        button {{ margin-top: 1rem; padding: 0.5rem 1rem; background: #6b7280; color: white; border: none; border-radius: 4px; cursor: pointer; }}
                        button:hover {{ background: #4b5563; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='icon'>✕</div>
                        <h1>Authorization Failed</h1>
                        <p>There was a problem connecting to ANAF.</p>
                        <div class='error'>{sanitizedError}</div>
                        <button onclick='window.close()'>Close Window</button>
                    </div>
                    <script>
                        if (window.opener) {{
                            window.opener.postMessage({{ type: 'ANAF_AUTH_ERROR', error: '{sanitizedError}' }}, '*');
                        }}
                    </script>
                </body>
                </html>";

            return Content(html, "text/html");
        }
    }
}
