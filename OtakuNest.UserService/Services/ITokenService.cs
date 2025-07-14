using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Services
{
    public interface ITokenService
    {
        Task<string> GenerateTokenAsync(ApplicationUser user);
        Task<string> GenerateRefreshTokenAsync();
        Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(ApplicationUser user);
        Task SaveRefreshTokenAsync(Guid userId, string refreshToken, DateTime expires);
        Task<bool> ValidateRefreshTokenAsync(string refreshToken, Guid userId);
        Task RevokeRefreshTokenAsync(string refreshToken);
        Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken);
        Task RevokeAllRefreshTokensForUserAsync(Guid userId);
    }
}
