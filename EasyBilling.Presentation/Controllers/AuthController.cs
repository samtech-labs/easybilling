using EasyBilling.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        public record LoginRequest(string Username, string Secret);

        [HttpPost("token")]
        public async Task<IActionResult> GetToken([FromBody] LoginRequest request)
        {
            var token = await _authService.AuthenticateAndGenerateTokenAsync(request.Username, request.Secret);
            if (token == null)
                return Unauthorized();

            return Ok(new { access_token = token, token_type = "Bearer" });
        }
    }
}
