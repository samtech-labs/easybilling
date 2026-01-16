using EasyBilling.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public record LoginRequest(string Username, string Secret);

        [HttpPost("token")]
        public async Task<IActionResult> GetToken([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Token request received for user: {Username}", request.Username);

            try
            {
                if (request == null)
                {
                    _logger.LogWarning("Token request with null LoginRequest");
                    return BadRequest(new { error = "Invalid login request" });
                }

                if (string.IsNullOrWhiteSpace(request.Username))
                {
                    _logger.LogWarning("Token request with empty username");
                    return BadRequest(new { error = "Username is required" });
                }

                if (string.IsNullOrWhiteSpace(request.Secret))
                {
                    _logger.LogWarning("Token request for user {Username} with empty password", request.Username);
                    return BadRequest(new { error = "Password is required" });
                }

                _logger.LogDebug("Authenticating user: {Username}", request.Username);

                var token = await _authService.AuthenticateAndGenerateTokenAsync(request.Username, request.Secret);

                if (token == null)
                {
                    _logger.LogWarning("Authentication failed for user: {Username}", request.Username);
                    return Unauthorized(new { error = "Invalid username or password" });
                }

                _logger.LogInformation("Token successfully generated for user: {Username}", request.Username);

                return Ok(new { access_token = token, token_type = "Bearer" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating token for user: {Username}", request?.Username);
                return StatusCode(500, new { error = "An error occurred while processing your request" });
            }
        }
    }
}
