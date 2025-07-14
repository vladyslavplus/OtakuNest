using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Services
{
    public interface ITokenService
    {
        Task<string> GenerateTokenAsync(ApplicationUser user);
    }
}
