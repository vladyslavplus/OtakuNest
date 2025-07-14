using MassTransit;
using Microsoft.AspNetCore.Identity;
using OtakuNest.Contracts;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Exceptions;
using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService,
            IPublishEndpoint publishEndpoint,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _publishEndpoint = publishEndpoint;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> RegisterAsync(UserRegisterDto dto, CancellationToken cancellationToken)
        {
            var userExists = await _userManager.FindByNameAsync(dto.UserName);
            if (userExists != null)
                throw new InvalidOperationException("User already exists.");

            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new UserCreationException($"User creation failed: {errors}");
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new RoleAssignmentException($"Adding role failed: {errors}");
            }

            var userCreatedEvent = new UserCreatedEvent(
                user.Id,
                user.UserName!,
                user.Email!,
                DateTime.UtcNow);

            await _publishEndpoint.Publish(userCreatedEvent, cancellationToken);

            var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            };

            _httpContextAccessor.HttpContext!.Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

            return accessToken;
        }

        public async Task<string> LoginAsync(UserLoginDto dto, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                throw new UnauthorizedAccessException("User not found.");

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid)
                throw new UnauthorizedAccessException("Invalid password.");

            var (accessToken, refreshToken) = await _tokenService.GenerateTokensAsync(user);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            };

            _httpContextAccessor.HttpContext!.Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

            return accessToken;
        }

        public async Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken)
        {
            var tokenEntity = await _tokenService.GetRefreshTokenEntityAsync(refreshToken);
            if (tokenEntity == null || tokenEntity.Revoked != null || tokenEntity.Expires <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid refresh token.");

            var user = tokenEntity.User ?? await _userManager.FindByIdAsync(tokenEntity.UserId.ToString());
            if (user == null)
                throw new UnauthorizedAccessException("User not found.");

            await _tokenService.RevokeRefreshTokenAsync(refreshToken);

            var tokens = await _tokenService.GenerateTokensAsync(user);
            return tokens;
        }
        public async Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken)
        {
            return await _tokenService.GetRefreshTokenEntityAsync(refreshToken);
        }
        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
        }
    }
}
