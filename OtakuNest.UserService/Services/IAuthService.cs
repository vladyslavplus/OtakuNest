using OtakuNest.UserService.DTOs;

namespace OtakuNest.UserService.Services
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(UserRegisterDto dto, CancellationToken cancellationToken);
        Task<string> LoginAsync(UserLoginDto dto, CancellationToken cancellationToken);
    }
}
