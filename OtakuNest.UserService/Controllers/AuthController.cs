using Microsoft.AspNetCore.Mvc;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Services;

namespace OtakuNest.UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDto dto, CancellationToken cancellationToken)
        {
            var accessToken = await _authService.RegisterAsync(dto, cancellationToken);
            return Ok(new { AccessToken = accessToken });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto, CancellationToken cancellationToken)
        {
            var accessToken = await _authService.LoginAsync(dto, cancellationToken);
            return Ok(new { AccessToken = accessToken });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
                return Unauthorized("Refresh token is missing.");

            var tokens = await _authService.RefreshTokenAsync(refreshToken);

            return Ok(new { AccessToken = tokens.AccessToken });
        }


        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeToken()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
                return BadRequest("Refresh token is missing.");

            await _authService.RevokeRefreshTokenAsync(refreshToken);

            Response.Cookies.Delete("refreshToken");

            return NoContent();
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
                return BadRequest("Refresh token is missing.");

            var tokenEntity = await _authService.GetRefreshTokenEntityAsync(refreshToken);
            if (tokenEntity == null)
                return BadRequest("Invalid refresh token.");

            await _authService.RevokeRefreshTokenAsync(refreshToken);

            Response.Cookies.Delete("refreshToken");

            return NoContent();
        }
    }
}
