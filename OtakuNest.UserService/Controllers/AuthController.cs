using Microsoft.AspNetCore.Http;
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
            var token = await _authService.RegisterAsync(dto, cancellationToken);
            return Ok(new { Token = token });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto, CancellationToken cancellationToken)
        {
            var token = await _authService.LoginAsync(dto, cancellationToken);
            return Ok(new { Token = token });
        }
    }
}
