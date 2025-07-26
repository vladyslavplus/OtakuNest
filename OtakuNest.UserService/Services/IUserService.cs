using OtakuNest.Common.Helpers;
using OtakuNest.UserService.DTOs;
using OtakuNest.UserService.Models;
using OtakuNest.UserService.Parameters;

namespace OtakuNest.UserService.Services
{
    public interface IUserService
    {
        Task<PagedList<ApplicationUser>> GetUsersAsync(UserParameters parameters, CancellationToken cancellationToken = default);
        Task<ApplicationUser?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> UpdateUserAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default);
    }
}
