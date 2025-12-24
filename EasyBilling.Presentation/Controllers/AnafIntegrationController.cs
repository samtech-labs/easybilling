using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using EasyBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace EasyBilling.Presentation.Controllers
{
    [ApiController]
    [Route("api/anaf")]
    public class AnafAuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ICurrentUserService _currentUserService;
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public AnafAuthController(
            IConfiguration config,
            ICurrentUserService currentUserService,
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _currentUserService = currentUserService;
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
        }

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
       //    queryParams["state"] = state;

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

            // Decode user ID from state
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

            // Exchange authorization code for access token using Basic Auth
            var clientId = _config["Anaf:ClientId"];
            var clientSecret = _config["Anaf:ClientSecret"];
            var redirectUri = _config["Anaf:RedirectUri"];
            var tokenUrl = _config["Anaf:TokenUrl"];

            var httpClient = _httpClientFactory.CreateClient();

            // Set Basic Auth header as per ANAF documentation
            var basicAuthValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", basicAuthValue);

            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri ?? string.Empty }
            };

            var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode,
                    new { error = "Failed to exchange code for token", details = errorContent });
            }

            var tokenResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<AnafTokenResponse>(tokenResponse);

            if (tokenData == null || string.IsNullOrEmpty(tokenData.AccessToken))
            {
                return BadRequest("Invalid token response");
            }

            // Store or update token in database
            var existingToken = await _dbContext.AnafTokens
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (existingToken != null)
            {
                existingToken.AccessToken = tokenData.AccessToken;
                existingToken.RefreshToken = tokenData.RefreshToken ?? existingToken.RefreshToken;
                existingToken.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                existingToken.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                var newToken = new AnafToken
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AccessToken = tokenData.AccessToken,
                    RefreshToken = tokenData.RefreshToken ?? string.Empty,
                    AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn),
                    RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30), // Default, adjust as needed
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.AnafTokens.Add(newToken);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "ANAF integration successful", userId });
        }

        private class AnafTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; } = string.Empty;
        }
    }
}
