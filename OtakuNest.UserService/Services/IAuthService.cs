using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Services
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(UserRegisterDto dto, CancellationToken cancellationToken);
        Task<string> LoginAsync(UserLoginDto dto, CancellationToken cancellationToken);
        Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string refreshToken);
        Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken);
        Task RevokeRefreshTokenAsync(string refreshToken);
    }
}
